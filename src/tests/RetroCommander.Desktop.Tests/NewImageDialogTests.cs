//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- headless tests for File > New Image. The window is built from
// the RetroIO drivers' blank-disk metadata, so the tests also take their expectations from that
// metadata (BlankImageCatalogue), and one test fails if a format name is ever written into the
// window's own code. Layout ids such as "cbm-d64" are only used to pick a disk to drive.

using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class NewImageDialogTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc_newimg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        // Lets Avalonia deliver queued change events before a test reads the result.
        private static void Pump() => Dispatcher.UIThread.RunJobs();

        private static BlankImageChoice Choice(string layoutId)
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Layout.Id == layoutId)
                {
                    return all[i];
                }
            }
            throw new AssertionException("RetroIO has no layout " + layoutId);
        }

        private static NewImageDialog DialogOn(string layoutId)
        {
            NewImageDialog dialog = new NewImageDialog();
            Assert.That(dialog.SelectMachine(Choice(layoutId).Layout.Machine), Is.True);
            Pump();
            Assert.That(dialog.SelectDisk(layoutId), Is.True);
            Pump();
            return dialog;
        }

        [AvaloniaTest]
        public void MachineList_IsEveryMachineTheDriversName()
        {
            NewImageDialog dialog = new NewImageDialog();
            string[] expected = BlankImageCatalogue.Machines(BlankImageCatalogue.List());
            Assert.That(dialog.MachineList.ItemsSource, Is.EqualTo(expected));
        }

        [AvaloniaTest]
        public void DiskList_IsThatMachinesLayouts()
        {
            BlankImageChoice tiki = Choice("tiki100-400k");
            NewImageDialog dialog = new NewImageDialog();
            dialog.SelectMachine(tiki.Layout.Machine);
            Pump();

            List<string> expected = new List<string>();
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Layout.Machine == tiki.Layout.Machine)
                {
                    expected.Add(all[i].Layout.Name);
                }
            }
            Assert.That(dialog.DiskCombo.ItemsSource, Is.EqualTo(expected));
        }

        [AvaloniaTest]
        public void Options_AreTheLayoutsOwn()
        {
            NewImageDialog dialog = DialogOn("cbm-d64");
            Assert.That(dialog.OptionCount, Is.EqualTo(Choice("cbm-d64").Layout.Options.Count));

            NewImageDialog cpm = DialogOn("kaypro4");
            Assert.That(cpm.OptionCount, Is.EqualTo(Choice("kaypro4").Layout.Options.Count));
        }

        [AvaloniaTest]
        public void Details_ShowTheDriversText_AndTheSameBytesWarning()
        {
            NewImageDialog ndfs = DialogOn("ndfs-smd-75m");
            string details = Choice("ndfs-smd-75m").Layout.Details;
            Assert.That(details, Is.Not.Empty, "RetroIO describes NDFS disks");
            Assert.That(ndfs.DetailsText.Text, Does.Contain(details.Split('\n')[0]));

            NewImageDialog kaypro = DialogOn("kaypro2");
            Assert.That(Choice("kaypro2").Layout.SameBytesAs, Is.Not.Empty);
            Assert.That(kaypro.DetailsText.Text, Does.Contain("byte for byte the same"));
        }

        [AvaloniaTest]
        public void Create_IsDisabled_UntilAnOutputPathIsChosen()
        {
            NewImageDialog dialog = DialogOn("cbm-d64");
            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            dialog.OutputPath.Text = Path.Combine(_dir, "a.d64");
            Pump();
            Assert.That(dialog.CreateButton.IsEnabled, Is.True);
        }

        [AvaloniaTest]
        public void BadOptionValue_ShowsTheDriversOwnMessage()
        {
            NewImageDialog dialog = DialogOn("cbm-d64");
            dialog.OutputPath.Text = Path.Combine(_dir, "a.d64");
            dialog.SetOption("diskId", "X");
            Pump();

            BlankImageOption id = FindOption(Choice("cbm-d64").Layout, "diskId");
            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.IsVisible, Is.True);
            Assert.That(dialog.ValidationText.Text, Is.EqualTo(id.Check("X")));
        }

        [AvaloniaTest]
        public void ExistingFile_IsRefusedBeforeCreate()
        {
            string path = Path.Combine(_dir, "taken.d64");
            File.WriteAllBytes(path, new byte[] { 1 });
            NewImageDialog dialog = DialogOn("cbm-d64");
            dialog.OutputPath.Text = path;
            Pump();
            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.Text, Does.Contain("already exists"));
        }

        [AvaloniaTest]
        public void Unformatted_SwitchesOffMountAfterCreate()
        {
            NewImageDialog dialog = DialogOn("tiki100-200k");
            Assert.That(dialog.MountAfterCreate.IsEnabled, Is.True);
            dialog.UnformattedCheck.IsChecked = true;
            Pump();
            Assert.That(dialog.MountAfterCreate.IsEnabled, Is.False);
            Assert.That(dialog.MountAfterCreate.IsChecked, Is.False);
        }

        [AvaloniaTest]
        public void CreatedWithTheWindowsValues_AD64_CarriesTheDiskName()
        {
            NewImageDialog dialog = DialogOn("cbm-d64");
            dialog.SetOption("diskName", "MYDISK");
            Pump();
            string path = Path.Combine(_dir, "my.d64");
            BlankImageChoice d64 = dialog.SelectedDisk!;
            BlankImageCatalogue.Create(d64, dialog.OptionValues(), path);

            Assert.That(new FileInfo(path).Length, Is.EqualTo(d64.Layout.SizeBytes));
            using FileStream fs = File.OpenRead(path);
            IFileSystemDriver driver = d64.Provider.Mount(fs, readOnly: true, d64.Layout.Id);
            try
            {
                Assert.That(driver.VolumeLabel, Does.Contain("MYDISK"));
            }
            finally
            {
                driver.Dispose();
            }
        }

        [AvaloniaTest]
        public void CreatedUnformatted_IsAllZerosOfTheRightSize()
        {
            BlankImageChoice tiki = Choice("tiki100-400k");
            string path = Path.Combine(_dir, "raw.dsk");
            BlankImageCatalogue.Create(tiki, new Dictionary<string, string>(), path, unformatted: true);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.EqualTo(tiki.Layout.SizeBytes));
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != 0)
                {
                    Assert.Fail("byte " + i + " is not zero");
                }
            }
        }

        [Test]
        public void WindowCode_NamesNoFormat()
        {
            // The window must be driven by the drivers' metadata, never by format names in its own
            // code. Every format name, layout id and extension the drivers give is looked for in
            // the window's source; any hit means someone hard-coded a format.
            string? dir = FindDesktopSourceFolder();
            if (dir == null)
            {
                Assert.Ignore("the RetroCommander.Desktop source folder was not found from the test binary");
                return;
            }

            string source = File.ReadAllText(Path.Combine(dir, "NewImageDialog.axaml.cs"))
                + File.ReadAllText(Path.Combine(dir, "NewImageDialog.axaml"));

            List<string> words = new List<string>();
            DriverFactory factory = DriverFactoryBuilder.CreateWithAllDrivers();
            string[] types = factory.GetRegisteredTypes();
            for (int i = 0; i < types.Length; i++)
            {
                words.Add("\"" + types[i] + "\"");
            }
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                words.Add("\"" + all[i].Layout.Id + "\"");
                words.Add("\"" + all[i].Layout.Extension + "\"");
                words.Add("\"" + all[i].Layout.Machine + "\"");
                for (int o = 0; o < all[i].Layout.Options.Count; o++)
                {
                    words.Add("\"" + all[i].Layout.Options[o].Id + "\"");
                }
            }

            for (int i = 0; i < words.Count; i++)
            {
                Assert.That(source, Does.Not.Contain(words[i]), "NewImageDialog hard-codes " + words[i]);
            }
        }

        private static BlankImageOption FindOption(BlankImageLayout layout, string id)
        {
            for (int i = 0; i < layout.Options.Count; i++)
            {
                if (layout.Options[i].Id == id)
                {
                    return layout.Options[i];
                }
            }
            throw new AssertionException(layout.Id + " has no option " + id);
        }

        private static string? FindDesktopSourceFolder()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "RetroCommander.Desktop", "NewImageDialog.axaml.cs");
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate);
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
