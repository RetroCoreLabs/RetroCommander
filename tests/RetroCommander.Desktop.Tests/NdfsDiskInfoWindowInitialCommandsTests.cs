//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Desktop.Tests -- Headless-driven tests for the "Initial Commands" tab of
// NdfsDiskInfoWindow. Construct the REAL window over a live NDFS mount (the repo's BIGDISK0-L.IMG
// SINTRAN L pack) and drive it the way a user would: read the editor, click Save, toggle read-only.
// These complement the pure API tests in RetroIO.Tests/NdfsInitialCommandsTests.cs by proving the
// tab wiring (load into TextBox, Save round-trips to disk, read-only disables editing) actually works.
//

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.BlockDevices;
using RetroIO.Storage.Ndfs.BlockDevices;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class NdfsDiskInfoWindowInitialCommandsTests
    {
        // The stock SINTRAN L07 initial commands present on BIGDISK0-L.IMG.
        private static readonly string[] StockCommands =
        {
            "ENTER-DIR,,DI-75-1,0", "ENTER-DIR,,DI-74-1,0", "SET-AVAIL"
        };

        /// <summary>Lets Avalonia deliver queued property-change/event work (the window is never shown).</summary>
        private static void Pump() => Dispatcher.UIThread.RunJobs();

        /// <summary>Raises the Click routed event exactly as a real button press would.</summary>
        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        /// <summary>
        /// Walks up from the test's base directory to find the repo's real BIGDISK0-L.IMG fixture.
        /// Returns null when it cannot be located (so the test is ignored rather than failing on CI).
        /// </summary>
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

        private static FileSystemMetadataService MetadataService() =>
            new FileSystemMetadataService(new DriverFactory());

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
        public void InitialCommandsTab_LoadsCommandsFromRealPack()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var window = new NdfsDiskInfoWindow(fs, MetadataService(), image!, isReadOnly: true);
                Pump();

                // The editor is populated one command per line; the unavailable panel is hidden.
                Assert.That(window.InitCmdUnavailablePanel.IsVisible, Is.False);
                Assert.That(window.InitCmdEditor.IsVisible, Is.True);

                string[] lines = (window.InitCmdEditor.Text ?? string.Empty)
                    .Replace("\r\n", "\n").Split('\n');
                Assert.That(lines, Is.EqualTo(StockCommands));

                // Provenance line names SEGFIL0.
                Assert.That(window.InitCmdInfoText.Text, Does.Contain("SEGFIL0"));
            }
        }

        [AvaloniaTest]
        public void InitialCommandsTab_ReadOnlyMount_EditingDisabled()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var window = new NdfsDiskInfoWindow(fs, MetadataService(), image!, isReadOnly: true);
                Pump();

                Assert.That(window.InitCmdEditor.IsReadOnly, Is.True, "editor must be read-only on a read-only mount");
                Assert.That(window.InitCmdSaveButton.IsEnabled, Is.False, "Save must be disabled on a read-only mount");
                Assert.That(window.InitCmdStatusText.Text, Does.Contain("Read-only"));
            }
        }

        [AvaloniaTest]
        public void InitialCommandsTab_Save_WritesToDiskAndReloads()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            // Work on a throwaway copy so the fixture image is never modified.
            string temp = Path.Combine(Path.GetTempPath(), "rc_initcmd_" + Guid.NewGuid().ToString("N") + ".img");
            File.Copy(image!, temp, true);
            try
            {
                var fs = Mount(temp, readOnly: false, out var device, out var stream);
                using (device)
                using (stream)
                {
                    var window = new NdfsDiskInfoWindow(fs, MetadataService(), temp, isReadOnly: false);
                    Pump();

                    // Save must be enabled on a read-write mount.
                    Assert.That(window.InitCmdSaveButton.IsEnabled, Is.True);

                    // Edit: keep the three stock commands and add a fourth.
                    window.InitCmdEditor.Text =
                        "ENTER-DIR,,DI-75-1,0\nENTER-DIR,,DI-74-1,0\nSET-AVAIL\nCC OFFLINE WRITE OK";
                    Pump();

                    Click(window.InitCmdSaveButton);

                    // Status reports success and the editor was reloaded from disk with the 4 commands.
                    Assert.That(window.InitCmdStatusText.Text, Does.Contain("Saved 4"));
                    string[] lines = (window.InitCmdEditor.Text ?? string.Empty)
                        .Replace("\r\n", "\n").Split('\n');
                    Assert.That(lines, Is.EqualTo(new[]
                    {
                        "ENTER-DIR,,DI-75-1,0", "ENTER-DIR,,DI-74-1,0", "SET-AVAIL", "CC OFFLINE WRITE OK"
                    }));
                }

                // Re-open from scratch and confirm the write persisted to disk.
                var fs2 = Mount(temp, readOnly: true, out var device2, out var stream2);
                using (device2)
                using (stream2)
                {
                    var buffer = fs2.ReadSintranInitialCommands();
                    Assert.That(buffer, Is.Not.Null);
                    Assert.That(buffer!.Commands, Is.EqualTo(new[]
                    {
                        "ENTER-DIR,,DI-75-1,0", "ENTER-DIR,,DI-74-1,0", "SET-AVAIL", "CC OFFLINE WRITE OK"
                    }));
                    Assert.That(buffer.TextLength, Is.EqualTo(74)); // live-verified value for these 4 commands
                }
            }
            finally
            {
                try { File.Delete(temp); } catch { /* best-effort cleanup */ }
            }
        }

        [AvaloniaTest]
        public void InitialCommandsTab_Save_RejectsEmbeddedQuote()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            string temp = Path.Combine(Path.GetTempPath(), "rc_initcmd_" + Guid.NewGuid().ToString("N") + ".img");
            File.Copy(image!, temp, true);
            try
            {
                var fs = Mount(temp, readOnly: false, out var device, out var stream);
                using (device)
                using (stream)
                {
                    var window = new NdfsDiskInfoWindow(fs, MetadataService(), temp, isReadOnly: false);
                    Pump();

                    // A single quote is illegal inside a command — the save must be rejected cleanly.
                    window.InitCmdEditor.Text = "CC BAD'CMD";
                    Pump();

                    Click(window.InitCmdSaveButton);

                    Assert.That(window.InitCmdStatusText.Text, Does.Contain("Invalid command"));
                }
            }
            finally
            {
                try { File.Delete(temp); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
