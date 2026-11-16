//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- a real MainWindow for headless tests, with a blank D64 image in a
// temporary folder whose name has spaces (as Explorer passes them). The window logs to a file in
// that folder, never to the user's real RetroCommander log, and everything is removed on Dispose.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Threading;
using NUnit.Framework;
using RetroIO.Storage.Local;

namespace RetroCommander.Desktop.Tests
{
    internal sealed class MainWindowTestHost : IDisposable
    {
        private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Condition polled on the UI thread.
        /// </summary>
        /// <returns>
        /// True once the awaited state is reached
        /// </returns>
        public delegate bool UiCondition();

        /// <summary>
        /// Creates the folder and the image, and the window (not shown yet).
        /// </summary>
        /// <param name="startupImage">
        /// True to give the window the image as its command-line startup image
        /// </param>
        public MainWindowTestHost(bool startupImage)
        {
            Folder = Path.Combine(Path.GetTempPath(), "RetroCommander StartupImageTests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
            ImagePath = Path.Combine(Folder, ImageName);
            CbmImageFactory.CreateBlankFile(CbmDiskType.D64, ImagePath, "TEST", "01");

            Window = new MainWindow(Path.Combine(Folder, "test.log"));
            if (startupImage)
            {
                Window.StartupImagePath = ImagePath;
            }
        }

        /// <summary>
        /// Gets the file name of the test image.
        /// </summary>
        public static string ImageName => "Game Disk 1.d64";

        /// <summary>
        /// Gets the temporary folder holding the image.
        /// </summary>
        public string Folder { get; }

        /// <summary>
        /// Gets the full path of the test image.
        /// </summary>
        public string ImagePath { get; }

        /// <summary>
        /// Gets the window under test.
        /// </summary>
        public MainWindow Window { get; }

        /// <summary>
        /// Gets the status line text.
        /// </summary>
        public string Status => Window.StatusText.Text ?? string.Empty;

        /// <summary>
        /// Runs the UI thread's queued work until the condition holds or 30 seconds pass. This is a
        /// poll on the asserted state, not a fixed sleep.
        /// </summary>
        /// <param name="condition">
        /// The state to wait for
        /// </param>
        /// <returns>
        /// True when the condition was reached
        /// </returns>
        public static bool PollUntil(UiCondition condition)
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

        /// <summary>
        /// Waits until the status line shows <paramref name="expected"/>, or fails the test with the
        /// status it shows instead (a mount failure starts with a cross).
        /// </summary>
        /// <param name="expected">
        /// The exact status text
        /// </param>
        public void WaitForStatus(string expected)
        {
            bool reached = PollUntil(() => Status == expected || Status.StartsWith("✗"));
            Assert.That(reached, Is.True, "timed out; status: " + Status);
            Assert.That(Status, Is.EqualTo(expected));
        }

        /// <summary>
        /// Closes the window (which unmounts the image and closes its file) and deletes the folder.
        /// </summary>
        public void Dispose()
        {
            Window.Close();
            Dispatcher.UIThread.RunJobs();

            // The log file can still be closing; retry briefly, then fail loudly rather than leave
            // the folder behind silently.
            Stopwatch clock = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    Directory.Delete(Folder, recursive: true);
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
