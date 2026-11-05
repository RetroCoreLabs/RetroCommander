//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- Headless-driven tests for the NDFS access and friend controls:
// the friend add / change / delete and default-file-access boxes in NdfsUserInfoWindow, and the
// File Access section of PropertiesDialog. They mirror the SINTRAN commands @CREATE-FRIEND,
// @SET-FRIEND-ACCESS, @DELETE-FRIEND, @SET-DEFAULT-FILE-ACCESS and @SET-FILE-ACCESS. Each test
// builds its own small image (users ALICE and BOB, one file ALICE/DATA:SYMB), drives the REAL
// window by clicking its buttons, and checks the result again after closing and mounting the
// image a second time, so a change that only lived in memory fails.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Ndfs.Creation;
using RetroIO.Storage.Ndfs.Drivers;
using RetroIO.Storage.Ndfs.Elements;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class NdfsAccessUiTests
    {
        private const string Owner = "ALICE";
        private const string Friend = "BOB";
        private const string DataFile = "ALICE/DATA:SYMB";

        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "rc-ndfs-access-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        private static void Pump() => Dispatcher.UIThread.RunJobs();

        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        /// <summary>
        /// Builds the test image and closes it again.
        /// </summary>
        private string MakeImage(string name)
        {
            string imagePath = Path.Combine(_testDir, name + ".img");
            NdfsImageCreator.CreateImageFromTemplate(
                imagePath,
                NdfsImageTemplate.Floppy360KB,
                directoryName: "ACCESS",
                bootLoaderType: NdfsBootLoaderType.Floppy);

            using (NdfsDriver driver = Open(imagePath, readOnly: false, out INdfsFileSystem fs))
            {
                driver.CreateDirectory("/" + Owner);
                driver.CreateDirectory("/" + Friend);
                fs.WriteFile(DataFile, Encoding.ASCII.GetBytes("HELLO\r\n"));
            }
            return imagePath;
        }

        private static NdfsDriver Open(string imagePath, bool readOnly, out INdfsFileSystem fs)
        {
            FileStream stream = new FileStream(imagePath, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: readOnly);
            INdfsFileSystem? mounted = driver.GetFileSystem();
            Assert.That(mounted, Is.Not.Null, "driver must expose the NDFS filesystem");
            fs = mounted!;
            return driver;
        }

        private static int IndexOf(INdfsFileSystem fs, string userName)
        {
            IReadOnlyList<UserEntry> users = fs.GetUsers();
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].UserName == userName)
                    return users[i].UserIndex;
            }
            Assert.Fail("no user " + userName);
            return -1;
        }

        private static int FriendRowCount(NdfsUserInfoWindow window)
        {
            int count = 0;
            if (window.FriendsList.ItemsSource is System.Collections.IEnumerable rows)
            {
                foreach (object _ in rows)
                    count++;
            }
            return count;
        }

        // ---------------------------------------------------------------
        // User window: friends
        // ---------------------------------------------------------------

        [AvaloniaTest]
        public void Friends_AddChangeDelete_PersistOnDisk()
        {
            string image = MakeImage("friends");

            using (Open(image, false, out INdfsFileSystem fs))
            {
                var window = new NdfsUserInfoWindow(fs, IndexOf(fs, Owner), isReadOnly: false);
                Pump();
                Assert.That(FriendRowCount(window), Is.EqualTo(0));

                // Add with an empty rights box gives RWA, as @CREATE-FRIEND does.
                window.FriendNameBox.Text = Friend;
                window.FriendRightsBox.Text = string.Empty;
                Click(window.AddFriendButton);
                Assert.That(FriendRowCount(window), Is.EqualTo(1), window.StatusText.Text);
            }

            using (Open(image, false, out INdfsFileSystem fs))
            {
                IReadOnlyList<NdfsFriendInfo> friends = fs.ListFriends(Owner);
                Assert.That(friends.Count, Is.EqualTo(1));
                Assert.That(friends[0].Rights, Is.EqualTo("RWA--"));

                // Selecting the row fills the boxes; change the rights to W (the manual's example).
                var window = new NdfsUserInfoWindow(fs, IndexOf(fs, Owner), isReadOnly: false);
                Pump();
                window.FriendsList.SelectedIndex = 0;
                Pump();
                Assert.That(window.FriendNameBox.Text, Is.EqualTo(Friend));
                Assert.That(window.FriendRightsBox.Text, Is.EqualTo("RWA"));
                window.FriendRightsBox.Text = "W";
                Click(window.SetFriendButton);
                Assert.That(window.StatusText.Text, Does.StartWith("✓"), window.StatusText.Text);
            }

            using (Open(image, false, out INdfsFileSystem fs))
            {
                Assert.That(fs.ListFriends(Owner)[0].Rights, Is.EqualTo("-W---"));

                var window = new NdfsUserInfoWindow(fs, IndexOf(fs, Owner), isReadOnly: false);
                Pump();
                window.FriendNameBox.Text = Friend;
                Click(window.DeleteFriendButton);
                Assert.That(FriendRowCount(window), Is.EqualTo(0), window.StatusText.Text);
            }

            using (Open(image, true, out INdfsFileSystem fs))
            {
                Assert.That(fs.ListFriends(Owner).Count, Is.EqualTo(0));
            }
        }

        [AvaloniaTest]
        public void Friends_BadRights_ShowErrorAndChangeNothing()
        {
            string image = MakeImage("friendbad");

            using (Open(image, false, out INdfsFileSystem fs))
            {
                var window = new NdfsUserInfoWindow(fs, IndexOf(fs, Owner), isReadOnly: false);
                Pump();

                // N must stand alone (@SET-FILE-ACCESS rule 2).
                window.FriendNameBox.Text = Friend;
                window.FriendRightsBox.Text = "NR";
                Click(window.AddFriendButton);
                Assert.That(window.StatusText.Text, Does.StartWith("Not changed"));
                Assert.That(FriendRowCount(window), Is.EqualTo(0));

                // Changing a user who is not a friend is refused.
                window.FriendRightsBox.Text = "R";
                Click(window.SetFriendButton);
                Assert.That(window.StatusText.Text, Does.StartWith("Not changed"));
            }

            using (Open(image, true, out INdfsFileSystem fs))
            {
                Assert.That(fs.ListFriends(Owner).Count, Is.EqualTo(0));
            }
        }

        // ---------------------------------------------------------------
        // User window: default file access
        // ---------------------------------------------------------------

        [AvaloniaTest]
        public void DefaultAccess_ShownInSintranFormAndSetPersists()
        {
            string image = MakeImage("default");

            using (Open(image, false, out INdfsFileSystem fs))
            {
                var window = new NdfsUserInfoWindow(fs, IndexOf(fs, Owner), isReadOnly: false);
                Pump();
                Assert.That(window.DefaultAccessSintranText.Text, Is.EqualTo("R,RWA,RWACD"), "SINTRAN's default 0x04FF");

                // Only PUBLIC changes; the empty fields keep FRIEND and OWN.
                window.DefaultAccessBox.Text = "N,,";
                Click(window.SetDefaultAccessButton);
                Assert.That(window.DefaultAccessSintranText.Text, Is.EqualTo("N,RWA,RWACD"), window.StatusText.Text);
            }

            using (Open(image, true, out INdfsFileSystem fs))
            {
                Assert.That(fs.GetDefaultFileAccess(Owner), Is.EqualTo((ushort)0x00FF));
            }
        }

        [AvaloniaTest]
        public void ReadOnlyMount_DisablesEveryChange()
        {
            string image = MakeImage("readonly");

            using (Open(image, true, out INdfsFileSystem fs))
            {
                var window = new NdfsUserInfoWindow(fs, IndexOf(fs, Owner), isReadOnly: true);
                Pump();
                Assert.That(window.SetDefaultAccessButton.IsEnabled, Is.False);
                Assert.That(window.AddFriendButton.IsEnabled, Is.False);
                Assert.That(window.SetFriendButton.IsEnabled, Is.False);
                Assert.That(window.DeleteFriendButton.IsEnabled, Is.False);
                Assert.That(window.DefaultAccessBox.IsEnabled, Is.False);
            }
        }

        // ---------------------------------------------------------------
        // Properties dialog: one file's access
        // ---------------------------------------------------------------

        private static DriverAdapterFileSystemEntry FindDataFile(NdfsDriver driver)
        {
            var listing = driver.ListDirectory("/" + Owner);
            foreach (var item in listing)
            {
                if (!item.Metadata.IsDirectory && item.Metadata.Name.StartsWith("DATA", StringComparison.Ordinal))
                    return new DriverAdapterFileSystemEntry(driver, item.Metadata);
            }
            Assert.Fail("DATA:SYMB not listed under " + Owner);
            return null!;
        }

        [AvaloniaTest]
        public void FileAccess_SetFromPropertiesDialogPersists()
        {
            string image = MakeImage("fileaccess");

            using (NdfsDriver driver = Open(image, false, out INdfsFileSystem fs))
            {
                var dialog = new PropertiesDialog(FindDataFile(driver), fs, isReadOnly: false);
                Pump();
                Assert.That(dialog.NdfsAccessBorder.IsVisible, Is.True);
                string before = dialog.FileAccessSintranText.Text ?? string.Empty;
                Assert.That(before, Is.Not.EqualTo("(unknown)"), dialog.FileAccessStatusText.Text);

                dialog.FileAccessBox.Text = "N,R,RWACD";
                Click(dialog.SetFileAccessButton);
                Assert.That(dialog.FileAccessSintranText.Text, Is.EqualTo("N,R,RWACD"), dialog.FileAccessStatusText.Text);
                Assert.That(dialog.AccessChanged, Is.True);
            }

            using (Open(image, true, out INdfsFileSystem fs))
            {
                Assert.That(fs.GetFileAccess(DataFile), Is.EqualTo((ushort)0x003F));
            }
        }

        [AvaloniaTest]
        public void FileAccess_BadInputAndReadOnlyChangeNothing()
        {
            string image = MakeImage("filebad");

            using (NdfsDriver driver = Open(image, false, out INdfsFileSystem fs))
            {
                ushort before = fs.GetFileAccess(DataFile);
                var dialog = new PropertiesDialog(FindDataFile(driver), fs, isReadOnly: false);
                Pump();
                dialog.FileAccessBox.Text = "R,R,R,R";
                Click(dialog.SetFileAccessButton);
                Assert.That(dialog.FileAccessStatusText.Text, Does.StartWith("Not changed"));
                Assert.That(dialog.AccessChanged, Is.False);
                Assert.That(fs.GetFileAccess(DataFile), Is.EqualTo(before));
            }

            using (NdfsDriver driver = Open(image, true, out INdfsFileSystem fs))
            {
                var dialog = new PropertiesDialog(FindDataFile(driver), fs, isReadOnly: true);
                Pump();
                Assert.That(dialog.NdfsAccessBorder.IsVisible, Is.True, "the access is still shown");
                Assert.That(dialog.SetFileAccessButton.IsEnabled, Is.False);
                Assert.That(dialog.FileAccessBox.IsEnabled, Is.False);
            }
        }
    }
}
