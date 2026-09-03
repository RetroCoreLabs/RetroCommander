//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroCommander.Core.Tests -- Real MBR partition mounting tests
//

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Drivers;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Real, end-to-end tests for VolumeProviderFactory's MBR partition support, against a real
    /// PC hard disk image (a genuine MBR-partitioned PC-DOS 3.3 bootable disk, not a synthetic
    /// fixture) — verified by hand (hex-dumped the MBR and the FAT12 boot sector inside the
    /// partition) to contain a single FAT12 partition with IBMBIO.COM, IBMDOS.COM, COMMAND.COM,
    /// and a DOS subdirectory, before writing any assertion below.
    /// </summary>
    /// <remarks>
    /// There is deliberately no "mount with no fragment" test here: PcFloppyDriver implements
    /// IPartitionProvider the same way BsdDriver does, and ConfigurePartitionForDriver's existing,
    /// pre-existing convention (unchanged by this feature) is that no fragment means SetRawMode()
    /// — the whole image is treated as the filesystem. For an MBR-partitioned image that's the
    /// wrong thing to do (sector 0 is the MBR, not a boot sector), exactly like mounting a BSD
    /// disk-label image with no #<letter> would be. A real MBR partition always needs an explicit
    /// #<index>, same as BSD always needs an explicit #<letter> — there is no "auto-select the
    /// sole partition" magic for either.
    /// </remarks>
    [TestFixture]
    public class VolumeProviderFactoryMbrPartitionTests
    {
        private const string HardDiskImage = @"C:\Users\ronny\OneDrive\Documents\RetroFS\test-images\pc\hd10.img";

        private DriverFactory _driverFactory = null!;
        private VolumeProviderFactory _factory = null!;
        private string _tempImagePath = null!;

        [SetUp]
        public void SetUp()
        {
            if (!File.Exists(HardDiskImage))
            {
                Assert.Ignore($"Test image not found: {HardDiskImage}");
            }

            _driverFactory = new DriverFactory();
            _driverFactory.Register(new PcFloppyDriverProvider());
            _factory = new VolumeProviderFactory(null, _driverFactory);

            // Work on a throwaway copy — VolumeProviderFactory mounts read-write when it can, and
            // these tests must not modify the real fixture.
            _tempImagePath = Path.Combine(Path.GetTempPath(), "mbr_test_" + Path.GetRandomFileName() + ".img");
            File.Copy(HardDiskImage, _tempImagePath, overwrite: true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempImagePath != null && File.Exists(_tempImagePath))
            {
                // Best-effort: the panel/driver holds the FileStream open for its lifetime (by
                // design — see VolumeProviderFactory.OpenImageStream's comment), and each test
                // below disposes its own panel before returning. If a test fails before disposing,
                // don't let a locked-file cleanup failure mask the real assertion failure.
                try
                {
                    File.Delete(_tempImagePath);
                }
                catch (IOException)
                {
                }
            }
        }

        [Test]
        public async Task Mount_ExplicitPartitionZero_DetectsFat()
        {
            var panel = await _factory.CreatePanelAsync($"file:///{_tempImagePath.Replace('\\', '/')}#0");
            try
            {
                await panel.RefreshAsync();
                AssertRootDirectoryMatchesRealDisk(panel);
            }
            finally
            {
                (panel as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Mount_NonexistentPartitionIndex_ThrowsWithClearMessage()
        {
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _factory.CreatePanelAsync($"file:///{_tempImagePath.Replace('\\', '/')}#5"));

            Assert.That(ex!.Message, Does.Contain("5"));
            Assert.That(ex.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Mount_SelectedPartition_ReadFile_MatchesRealDiskContent()
        {
            var panel = await _factory.CreatePanelAsync($"file:///{_tempImagePath.Replace('\\', '/')}#0");
            try
            {
                await panel.RefreshAsync();

                IFileSystemEntry? commandCom = null;
                for (int i = 0; i < panel.Entries.Count; i++)
                {
                    if (string.Equals(panel.Entries[i].Name, "COMMAND.COM", StringComparison.OrdinalIgnoreCase))
                    {
                        commandCom = panel.Entries[i];
                        break;
                    }
                }

                Assert.That(commandCom, Is.Not.Null, "COMMAND.COM should exist in the root directory of this real disk image");

                using var stream = await commandCom!.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] data = ms.ToArray();

                // First 2 bytes of a real DOS COMMAND.COM are the 0x5A4D "MZ" EXE-ish signature is NOT
                // guaranteed for old COM-style command.com — instead assert the size is sane and
                // matches what the real disk image reports (hex-dumped directly: size field 0x62db =
                // 25307 bytes, verified against the raw root directory entry before writing this test).
                Assert.That(data.Length, Is.EqualTo(25307),
                    "COMMAND.COM size should match the real directory entry on this hand-verified disk image");
            }
            finally
            {
                (panel as IDisposable)?.Dispose();
            }
        }

        private static void AssertRootDirectoryMatchesRealDisk(IFileSystemPanel panel)
        {
            Assert.That(panel.Entries.Count, Is.EqualTo(4),
                "Root of this real disk has exactly IBMBIO.COM, IBMDOS.COM, COMMAND.COM, and DOS (verified via hex dump)");

            bool hasIbmBio = false, hasIbmDos = false, hasCommand = false, hasDosDir = false;
            for (int i = 0; i < panel.Entries.Count; i++)
            {
                var entry = panel.Entries[i];
                if (string.Equals(entry.Name, "IBMBIO.COM", StringComparison.OrdinalIgnoreCase)) hasIbmBio = true;
                if (string.Equals(entry.Name, "IBMDOS.COM", StringComparison.OrdinalIgnoreCase)) hasIbmDos = true;
                if (string.Equals(entry.Name, "COMMAND.COM", StringComparison.OrdinalIgnoreCase)) hasCommand = true;
                if (string.Equals(entry.Name, "DOS", StringComparison.OrdinalIgnoreCase) && entry.IsDirectory) hasDosDir = true;
            }

            Assert.That(hasIbmBio, Is.True, "IBMBIO.COM should be present");
            Assert.That(hasIbmDos, Is.True, "IBMDOS.COM should be present");
            Assert.That(hasCommand, Is.True, "COMMAND.COM should be present");
            Assert.That(hasDosDir, Is.True, "DOS subdirectory should be present (as a directory, not a file)");
        }
    }
}
