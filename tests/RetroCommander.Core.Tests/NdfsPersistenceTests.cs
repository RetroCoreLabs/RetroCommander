//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Managers;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Services;
using RetroIO.Storage.Ndfs.Creation;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// End-to-end persistence tests through the REAL app mount path (VolumeProviderFactory +
    /// PanelManager), reproducing the reported data loss: mount an NDFS image read-write, create a
    /// user and copy a file, unmount, remount — everything was gone.
    ///
    /// Two root causes this pins down:
    ///   1) VolumeProviderFactory opened the image with File.OpenRead() (READ-ONLY) even when
    ///      mounting read-write, so writes lived only in the in-memory page cache.
    ///   2) PanelManager.RemovePanel removed the panel WITHOUT disposing it, so the driver / block
    ///      device / FileStream were never flushed or closed on unmount.
    /// Both had to be fixed for changes to survive a remount.
    /// </summary>
    [TestFixture]
    [Category("NDFS")]
    public class NdfsPersistenceTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "rfs_ndfs_persist_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // best effort
            }
        }

        private string MakeImageUrn(out string imagePath)
        {
            imagePath = Path.Combine(_testDir, "persist.img");
            NdfsImageCreator.CreateImageFromTemplate(
                imagePath,
                NdfsImageTemplate.Floppy12MB,
                directoryName: "PERSIST",
                bootLoaderType: NdfsBootLoaderType.Floppy);
            return "ndfs:///" + imagePath.Replace("\\", "/");
        }

        private static IFileSystemEntry? Find(IFileSystemPanel panel, string name)
        {
            for (int i = 0; i < panel.Entries.Count; i++)
            {
                if (string.Equals(panel.Entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return panel.Entries[i];
            }
            return null;
        }

        [Test]
        public async Task Mount_CreateUserAndFile_Unmount_Remount_StillThere()
        {
            string urn = MakeImageUrn(out _);
            var factory = new VolumeProviderFactory(null, DriverFactoryBuilder.CreateWithAllDrivers());

            // --- Mount #1 (read-write): create a user and copy a file into it ---
            IFileSystemPanel panel1 = await factory.CreatePanelAsync(urn);
            Assert.That(panel1.IsReadOnly, Is.False, "a writable NDFS image must mount read-write");

            await panel1.RefreshAsync();
            await panel1.CreateDirectoryAsync("alice");          // NDFS user ALICE
            await panel1.NavigateAsync("/ALICE");
            using (var src = new MemoryStream(Encoding.ASCII.GetBytes("bpun payload")))
            {
                await panel1.CreateFileAsync("fs.bpun", src);    // file FS:BPUN under ALICE
            }

            // --- Unmount: dispose flushes the driver -> block device -> FileStream to disk ---
            (panel1 as IDisposable)?.Dispose();

            // --- Mount #2 (fresh open of the same file): user + file MUST still be there ---
            IFileSystemPanel panel2 = await factory.CreatePanelAsync(urn);

            await panel2.RefreshAsync();
            Assert.That(Find(panel2, "ALICE"), Is.Not.Null, "user ALICE must survive unmount/remount");

            await panel2.NavigateAsync("/ALICE");
            await panel2.RefreshAsync();
            Assert.That(Find(panel2, "FS:BPUN"), Is.Not.Null, "file FS:BPUN must survive unmount/remount");

            (panel2 as IDisposable)?.Dispose();
        }

        [Test]
        public async Task PanelManager_RemovePanel_DisposesPanel_FlushesAndReleasesHandle()
        {
            string urn = MakeImageUrn(out string imagePath);
            var pm = new PanelManager(new VolumeProviderFactory(null, DriverFactoryBuilder.CreateWithAllDrivers()));

            IFileSystemPanel panel = await pm.CreatePanelAsync(urn); // registered with the manager
            await panel.CreateDirectoryAsync("bob");                 // write something

            bool removed = pm.RemovePanel(panel.Id);
            Assert.That(removed, Is.True);

            // If RemovePanel disposed the panel, the file handle is released — an EXCLUSIVE open
            // now succeeds (it would throw IOException if the handle were still held).
            using (var exclusive = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.That(exclusive.Length, Is.GreaterThan(0));
            }

            // And the write made it to disk.
            var verifyFactory = new VolumeProviderFactory(null, DriverFactoryBuilder.CreateWithAllDrivers());
            IFileSystemPanel verify = await verifyFactory.CreatePanelAsync(urn);
            await verify.RefreshAsync();
            Assert.That(Find(verify, "BOB"), Is.Not.Null, "write must persist after RemovePanel disposes");
            (verify as IDisposable)?.Dispose();
        }
    }
}
