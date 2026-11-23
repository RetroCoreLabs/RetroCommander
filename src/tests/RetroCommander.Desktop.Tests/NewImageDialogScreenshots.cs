//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- pictures of File > New Image, drawn off-screen by the headless
// Skia renderer (nothing is shown on the desktop). Explicit: run it by name with
// RC_SCREENSHOT_DIR set to the folder the PNG files should go in.

using System;
using System.IO;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using RetroCommander.Core.Services;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    [Explicit("writes PNG pictures of the window; run by name with RC_SCREENSHOT_DIR set")]
    public class NewImageDialogScreenshots
    {
        [AvaloniaTest]
        public void Capture()
        {
            string? dir = Environment.GetEnvironmentVariable("RC_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Ignore("RC_SCREENSHOT_DIR is not set");
                return;
            }
            Directory.CreateDirectory(dir);

            Shot(dir, "1-machines", "cbm-d64", null);
            Shot(dir, "2-d64-bad-disk-id", "cbm-d64", d => d.SetOption("diskId", "A"));
            Shot(dir, "3-sintran-disc-75mb", "ndfs-sintran-disc-75mb", null);
            Shot(dir, "4-ndfs-custom", "ndfs-custom", null);
            Shot(dir, "5-pc-hard-disk", "pc-hd-at-20m", null);
            Shot(dir, "6-unformatted-tiki100", "tiki100-400k", d => d.UnformattedCheck.IsChecked = true);
        }

        private delegate void Adjust(NewImageDialog dialog);

        private static void Shot(string dir, string name, string layoutId, Adjust? adjust)
        {
            BlankImageChoice choice = Find(layoutId);
            NewImageDialog dialog = new NewImageDialog();
            dialog.Show();
            dialog.SelectMachine(choice.Layout.Machine);
            Dispatcher.UIThread.RunJobs();
            dialog.SelectDisk(layoutId);
            dialog.OutputPath.Text = Path.Combine(Path.GetTempPath(), "example" + choice.Layout.Extension);
            Dispatcher.UIThread.RunJobs();
            adjust?.Invoke(dialog);
            Dispatcher.UIThread.RunJobs();

            // The headless renderer draws on its own timer; tick it so the capture shows the
            // window as it is now, not the frame from before the last change.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            using var frame = dialog.CaptureRenderedFrame();
            Assert.That(frame, Is.Not.Null);
            frame!.Save(Path.Combine(dir, name + ".png"));
            dialog.Close();
        }

        private static BlankImageChoice Find(string layoutId)
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
    }
}
