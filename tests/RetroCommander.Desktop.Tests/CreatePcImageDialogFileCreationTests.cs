//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Desktop.Tests -- Exercises the EXACT code path the real "Create Image" button
// invokes (CreatePcImageDialog.CreateImageFileAsync), writing a real file to disk and mounting it
// back through PcFloppyDriver — the same driver RetroCommander itself uses — rather than only
// testing PcImageFactory directly or the dialog's combo/preview logic in isolation.
//

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Drivers;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class CreatePcImageDialogFileCreationTests
    {
        private string _tempPath = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "pcimg_dlg_" + Path.GetRandomFileName() + ".img");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }
        }

        [AvaloniaTest]
        public async Task CreateImageFileAsync_DefaultFloppySelection_WritesRealMountableFile()
        {
            var dialog = new CreatePcImageDialog();

            await dialog.CreateImageFileAsync(_tempPath);

            Assert.That(File.Exists(_tempPath), Is.True);
            Assert.That(new FileInfo(_tempPath).Length, Is.EqualTo(1474560), "default selection is 1.44MB");

            using var stream = File.OpenRead(_tempPath);
            var driver = new PcFloppyDriver();
            driver.Mount(stream, readOnly: true);

            Assert.That(driver.FileSystemType, Is.EqualTo("FAT12"));
            Assert.That(driver.ListDirectory("/"), Is.Empty);
        }

        [AvaloniaTest]
        public async Task CreateImageFileAsync_VolumeLabel_IsWrittenIntoRootDirectory()
        {
            var dialog = new CreatePcImageDialog();
            dialog.VolumeLabelInput.Text = "MYDISK";

            await dialog.CreateImageFileAsync(_tempPath);

            using var stream = File.OpenRead(_tempPath);
            var driver = new PcFloppyDriver();
            driver.Mount(stream, readOnly: true);

            Assert.That(driver.VolumeLabel, Does.StartWith("MYDISK"));
        }

        [AvaloniaTest]
        public async Task CreateImageFileAsync_HardDiskSelection_WritesRealFileWithMountablePartition()
        {
            var dialog = new CreatePcImageDialog();
            SelectComboItemByTag(dialog, dialog.MediaTypeCombo, "HardDisk");
            SelectComboItemByTag(dialog, dialog.HddPresetCombo, "Xt10MB");

            await dialog.CreateImageFileAsync(_tempPath);

            Assert.That(File.Exists(_tempPath), Is.True);
            Assert.That(new FileInfo(_tempPath).Length, Is.EqualTo(306L * 4 * 17 * 512));

            using var stream = File.OpenRead(_tempPath);
            var driver = new PcFloppyDriver();
            Assert.That(((IPartitionProvider)driver).SelectPartition("0"), Is.True);
            driver.Mount(stream, readOnly: true);

            Assert.That(driver.FileSystemType, Is.EqualTo("FAT12"));
            Assert.That(driver.ListDirectory("/"), Is.Empty);
        }

        [AvaloniaTest]
        public async Task CreateImageFileAsync_LargeHardDiskSelection_ProducesFat16Partition()
        {
            var dialog = new CreatePcImageDialog();
            SelectComboItemByTag(dialog, dialog.MediaTypeCombo, "HardDisk");
            SelectComboItemByTag(dialog, dialog.HddPresetCombo, "At62MB");

            await dialog.CreateImageFileAsync(_tempPath);

            using var stream = File.OpenRead(_tempPath);
            var driver = new PcFloppyDriver();
            ((IPartitionProvider)driver).SelectPartition("0");
            driver.Mount(stream, readOnly: true);

            Assert.That(driver.FileSystemType, Is.EqualTo("FAT16"));
        }

        [AvaloniaTest]
        public async Task CreateImageFileAsync_ThenWriteAndReadFileBack_RoundTrips()
        {
            // Full loop: dialog creates the file, then a second open (read-write, as
            // RetroCommander would after mounting the newly-created image) writes a real file
            // into it and reads it back — proves the created image isn't just structurally valid
            // but actually usable.
            var dialog = new CreatePcImageDialog();
            await dialog.CreateImageFileAsync(_tempPath);

            byte[] data = { 10, 20, 30, 40, 50 };
            using (var stream = new FileStream(_tempPath, FileMode.Open, FileAccess.ReadWrite))
            {
                var driver = new PcFloppyDriver();
                driver.Mount(stream, readOnly: false);
                driver.WriteFile("TEST.TXT", data);

                // PcFloppyDriver/GenericBlockDevice buffer writes in memory and only flush to the
                // underlying stream on Dispose() — confirmed the hard way: omitting this line
                // makes the write silently vanish (the reopened file below wouldn't contain it),
                // even though WriteFile() itself completed without error. Real behavior, not a
                // test artifact — RetroCommander's panel Dispose() path relies on the same thing.
                driver.Dispose();
            }

            using (var stream = File.OpenRead(_tempPath))
            {
                var driver = new PcFloppyDriver();
                driver.Mount(stream, readOnly: true);
                Assert.That(driver.ReadFile("TEST.TXT"), Is.EqualTo(data));
            }
        }

        private static void SelectComboItemByTag(CreatePcImageDialog dialog, Avalonia.Controls.ComboBox combo, string tag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is Avalonia.Controls.ComboBoxItem item && (item.Tag as string) == tag)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            Assert.Fail($"No ComboBoxItem with Tag='{tag}' found");
        }
    }
}
