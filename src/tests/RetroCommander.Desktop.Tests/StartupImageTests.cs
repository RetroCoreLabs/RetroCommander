//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- the real MainWindow, started the way Windows starts it for a
// linked disk image (the image path as the first argument, see FileAssociationWindowsTests), must
// mount the image in the left panel and show the image's folder in the right panel.
//
// The window logs to a file in the test's own temporary folder, never to the user's real log.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using RetroIO.Storage.Local;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class StartupImageTests
    {
        private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(30);

        [AvaloniaTest]
        public void StartupImage_MountsLeft_FolderRight()
        {
            // A folder and file name with spaces, as Explorer passes them.
            string folder = Path.Combine(Path.GetTempPath(), "RetroCommander StartupImageTests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string image = Path.Combine(folder, "Game Disk 1.d64");
            MainWindow? window = null;
            try
            {
                CbmImageFactory.CreateBlankFile(CbmDiskType.D64, image, "TEST", "01");

                window = new MainWindow(Path.Combine(folder, "test.log")) { StartupImagePath = image };
                window.Show();

                string expectedStatus = "✓ Mounted Game Disk 1.d64 on left panel";
                bool done = PollUntil(() => window.StatusText.Text == expectedStatus || (window.StatusText.Text ?? "").StartsWith("✗"));

                Assert.That(done, Is.True, "startup mount did not finish; status: " + window.StatusText.Text);
                Assert.That(window.StatusText.Text, Is.EqualTo(expectedStatus));
                Assert.That(window.RightPanelPath.Text?.TrimEnd('\\', '/'), Is.EqualTo(folder).IgnoreCase);
                Assert.That(window.LeftPanelPath.Text?.TrimEnd('\\', '/'), Is.Not.EqualTo(folder).IgnoreCase,
                    "the left panel must show the mounted image, not the folder");
            }
            finally
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                // The window's logger may still hold its log file open; retry briefly, then give up
                // loudly rather than leave the folder silently.
                DeleteFolderWithRetry(folder);
            }
        }

        // Runs the UI thread's queued work until the condition holds or the deadline passes.
        // This is a poll on the asserted state, not a fixed sleep.
        private static bool PollUntil(Func<bool> condition)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed < s_deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return condition();
        }

        private static void DeleteFolderWithRetry(string folder)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                    return;
                }
                catch (IOException) when (clock.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (clock.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(50);
                }
            }
        }
    }
}
