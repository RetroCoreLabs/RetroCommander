//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroCommander.Desktop.Tests -- Headless-driven tests for NdfsUserInfoWindow (the F3-view for a top-level
// NDFS user). Constructs the REAL window over a live NDFS mount (BIGDISK0-L.IMG) and verifies the
// user fields, the password formatting (decimal + 6-digit ND octal with a 'B' suffix), read-only
// disabling of the reset button, and — when a user with a non-zero password exists — that a reset
// clears it to 0 and persists to disk.
//

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;
using RetroIO.Storage.Local.BlockDevices;
using RetroIO.Storage.Ndfs.BlockDevices;
using RetroIO.Storage.Ndfs.Elements;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class NdfsUserInfoWindowTests
    {
        private static void Pump() => Dispatcher.UIThread.RunJobs();

        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        private static string? ResolveRealImage()
        {
            const string rel = @"demo\test-images\ndfs\BIGDISK0-L.IMG";
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            string[] absolutes =
            {
                @"E:\Dev\Ronny\RetroFS\demo\test-images\ndfs\BIGDISK0-L.IMG",
                "/mnt/e/Dev/Ronny/RetroFS/demo/test-images/ndfs/BIGDISK0-L.IMG",
            };
            for (int i = 0; i < absolutes.Length; i++)
                if (File.Exists(absolutes[i])) return absolutes[i];
            return null;
        }

        private static NdfsFileSystem Mount(string imagePath, bool readOnly, out GenericBlockDevice device, out Stream stream)
        {
            stream = File.Open(imagePath, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
            var descriptor = NdfsBlockDeviceDescriptor.Create((int)stream.Length);
            device = new GenericBlockDevice(descriptor);
            device.Mount(stream, readOnly);
            var fs = new NdfsFileSystem();
            fs.Initialize(device);
            return fs;
        }

        [AvaloniaTest]
        public void UserInfo_LoadsFirstUserFromRealPack()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var users = fs.GetUsers();
                if (users.Count == 0) Assert.Ignore("pack has no users");
                UserEntry u = users[0];

                var window = new NdfsUserInfoWindow(fs, u.UserIndex, isReadOnly: true);
                Pump();

                Assert.That(window.UserNameText.Text, Is.EqualTo(u.UserName));
                Assert.That(window.UserIndexText.Text, Is.EqualTo(u.UserIndex.ToString()));
                Assert.That(window.EnterCountText.Text, Is.EqualTo(u.EnterCount.ToString()));
            }
        }

        [AvaloniaTest]
        public void UserInfo_PasswordShownAsDecimalAndOctalWithB()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var users = fs.GetUsers();
                if (users.Count == 0) Assert.Ignore("pack has no users");
                UserEntry u = users[0];

                var window = new NdfsUserInfoWindow(fs, u.UserIndex, isReadOnly: true);
                Pump();

                // Decimal must equal the raw 16-bit password value.
                Assert.That(window.PasswordDecText.Text, Is.EqualTo(u.Password.ToString()));

                // Octal must be the 6-digit ND-style octal of that same value, suffixed with 'B'.
                string expectedOctal = Convert.ToString(u.Password, 8).PadLeft(6, '0') + "B";
                Assert.That(window.PasswordOctText.Text, Is.EqualTo(expectedOctal));
                Assert.That(window.PasswordOctText.Text, Does.EndWith("B"));

                // And the two must be internally consistent (octal digits, minus the B, parse to the decimal).
                string octalDigits = window.PasswordOctText.Text!.TrimEnd('B');
                Assert.That(Convert.ToInt32(octalDigits, 8), Is.EqualTo(int.Parse(window.PasswordDecText.Text!)));
            }
        }

        [AvaloniaTest]
        public void UserInfo_ReadOnlyMount_ResetDisabled()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var users = fs.GetUsers();
                if (users.Count == 0) Assert.Ignore("pack has no users");

                var window = new NdfsUserInfoWindow(fs, users[0].UserIndex, isReadOnly: true);
                Pump();

                Assert.That(window.ResetPasswordButton.IsEnabled, Is.False, "reset must be disabled on a read-only mount");
                Assert.That(window.ReadOnlyNotice.IsVisible, Is.True);
            }
        }

        [AvaloniaTest]
        public void UserInfo_ForUserName_ResolvesTheUser()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var users = fs.GetUsers();
                if (users.Count == 0) Assert.Ignore("pack has no users");
                string name = users[0].UserName;

                var window = NdfsUserInfoWindow.ForUserName(fs, name, isReadOnly: true);
                Assert.That(window, Is.Not.Null);
                Pump();
                Assert.That(window!.UserNameText.Text, Is.EqualTo(name));

                // A non-existent name yields null.
                Assert.That(NdfsUserInfoWindow.ForUserName(fs, "NO-SUCH-USER-XYZ", true), Is.Null);
            }
        }

        [AvaloniaTest]
        public void UserInfo_Reset_ClearsPasswordAndPersists()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            // Find a user with a non-zero password to make the reset observable.
            int targetIndex = -1;
            {
                var fsRo = Mount(image!, readOnly: true, out var dev0, out var str0);
                using (dev0)
                using (str0)
                {
                    var users = fsRo.GetUsers();
                    for (int i = 0; i < users.Count; i++)
                    {
                        if (users[i].Password != 0) { targetIndex = users[i].UserIndex; break; }
                    }
                }
            }
            if (targetIndex < 0) Assert.Ignore("no user on this pack has a non-zero password to reset");

            string temp = Path.Combine(Path.GetTempPath(), "rc_userreset_" + Guid.NewGuid().ToString("N") + ".img");
            File.Copy(image!, temp, true);
            try
            {
                // Reset via the window on a read-write mount.
                var fs = Mount(temp, readOnly: false, out var device, out var stream);
                using (device)
                using (stream)
                {
                    var window = new NdfsUserInfoWindow(fs, targetIndex, isReadOnly: false);
                    Pump();

                    Assert.That(window.ResetPasswordButton.IsEnabled, Is.True);
                    Click(window.ResetPasswordButton);

                    Assert.That(window.PasswordDecText.Text, Is.EqualTo("0"));
                    Assert.That(window.PasswordOctText.Text, Is.EqualTo("000000B"));
                    Assert.That(window.PasswordStateText.Text, Does.Contain("No password"));
                    // Now that it is 0, the reset button disables itself.
                    Assert.That(window.ResetPasswordButton.IsEnabled, Is.False);
                }
                // Device disposed above → flushed to disk.

                // Re-open from scratch and confirm the password is 0 on disk.
                var fs2 = Mount(temp, readOnly: true, out var device2, out var stream2);
                using (device2)
                using (stream2)
                {
                    var users = fs2.GetUsers();
                    UserEntry? reloaded = null;
                    for (int i = 0; i < users.Count; i++)
                        if (users[i].UserIndex == targetIndex) { reloaded = users[i]; break; }

                    Assert.That(reloaded, Is.Not.Null);
                    Assert.That(reloaded!.Password, Is.EqualTo((ushort)0));
                }
            }
            finally
            {
                try { File.Delete(temp); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
