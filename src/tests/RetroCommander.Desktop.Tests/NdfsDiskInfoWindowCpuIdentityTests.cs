//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- Headless-driven tests for the "CPU Identity" tab of
// NdfsDiskInfoWindow. Construct the REAL window over a live NDFS mount (the repo's BIGDISK0-L.IMG
// SINTRAN L pack) and drive it the way a user would: read the values, type a new number, click
// Apply, toggle read-only. The pure API is covered in RetroIO.Tests/NdfsCpuIdentityTests.cs; these
// prove the tab wiring. Every write happens on a throwaway COPY of the fixture image.

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
    public class NdfsDiskInfoWindowCpuIdentityTests
    {
        private static void Pump() => Dispatcher.UIThread.RunJobs();

        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        /// <summary>
        /// Walks up from the test's base directory to find the repo's real BIGDISK0-L.IMG fixture.
        /// </summary>
        private static string? ResolveRealImage()
        {
            string rel = Path.Combine("demo", "test-images", "ndfs", "BIGDISK0-L.IMG");
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
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
        public void CpuIdentityTab_ShowsBothCopiesFromRealPack()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var window = new NdfsDiskInfoWindow(fs, MetadataService(), image!, isReadOnly: true);
                Pump();

                Assert.That(window.CpuIdUnavailablePanel.IsVisible, Is.False);
                Assert.That(window.CpuIdPanel.IsVisible, Is.True);

                // What the library reads is what the tab shows, for both copies.
                var id = fs.ReadSintranCpuIdentity();
                Assert.That(window.CpuIdImageSysnoText.Text, Is.EqualTo(id.Copies[0].Sysno.ToString()));
                Assert.That(window.CpuIdSaveSysnoText.Text, Is.EqualTo(id.Copies[1].Sysno.ToString()));
                Assert.That(window.CpuIdImageTypeText.Text, Is.EqualTo(id.Copies[0].CpuType.ToString()));
                Assert.That(window.CpuIdSaveWhereText.Text, Does.Contain("S3SDPIT").Or.Contain("segment 53"));
                Assert.That(window.CpuIdInfoText.Text, Does.Contain("SINTRAN L"));
                Assert.That(window.CpuIdInfoText.Text, Does.Contain("RTFIL"));
            }
        }

        [AvaloniaTest]
        public void CpuIdentityTab_ReadOnlyMount_ChangeDisabled()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            var fs = Mount(image!, readOnly: true, out var device, out var stream);
            using (device)
            using (stream)
            {
                var window = new NdfsDiskInfoWindow(fs, MetadataService(), image!, isReadOnly: true);
                Pump();

                Assert.That(window.CpuIdApplyButton.IsEnabled, Is.False, "Apply must be disabled on a read-only mount");
                Assert.That(window.CpuIdNumberBox.IsEnabled, Is.False);
                Assert.That(window.CpuIdStatusText.Text, Does.Contain("Read-only"));
            }
        }

        [AvaloniaTest]
        public void CpuIdentityTab_Apply_WritesBothCopiesAndClearsStamp()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            // Work on a throwaway copy so the fixture image is never modified.
            string temp = Path.Combine(Path.GetTempPath(), "rc_cpuid_" + Guid.NewGuid().ToString("N") + ".img");
            File.Copy(image!, temp, true);
            try
            {
                var fs = Mount(temp, readOnly: false, out var device, out var stream);
                using (device)
                using (stream)
                {
                    var window = new NdfsDiskInfoWindow(fs, MetadataService(), temp, isReadOnly: false);
                    Pump();

                    Assert.That(window.CpuIdApplyButton.IsEnabled, Is.True);

                    window.CpuIdNumberBox.Text = "21238";
                    window.CpuIdTypeBox.Text = "9883";
                    window.CpuIdKeepStampBox.IsChecked = false;
                    Pump();

                    Click(window.CpuIdApplyButton);

                    Assert.That(window.CpuIdStatusText.Text, Does.Contain("CPU number 21238"));
                    Assert.That(window.CpuIdStatusText.Text, Does.Contain("stamp cleared"));
                    Assert.That(window.CpuIdImageSysnoText.Text, Is.EqualTo("21238"));
                    Assert.That(window.CpuIdSaveFcpunText.Text, Is.EqualTo("21238"));
                    Assert.That(window.CpuIdSaveTypeText.Text, Is.EqualTo("9883"));
                    Assert.That(window.CpuIdStampText.Text, Does.StartWith("not entered"));
                }

                // Re-open from scratch and confirm the write persisted to disk.
                var fs2 = Mount(temp, readOnly: true, out var device2, out var stream2);
                using (device2)
                using (stream2)
                {
                    var id = fs2.ReadSintranCpuIdentity();
                    Assert.That(id.Copies[0].Sysno, Is.EqualTo(21238));
                    Assert.That(id.Copies[0].Fcpun, Is.EqualTo(21238));
                    Assert.That(id.Copies[1].Sysno, Is.EqualTo(21238));
                    Assert.That(id.Copies[1].CpuType, Is.EqualTo(9883));
                    Assert.That(id.DirectoryEntered, Is.False);
                    Assert.That(id.DirectorySystemNumber, Is.EqualTo(0));
                }
            }
            finally
            {
                try { File.Delete(temp); } catch { /* best-effort cleanup */ }
            }
        }

        [AvaloniaTest]
        public void CpuIdentityTab_Apply_RejectsBadNumberWithoutWriting()
        {
            string? image = ResolveRealImage();
            if (image == null) Assert.Ignore("Real NDFS disk image BIGDISK0-L.IMG not found");

            string temp = Path.Combine(Path.GetTempPath(), "rc_cpuid_" + Guid.NewGuid().ToString("N") + ".img");
            File.Copy(image!, temp, true);
            try
            {
                var fs = Mount(temp, readOnly: false, out var device, out var stream);
                using (device)
                using (stream)
                {
                    var window = new NdfsDiskInfoWindow(fs, MetadataService(), temp, isReadOnly: false);
                    Pump();
                    string before = window.CpuIdImageSysnoText.Text ?? string.Empty;

                    window.CpuIdNumberBox.Text = "70000";
                    Pump();
                    Click(window.CpuIdApplyButton);

                    Assert.That(window.CpuIdStatusText.Text, Does.Contain("0..65535"));
                    Assert.That(window.CpuIdImageSysnoText.Text, Is.EqualTo(before));
                    Assert.That(fs.ReadSintranCpuIdentity().DirectoryEntered, Is.EqualTo(fs.GetMasterBlock().IsDirectoryEntered));
                }
            }
            finally
            {
                try { File.Delete(temp); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
