//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroCommander.Desktop.Tests -- Headless-driven tests for the unified NewImageDialog: constructs the
// real Window and drives it the way a user would (selecting a format in the left list, changing
// combos, typing names) and asserts on panel visibility, live preview text, inline validation, and
// the actual bytes of the created image.
//
// These exist because a hand-click-through missed a real defect: the Create handler read TextBox /
// ComboBox values from INSIDE a Task.Run body, which throws "Call from invalid thread" — Avalonia
// controls may only be touched on the UI thread. CreateNdfs_OnBackgroundThread_DoesNotTouchUi is
// the regression guard for exactly that.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using RetroIO.Storage.Ndfs.BlockDevices;
using RetroIO.Storage.Ndfs.Creation;
using RetroIO.Storage.Ndfs.Structures;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class NewImageDialogTests
    {
        private static string TempPath(string ext) =>
            Path.Combine(Path.GetTempPath(), $"rc_newimg_{Guid.NewGuid():N}{ext}");

        /// <summary>
        /// Lets Avalonia deliver queued property-change/event work. TextBox.TextChanged is not
        /// necessarily raised synchronously when Text is assigned on a window that was never shown,
        /// so a test that sets Text and immediately asserts on the resulting validation state can
        /// read a stale value. Pumping mirrors what the real message loop does.
        /// </summary>
        private static void Pump() => Dispatcher.UIThread.RunJobs();

        /// <summary>Sets a TextBox exactly as typing would, then lets the change propagate.</summary>
        private static void Type(TextBox box, string text)
        {
            box.Text = text;
            Pump();
        }

        /// <summary>Selects a format in the left-hand catalogue by its Tag, as a click would.</summary>
        private static void SelectFormat(NewImageDialog dialog, string tag)
        {
            ListBox[] lists = { dialog.NdfsList, dialog.PcList, dialog.CbmList };

            for (int l = 0; l < lists.Length; l++)
            {
                var list = lists[l];
                for (int i = 0; i < list.ItemCount; i++)
                {
                    if (list.Items[i] is ListBoxItem item &&
                        string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
                    {
                        list.SelectedItem = item;
                        Pump();
                        return;
                    }
                }
            }

            Assert.Fail($"format '{tag}' not found in the catalogue");
        }

        // ================= format switching =================

        [AvaloniaTest]
        public void Default_IsNdfs_AndOnlyNdfsPanelVisible()
        {
            var dialog = new NewImageDialog();

            Assert.That(dialog.NdfsPanel.IsVisible, Is.True);
            Assert.That(dialog.PcPanel.IsVisible, Is.False);
            Assert.That(dialog.CbmPanel.IsVisible, Is.False);
            Assert.That(dialog.FormatTitle.Text, Does.Contain("NDFS"));
        }

        [AvaloniaTest]
        public void SelectingPc_SwapsToPcPanel()
        {
            var dialog = new NewImageDialog();
            SelectFormat(dialog, "PC");

            Assert.That(dialog.PcPanel.IsVisible, Is.True);
            Assert.That(dialog.NdfsPanel.IsVisible, Is.False);
            Assert.That(dialog.CbmPanel.IsVisible, Is.False);
            Assert.That(dialog.FormatTitle.Text, Does.Contain("PC"));
        }

        [AvaloniaTest]
        public void SelectingD64_SwapsToCommodorePanel()
        {
            var dialog = new NewImageDialog();
            SelectFormat(dialog, "D64");

            Assert.That(dialog.CbmPanel.IsVisible, Is.True);
            Assert.That(dialog.NdfsPanel.IsVisible, Is.False);
            Assert.That(dialog.PreviewText.Text, Does.Contain("1541"));
        }

        [AvaloniaTest]
        public void SelectingAFormat_ClearsSelectionInTheOtherLists()
        {
            // The three grouped lists must behave as ONE logical selection.
            var dialog = new NewImageDialog();
            SelectFormat(dialog, "D81");

            Assert.That(dialog.NdfsList.SelectedItem, Is.Null);
            Assert.That(dialog.PcList.SelectedItem, Is.Null);
            Assert.That(dialog.CbmList.SelectedItem, Is.Not.Null);
        }

        // ================= NDFS preview shows real drive geometry =================

        [AvaloniaTest]
        public void NdfsPreview_Smd75MB_ShowsDeviceCapacityAndSpare()
        {
            var dialog = new NewImageDialog();
            dialog.NdfsTemplateCombo.SelectedIndex = 2;
            Pump(); // SMD 75MB
            Type(dialog.OutputPath, TempPath(".img"));

            // Real measured geometry: 38,400 device pages, 36,945 usable, 1,455 spare.
            Assert.That(dialog.PreviewText.Text, Does.Contain("38,400"));
            Assert.That(dialog.PreviewText.Text, Does.Contain("36,945"));
            Assert.That(dialog.PreviewText.Text, Does.Contain("1,455"));
            Assert.That(dialog.PreviewText.Text, Does.Contain("bad-sector"));
        }

        [AvaloniaTest]
        public void NdfsPreview_Winchester_ShowsMicropolis1325Geometry()
        {
            var dialog = new NewImageDialog();
            dialog.NdfsTemplateCombo.SelectedIndex = 3;
            Pump(); // Winchester 74MB
            Type(dialog.OutputPath, TempPath(".img"));

            // Micropolis 1325: 36,864 device pages, 36,396 usable, 468 spare.
            Assert.That(dialog.PreviewText.Text, Does.Contain("36,864"));
            Assert.That(dialog.PreviewText.Text, Does.Contain("36,396"));
            Assert.That(dialog.PreviewText.Text, Does.Contain("468"));
        }

        [AvaloniaTest]
        public void SelectingCustomTemplate_RevealsThePageCountBox()
        {
            var dialog = new NewImageDialog();
            Assert.That(dialog.NdfsCustomPages.IsVisible, Is.False);

            dialog.NdfsTemplateCombo.SelectedIndex = 5;
            Pump(); // Custom
            Assert.That(dialog.NdfsCustomPages.IsVisible, Is.True);
        }

        // ================= inline validation =================

        [AvaloniaTest]
        public void Create_IsDisabled_UntilAnOutputPathIsChosen()
        {
            var dialog = new NewImageDialog();

            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.Text, Does.Contain("output file"));

            Type(dialog.OutputPath, TempPath(".img"));
            Assert.That(dialog.CreateButton.IsEnabled, Is.True);
        }

        [AvaloniaTest]
        public void NdfsName_LongerThan16Chars_IsRejectedInline()
        {
            var dialog = new NewImageDialog();
            Type(dialog.OutputPath, TempPath(".img"));
            Type(dialog.NdfsName, "THIS-NAME-IS-FAR-TOO-LONG");

            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.IsVisible, Is.True);
            Assert.That(dialog.ValidationText.Text, Does.Contain("16"));
        }

        [AvaloniaTest]
        public void NdfsName_NonPrintableAscii_IsRejectedInline()
        {
            var dialog = new NewImageDialog();
            Type(dialog.OutputPath, TempPath(".img"));
            Type(dialog.NdfsName, "BAD\tTAB");

            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.Text, Does.Contain("printable"));
        }

        [AvaloniaTest]
        public void CommodoreDiskId_MustBeExactlyTwoChars()
        {
            var dialog = new NewImageDialog();
            SelectFormat(dialog, "D64");
            Type(dialog.OutputPath, TempPath(".d64"));

            Type(dialog.CbmId, "X");
            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.Text, Does.Contain("2 characters"));

            Type(dialog.CbmId, "01");
            Assert.That(dialog.CreateButton.IsEnabled, Is.True);
        }

        [AvaloniaTest]
        public void PcVolumeLabel_LongerThan11Chars_IsRejectedInline()
        {
            var dialog = new NewImageDialog();
            SelectFormat(dialog, "PC");
            Type(dialog.OutputPath, TempPath(".img"));
            Type(dialog.PcLabel, "WAY-TOO-LONG-LABEL");

            Assert.That(dialog.CreateButton.IsEnabled, Is.False);
            Assert.That(dialog.ValidationText.Text, Does.Contain("11"));
        }

        // ================= blank-image option =================

        [AvaloniaTest]
        public void TickingBlankImage_DisablesAllMetadataFields()
        {
            var dialog = new NewImageDialog();
            Type(dialog.OutputPath, TempPath(".img"));

            dialog.NdfsBlankCheck.IsChecked = true;
            Pump();

            Assert.That(dialog.NdfsName.IsEnabled, Is.False, "a blank image has no directory name");
            Assert.That(dialog.NdfsBootCombo.IsEnabled, Is.False, "a blank image has no boot sector");
            Assert.That(dialog.NdfsExtInfoCheck.IsEnabled, Is.False);
            Assert.That(dialog.NdfsBlankNote.IsVisible, Is.True);
            Assert.That(dialog.PreviewText.Text, Does.Contain("0x00"));
        }

        // ================= the threading regression =================

        [AvaloniaTest]
        public void CreateNdfs_OnBackgroundThread_DoesNotTouchUi()
        {
            // REGRESSION GUARD for "Create failed: Call from invalid thread".
            // The Create handler snapshots the controls on the UI thread into a plain request, then
            // runs the (possibly slow, 100+ MB) file write on a background thread. If Execute() ever
            // reaches back into a control again, this test throws exactly as the app did.
            var dialog = new NewImageDialog();
            string path = TempPath(".img");
            Type(dialog.OutputPath, path);
            Type(dialog.NdfsName, "THREADED");

            try
            {
                // Built on the UI thread...
                var request = dialog.BuildRequest(path);

                // ...executed OFF it, exactly as OnCreateClick does.
                //
                // Deliberately NOT Assert.DoesNotThrowAsync(async () => await Task.Run(...)):
                // an [AvaloniaTest] body runs on the headless dispatcher thread, whose
                // SynchronizationContext is installed. DoesNotThrowAsync blocks that thread
                // waiting for the lambda's task, while the `await` schedules its continuation
                // back onto the very same (blocked) thread -- the test host then deadlocks and
                // never exits, hanging the whole run. Join() on a plain worker thread has no
                // continuation to post, so it cannot deadlock.
                Exception? backgroundFailure = null;
                Thread worker = new Thread(() =>
                {
                    try
                    {
                        NewImageDialog.Execute(request);
                    }
                    catch (Exception ex)
                    {
                        backgroundFailure = ex;
                    }
                });
                worker.IsBackground = true;
                worker.Start();

                Assert.That(worker.Join(TimeSpan.FromMinutes(2)), Is.True,
                    "image creation did not finish within two minutes");
                Assert.That(backgroundFailure, Is.Null,
                    "image creation must not touch Avalonia controls from a background thread");

                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // ================= end-to-end: the dialog really creates valid images =================

        [AvaloniaTest]
        public void CreatesNdfsImage_ThatIsAValidMountableVolume()
        {
            var dialog = new NewImageDialog();
            string path = TempPath(".img");

            dialog.NdfsTemplateCombo.SelectedIndex = 2;
            Pump();    // SMD 75MB
            Type(dialog.NdfsName, "UITEST");
            Type(dialog.OutputPath, path);

            try
            {
                NewImageDialog.Execute(dialog.BuildRequest(path));

                // Real SMD device geometry.
                Assert.That(new FileInfo(path).Length,
                    Is.EqualTo(38400L * NdfsBlockDeviceDescriptor.NDFS_PAGE_SIZE));

                byte[] page = new byte[NdfsBlockDeviceDescriptor.NDFS_PAGE_SIZE];
                using (FileStream fs = File.OpenRead(path))
                {
                    fs.ReadExactly(page, 0, page.Length);
                }

                MasterBlock mb = MasterBlock.FromBytes(page);
                Assert.That(mb.DirectoryName, Is.EqualTo("UITEST"));
                Assert.That(mb.ExtValid, Is.True);
                Assert.That(mb.ChecksumState, Is.EqualTo(ChecksumValidationState.CrcValid));
                Assert.That(mb.ExtPagesAvailable, Is.EqualTo(36945u));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [AvaloniaTest]
        public void CreatesBlankNdfsImage_ThatIsEntirelyZero()
        {
            var dialog = new NewImageDialog();
            string path = TempPath(".img");

            dialog.NdfsTemplateCombo.SelectedIndex = 0;
            Pump();   // 154-page floppy (small, fast to scan)
            dialog.NdfsBlankCheck.IsChecked = true;
            Pump();
            Type(dialog.OutputPath, path);

            try
            {
                NewImageDialog.Execute(dialog.BuildRequest(path));

                byte[] data = File.ReadAllBytes(path);
                Assert.That(data.Length, Is.EqualTo(154L * NdfsBlockDeviceDescriptor.NDFS_PAGE_SIZE));

                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] != 0)
                        Assert.Fail($"blank image must be all 0x00, but byte {i} = 0x{data[i]:X2}");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [AvaloniaTest]
        public void CreatesCommodoreD64_WithCorrectSize()
        {
            var dialog = new NewImageDialog();
            string path = TempPath(".d64");

            SelectFormat(dialog, "D64");
            Type(dialog.CbmName, "UITEST");
            Type(dialog.CbmId, "01");
            Type(dialog.OutputPath, path);

            try
            {
                NewImageDialog.Execute(dialog.BuildRequest(path));

                // D64 = 1541, 35 tracks, 174,848 bytes.
                Assert.That(new FileInfo(path).Length, Is.EqualTo(174848L));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [AvaloniaTest]
        public void CreatesPcFloppy_WithCorrectSize()
        {
            var dialog = new NewImageDialog();
            string path = TempPath(".img");

            SelectFormat(dialog, "PC");
            Type(dialog.PcLabel, "UITEST");
            Type(dialog.OutputPath, path);

            try
            {
                NewImageDialog.Execute(dialog.BuildRequest(path));

                // Default is the 1.44 MB 3.5" HD floppy.
                Assert.That(new FileInfo(path).Length, Is.EqualTo(1474560L));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
