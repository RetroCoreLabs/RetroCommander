//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// Drives the "Convert disk image" dialog the way its Convert button does (LoadSource, then
// TryConvert) on images the test builds in its own temporary folder.

using System;
using System.IO;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Images;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class ConvertDiskImageDialogTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc_convert_" + Guid.NewGuid().ToString("N"));
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

        [AvaloniaTest]
        public void RawSource_ShowsShapeFromPartitionTable_AndWritesVhd()
        {
            string raw = Path.Combine(_dir, "DISK.img");
            File.WriteAllBytes(raw, PcImageFactory.CreateBlankHardDisk(PcHddPreset.At20MB));

            ConvertDiskImageDialog dialog = new ConvertDiskImageDialog(raw);
            Assert.That(dialog.GeometryPanel.IsVisible, Is.True);
            Assert.That(dialog.TargetInput.Text, Is.EqualTo(Path.Combine(_dir, "DISK.vhd")));
            DiskImageConverter.TryDetectGeometry(raw, out DiskGeometry expected);
            Assert.That(dialog.CylindersInput.Text, Is.EqualTo(expected.Cylinders.ToString()));
            Assert.That(dialog.HeadsInput.Text, Is.EqualTo(expected.Heads.ToString()));
            Assert.That(dialog.SectorsInput.Text, Is.EqualTo(expected.SectorsPerTrack.ToString()));

            // The user changes the shape; the changed shape is what goes into the footer.
            dialog.CylindersInput.Text = "600";
            Assert.That(dialog.TryConvert(), Is.True, dialog.ProblemText.Text);
            Assert.That(DiskImageConverter.IsFixedVhd(dialog.CreatedPath!), Is.True);
            Assert.That(DiskImageConverter.TryDetectGeometry(dialog.CreatedPath!, out DiskGeometry written), Is.True);
            Assert.That(written.Cylinders, Is.EqualTo(600));
        }

        [AvaloniaTest]
        public void VhdSource_HidesShape_AndWritesTheSameBytesBack()
        {
            string raw = Path.Combine(_dir, "DISK.hdd");
            File.WriteAllBytes(raw, PcImageFactory.CreateBlankHardDisk(PcHddPreset.At20MB));
            string vhd = Path.Combine(_dir, "DISK.vhd");
            DiskImageConverter.RawToFixedVhd(raw, vhd, null);

            ConvertDiskImageDialog dialog = new ConvertDiskImageDialog(vhd);
            Assert.That(dialog.GeometryPanel.IsVisible, Is.False);
            Assert.That(dialog.TargetInput.Text, Is.EqualTo(Path.Combine(_dir, "DISK.img")));
            Assert.That(dialog.TryConvert(), Is.True, dialog.ProblemText.Text);
            Assert.That(File.ReadAllBytes(dialog.CreatedPath!), Is.EqualTo(File.ReadAllBytes(raw)));
        }

        [AvaloniaTest]
        public void ExistingTarget_IsRefusedAndShown()
        {
            string raw = Path.Combine(_dir, "DISK.img");
            File.WriteAllBytes(raw, PcImageFactory.CreateBlankHardDisk(PcHddPreset.At20MB));
            File.WriteAllBytes(Path.Combine(_dir, "DISK.vhd"), new byte[] { 9 });

            ConvertDiskImageDialog dialog = new ConvertDiskImageDialog(raw);
            Assert.That(dialog.TryConvert(), Is.False);
            Assert.That(dialog.ProblemText.Text, Does.Contain("already exists"));
            Assert.That(File.ReadAllBytes(Path.Combine(_dir, "DISK.vhd")), Is.EqualTo(new byte[] { 9 }));
        }

        [AvaloniaTest]
        public void RawWithoutShape_LeavesFieldsEmpty_AndAsksForThem()
        {
            string blank = Path.Combine(_dir, "BLANK.img");
            File.WriteAllBytes(blank, new byte[512 * 64]);

            ConvertDiskImageDialog dialog = new ConvertDiskImageDialog(blank);
            Assert.That(dialog.GeometryPanel.IsVisible, Is.True);
            Assert.That(dialog.CylindersInput.Text, Is.Empty);
            Assert.That(dialog.TryConvert(), Is.False);
            Assert.That(dialog.ProblemText.Text, Does.Contain("Fill in"));

            dialog.CylindersInput.Text = "4";
            dialog.HeadsInput.Text = "1";
            dialog.SectorsInput.Text = "16";
            Assert.That(dialog.TryConvert(), Is.True, dialog.ProblemText.Text);
        }
    }
}
