//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Buffers.Binary;
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
    /// Opens a PC hard disk kept as raw <c>.img</c>, raw <c>.hdd</c> and fixed <c>.vhd</c> through
    /// RetroCommander's own <see cref="VolumeProviderFactory"/> and lists the FAT partition's files.
    /// </summary>
    /// <remarks>
    /// The disk is built by the test (an MBR with one FAT16 partition, RetroIO's PcImageFactory) in
    /// its own temporary folder, with one file written into it before it is saved. The VHD footer is
    /// written byte by byte from the VHD spec, with the values RetroCore's PC/AT code writes.
    /// </remarks>
    [TestFixture]
    public class HardDiskImageFormatTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc_hdformats_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            // Disposing a panel does not close the image file it mounted (the same as in
            // VolumeProviderFactoryMbrPartitionTests), so let the finalisers close the files first.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        [TestCase(".img", false)]
        [TestCase(".hdd", false)]
        [TestCase(".vhd", true)]
        public async Task Open_ListsTheFatPartitionsFiles(string extension, bool vhd)
        {
            string path = MakeDisk("DISK" + extension, vhd, damageFooter: false);
            VolumeProviderFactory factory = NewFactory();

            IFileSystemPanel panel = await factory.CreatePanelAsync("file:///" + path.Replace('\\', '/') + "#0");
            try
            {
                await panel.RefreshAsync();
                Assert.That(panel.FileSystemType, Is.EqualTo("FAT16"));
                bool found = false;
                for (int i = 0; i < panel.Entries.Count; i++)
                {
                    if (string.Equals(panel.Entries[i].Name, "README.TXT", StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                    }
                }
                Assert.That(found, Is.True, "README.TXT is in the root directory");
            }
            finally
            {
                (panel as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Open_DamagedVhd_IsRefusedWithTheReason()
        {
            string path = MakeDisk("DISK.vhd", vhd: true, damageFooter: true);
            VolumeProviderFactory factory = NewFactory();
            InvalidDataException ex = Assert.ThrowsAsync<InvalidDataException>(async () =>
                await factory.CreatePanelAsync("file:///" + path.Replace('\\', '/') + "#0"))!;
            Assert.That(ex.Message, Does.Contain("checksum"));
        }

        private static VolumeProviderFactory NewFactory()
        {
            DriverFactory drivers = new DriverFactory();
            drivers.Register(new PcFloppyDriverProvider());
            return new VolumeProviderFactory(null, drivers);
        }

        private string MakeDisk(string name, bool vhd, bool damageFooter)
        {
            byte[] disk = PcImageFactory.CreateBlankHardDisk(PcHddPreset.At20MB, "TESTHDD");
            using (MemoryStream ms = new MemoryStream(disk, writable: true))
            {
                PcFloppyDriver driver = new PcFloppyDriver();
                ((RetroIO.Storage.Core.IPartitionProvider)driver).SelectPartition("0");
                driver.Mount(ms, readOnly: false);
                driver.WriteFile("README.TXT", new byte[] { 72, 105 });
                driver.Dispose();
            }

            string path = Path.Combine(_dir, name);
            using FileStream fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            fs.Write(disk, 0, disk.Length);
            if (vhd)
            {
                byte[] f = new byte[512];
                System.Text.Encoding.ASCII.GetBytes("conectix").CopyTo(f, 0);
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(8), 2);
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(12), 0x00010000);
                BinaryPrimitives.WriteUInt64BigEndian(f.AsSpan(16), ulong.MaxValue);
                System.Text.Encoding.ASCII.GetBytes("rcl ").CopyTo(f, 28);
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(36), 0x5769326B);
                BinaryPrimitives.WriteUInt64BigEndian(f.AsSpan(40), (ulong)disk.Length);
                BinaryPrimitives.WriteUInt64BigEndian(f.AsSpan(48), (ulong)disk.Length);
                BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(56), 615);
                f[58] = 4;
                f[59] = 17;
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(60), 2);
                uint sum = 0;
                for (int i = 0; i < 512; i++)
                {
                    if (i < 64 || i >= 68) sum += f[i];
                }
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(64), ~sum);
                if (damageFooter)
                {
                    f[300] ^= 0xFF;
                }
                fs.Write(f, 0, f.Length);
            }
            return path;
        }
    }
}
