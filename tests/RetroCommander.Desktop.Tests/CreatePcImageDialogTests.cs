//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroCommander.Desktop.Tests -- Real, headless-driven tests for CreatePcImageDialog: constructs the
// actual Window/ComboBox/TextBlock instances (via Avalonia.Headless, no real display) and drives
// them the way a user would — selecting combo items and reading back visibility/preview text —
// rather than only asserting on the underlying PcImageFactory library.
//

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class CreatePcImageDialogTests
    {
        [AvaloniaTest]
        public void Default_ShowsFloppyPanel_HidesHddPanel()
        {
            var dialog = new CreatePcImageDialog();

            Assert.That(dialog.FloppySizePanel.IsVisible, Is.True);
            Assert.That(dialog.HddPresetPanel.IsVisible, Is.False);
        }

        [AvaloniaTest]
        public void Default_FloppySize_Is1_44M_AndPreviewShowsFat12()
        {
            var dialog = new CreatePcImageDialog();

            Assert.That((dialog.FloppySizeCombo.SelectedItem as ComboBoxItem)?.Tag, Is.EqualTo("Size1_44M"));
            Assert.That(dialog.PreviewFileSystem.Text, Is.EqualTo("Filesystem: FAT12"));
            Assert.That(dialog.PreviewFileSize.Text, Does.Contain("1,474,560"));
        }

        [AvaloniaTest]
        public void SelectingHardDisk_TogglesPanelVisibility()
        {
            var dialog = new CreatePcImageDialog();

            SelectComboItemByTag(dialog.MediaTypeCombo, "HardDisk");

            Assert.That(dialog.FloppySizePanel.IsVisible, Is.False);
            Assert.That(dialog.HddPresetPanel.IsVisible, Is.True);
        }

        [AvaloniaTest]
        public void SelectingHardDisk_DefaultPreset_PreviewShowsXt10MbFat12()
        {
            var dialog = new CreatePcImageDialog();

            SelectComboItemByTag(dialog.MediaTypeCombo, "HardDisk");

            Assert.That((dialog.HddPresetCombo.SelectedItem as ComboBoxItem)?.Tag, Is.EqualTo("Xt10MB"));
            Assert.That(dialog.PreviewGeometry.Text, Does.Contain("306 cyl, 4 heads, 17 sect/trk"));
            Assert.That(dialog.PreviewFileSize.Text, Does.Contain("10,653,696"));
            Assert.That(dialog.PreviewFileSystem.Text, Does.Contain("FAT12"));
        }

        [AvaloniaTest]
        public void SelectingLargeHddPreset_PreviewSwitchesToFat16()
        {
            var dialog = new CreatePcImageDialog();

            SelectComboItemByTag(dialog.MediaTypeCombo, "HardDisk");
            SelectComboItemByTag(dialog.HddPresetCombo, "At62MB");

            Assert.That(dialog.PreviewGeometry.Text, Does.Contain("940 cyl, 8 heads, 17 sect/trk"));
            Assert.That(dialog.PreviewFileSystem.Text, Does.Contain("FAT16"));
        }

        [AvaloniaTest]
        public void SwitchingBackToFloppy_RestoresFloppyPanelAndPreview()
        {
            var dialog = new CreatePcImageDialog();

            SelectComboItemByTag(dialog.MediaTypeCombo, "HardDisk");
            SelectComboItemByTag(dialog.MediaTypeCombo, "Floppy");

            Assert.That(dialog.FloppySizePanel.IsVisible, Is.True);
            Assert.That(dialog.HddPresetPanel.IsVisible, Is.False);
            Assert.That(dialog.PreviewFileSystem.Text, Is.EqualTo("Filesystem: FAT12"));
        }

        [AvaloniaTest]
        public void SelectingEachFloppySize_UpdatesPreviewToMatchingByteCount()
        {
            var dialog = new CreatePcImageDialog();

            (string tag, string expectedBytes)[] cases =
            {
                ("Size360K", "368,640"),
                ("Size720K", "737,280"),
                ("Size1_2M", "1,228,800"),
                ("Size1_44M", "1,474,560"),
                ("Size2_88M", "2,949,120"),
            };

            for (int i = 0; i < cases.Length; i++)
            {
                SelectComboItemByTag(dialog.FloppySizeCombo, cases[i].tag);
                Assert.That(dialog.PreviewFileSize.Text, Does.Contain(cases[i].expectedBytes),
                    $"preview mismatch for floppy size tag '{cases[i].tag}'");
            }
        }

        [AvaloniaTest]
        public void VolumeLabelInput_AcceptsText()
        {
            var dialog = new CreatePcImageDialog();

            dialog.VolumeLabelInput.Text = "MYDISK";

            Assert.That(dialog.VolumeLabelInput.Text, Is.EqualTo("MYDISK"));
        }

        private static void SelectComboItemByTag(ComboBox combo, string tag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && (item.Tag as string) == tag)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            Assert.Fail($"No ComboBoxItem with Tag='{tag}' found");
        }
    }
}
