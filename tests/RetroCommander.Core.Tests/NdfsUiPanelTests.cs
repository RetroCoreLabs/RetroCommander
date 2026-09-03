//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Operations;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local;
using RetroIO.Storage.Ndfs.Creation;
using RetroIO.Storage.Ndfs.Drivers;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// UI-layer regression tests for NDFS through the exact panel the app drives
    /// (DriverAdapterFileSystemPanel + DriverAdapterFileSystemEntry). Covers two reported bugs:
    ///
    /// 1) F7/Mkdir on NDFS reported "Directory 'test' was created but could not be found after
    ///    refresh" even though the user WAS created — NDFS upper-cases the name ("test" -> "TEST")
    ///    and the post-create lookup was case-sensitive (and threw, hiding the success).
    ///
    /// 2) Copying "fs.bpun" into NDFS showed up as just "fs": the panel surfaces
    ///    IFileSystemEntry.Name (= FileMetadata.Name), and NDFS was putting the bare ObjectName
    ///    there instead of the full "NAME:TYPE" display name, so the ":BPUN" type was dropped.
    /// </summary>
    [TestFixture]
    [Category("NDFS")]
    public class NdfsUiPanelTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "rfs_ndfs_ui_" + Path.GetRandomFileName());
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

        private string MakeImage()
        {
            string path = Path.Combine(_testDir, "ui.img");
            NdfsImageCreator.CreateImageFromTemplate(
                path,
                NdfsImageTemplate.Floppy12MB,
                directoryName: "UI-TEST",
                bootLoaderType: NdfsBootLoaderType.Floppy);
            return path;
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
        public async Task CreateDirectory_LowercaseName_DoesNotThrow_AndIsListed()
        {
            string image = MakeImage();
            using FileStream stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite);

            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: false);

            DriverAdapterFileSystemPanel panel =
                new DriverAdapterFileSystemPanel("ndfs", driver, "ndfs:///ui.img");
            await panel.RefreshAsync();

            // Must NOT throw despite NDFS upper-casing "test" -> "TEST".
            IFileSystemEntry created = await panel.CreateDirectoryAsync("test");
            Assert.That(created, Is.Not.Null, "CreateDirectoryAsync must return the created entry, not throw");

            // The user appears in the listing (under its normalised, upper-cased name).
            await panel.RefreshAsync();
            Assert.That(Find(panel, "TEST"), Is.Not.Null, "the new user must be listed");

            panel.Dispose();
        }

        [Test]
        public async Task CopyFileWithType_KeepsTypeInPanelName()
        {
            string image = MakeImage();
            using FileStream stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite);

            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: false);

            DriverAdapterFileSystemPanel panel =
                new DriverAdapterFileSystemPanel("ndfs", driver, "ndfs:///ui.img");
            await panel.RefreshAsync();

            // Create a user and navigate into it (files live under users in NDFS).
            await panel.CreateDirectoryAsync("alice");
            await panel.NavigateAsync("/ALICE");

            // This mirrors F5-Copy of a Windows "fs.bpun" into the NDFS volume.
            byte[] data = Encoding.ASCII.GetBytes("bpun payload");
            using (MemoryStream src = new MemoryStream(data))
            {
                await panel.CreateFileAsync("fs.bpun", src);
            }
            await panel.RefreshAsync();

            // The panel name (what the grid shows) must keep the type: "FS:BPUN", not "FS".
            IFileSystemEntry? entry = Find(panel, "FS:BPUN");
            Assert.That(entry, Is.Not.Null, "copied 'fs.bpun' must show as FS:BPUN in the panel");
            Assert.That(Find(panel, "FS"), Is.Null, "must NOT appear as the bare name 'FS'");

            panel.Dispose();
        }

        /// <summary>
        /// Root-cause regression test: DriverAdapterFileSystemEntry.ExtractProperties previously
        /// left an NDFS entry's property bag with only StandardProperties.* — real NDFS-specific
        /// metadata (owner, access bits, page layout, ...) was never populated, even though
        /// NdfsDriver already implements IExtendedAttributeSupport.GetFileProperties. That gap made
        /// "NDFS-to-NDFS copies preserve metadata" true only in hand-built unit tests, not for a
        /// real mounted image driving the UI. This test drives a real NDFS image end-to-end and
        /// asserts the entry's Properties actually carry "ndfs."-prefixed keys.
        /// </summary>
        [Test]
        public async Task RealNdfsEntry_PropertiesContainNdfsMetadata()
        {
            string image = MakeImage();
            using FileStream stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite);

            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: false);

            DriverAdapterFileSystemPanel panel =
                new DriverAdapterFileSystemPanel("ndfs", driver, "ndfs:///ui.img");
            await panel.RefreshAsync();

            await panel.CreateDirectoryAsync("dave");
            await panel.NavigateAsync("/DAVE");

            byte[] data = Encoding.ASCII.GetBytes("metadata payload");
            using (MemoryStream src = new MemoryStream(data))
            {
                await panel.CreateFileAsync("meta.txt", src);
            }
            await panel.RefreshAsync();

            IFileSystemEntry? entry = Find(panel, "META:TXT") ?? Find(panel, "META.TXT");
            Assert.That(entry, Is.Not.Null, "the file just created must be listed");

            bool hasNdfsMetadata = false;
            var props = entry!.Properties.GetAllProperties();
            foreach (var kvp in props)
            {
                if (kvp.Key.StartsWith("ndfs.", StringComparison.Ordinal))
                {
                    hasNdfsMetadata = true;
                    break;
                }
            }
            Assert.That(hasNdfsMetadata, Is.True,
                "a real NDFS file's property bag must carry real 'ndfs.*' metadata, not be empty");

            panel.Dispose();
        }

        /// <summary>
        /// Reproduces the reported bug: copying a file from an NDFS panel to a native Windows
        /// panel via F5 (MainWindow.OnCopyClick -> SmartFileOperations.CopyAsync) produces a
        /// zero-length destination file. Unlike SmartFileOperationsTests, this drives the REAL
        /// DriverAdapterFileSystemPanel/Entry (backed by an actual NDFS image) and the REAL
        /// NativeFileSystemPanel, instead of Moq'd IFileSystemEntry/IFileSystemPanel stand-ins —
        /// those mocks can't reproduce a bug that lives in the NDFS read path or in how the
        /// panel/entry wiring flows real bytes.
        /// </summary>
        [Test]
        public async Task CopyFile_NdfsToNative_ContentIsNotZeroLength()
        {
            string image = MakeImage();
            using FileStream stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite);

            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: false);

            DriverAdapterFileSystemPanel sourcePanel =
                new DriverAdapterFileSystemPanel("ndfs", driver, "ndfs:///ui.img");
            await sourcePanel.RefreshAsync();

            // Create a user and a file under it, exactly like a real NDFS volume would have.
            await sourcePanel.CreateDirectoryAsync("bob");
            await sourcePanel.NavigateAsync("/BOB");

            byte[] expected = Encoding.ASCII.GetBytes("hello from ndfs, this is real payload data");
            using (MemoryStream src = new MemoryStream(expected))
            {
                await sourcePanel.CreateFileAsync("data.txt", src);
            }
            await sourcePanel.RefreshAsync();

            IFileSystemEntry? sourceFile = Find(sourcePanel, "DATA:TXT") ?? Find(sourcePanel, "DATA.TXT");
            Assert.That(sourceFile, Is.Not.Null, "the file just created on the NDFS panel must be listed");
            sourcePanel.SelectedEntry = sourceFile;

            string nativeDir = Path.Combine(_testDir, "native-target");
            Directory.CreateDirectory(nativeDir);
            NativeFileSystemPanel targetPanel = new NativeFileSystemPanel("native", nativeDir);

            SmartFileOperations fileOps = new SmartFileOperations();
            var result = await fileOps.CopyAsync(sourceFile!, targetPanel, new CopyOptions { Overwrite = true });

            Assert.That(result.Success, Is.True, $"copy must succeed: {result.ErrorMessage}");

            // NDFS uses ':' as its NAME:TYPE separator; NativeFileSystemPanel maps ':' to '.'
            // (the Windows/FAT extension separator) because a raw ':' in an NTFS path is parsed
            // as an Alternate Data Stream marker, not a literal filename character.
            string destPath = Path.Combine(nativeDir, "DATA.TXT");
            Assert.That(File.Exists(destPath), Is.True, "destination file must exist (as DATA.TXT, not DATA:TXT)");

            byte[] actual = File.ReadAllBytes(destPath);
            Assert.That(actual.Length, Is.GreaterThan(0), "destination file must NOT be zero-length");
            Assert.That(actual, Is.EqualTo(expected), "destination file content must match the NDFS source content");

            // The raw colon path must NOT resolve to a visible file — if it did, the bytes went
            // into a hidden NTFS Alternate Data Stream instead of the real file (the original bug).
            Assert.That(File.Exists(Path.Combine(nativeDir, "DATA:TXT")), Is.False,
                "must not silently write into an NTFS Alternate Data Stream");

            sourcePanel.Dispose();
        }

        /// <summary>
        /// End-to-end proof of the new "preserve NDFS metadata on export" feature: a real NDFS
        /// file, copied to a real native Windows folder via the real SmartFileOperations.CopyAsync,
        /// produces both the visible data file AND a ".xat" sidecar carrying its real "ndfs.*"
        /// metadata (see DriverAdapterFileSystemEntry.ExtractProperties and
        /// SmartFileOperations.TryWriteNdfsMetadataSidecarAsync).
        /// </summary>
        [Test]
        public async Task CopyFile_NdfsToNative_WritesMetadataSidecar()
        {
            string image = MakeImage();
            using FileStream stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite);

            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: false);

            DriverAdapterFileSystemPanel sourcePanel =
                new DriverAdapterFileSystemPanel("ndfs", driver, "ndfs:///ui.img");
            await sourcePanel.RefreshAsync();

            await sourcePanel.CreateDirectoryAsync("erin");
            await sourcePanel.NavigateAsync("/ERIN");

            byte[] payload = Encoding.ASCII.GetBytes("sidecar test payload");
            using (MemoryStream src = new MemoryStream(payload))
            {
                await sourcePanel.CreateFileAsync("sidecar.txt", src);
            }
            await sourcePanel.RefreshAsync();

            IFileSystemEntry? sourceFile = Find(sourcePanel, "SIDECAR:TXT") ?? Find(sourcePanel, "SIDECAR.TXT");
            Assert.That(sourceFile, Is.Not.Null);

            string nativeDir = Path.Combine(_testDir, "native-target-sidecar");
            Directory.CreateDirectory(nativeDir);
            NativeFileSystemPanel targetPanel = new NativeFileSystemPanel("native", nativeDir);

            SmartFileOperations fileOps = new SmartFileOperations();
            var result = await fileOps.CopyAsync(sourceFile!, targetPanel,
                new CopyOptions { Overwrite = true, PreserveNdfsMetadataOnExport = true });

            Assert.That(result.Success, Is.True, $"copy must succeed: {result.ErrorMessage}");
            Assert.That(File.Exists(Path.Combine(nativeDir, "SIDECAR.TXT")), Is.True);

            string xatPath = Path.Combine(nativeDir, "SIDECAR.xat");
            Assert.That(File.Exists(xatPath), Is.True, "a .xat sidecar must be written next to the exported file");
            Assert.That(File.ReadAllText(xatPath), Does.Contain("ndfs."),
                "the sidecar must contain real NDFS-prefixed metadata keys");

            sourcePanel.Dispose();
        }

        /// <summary>
        /// Full round trip for the "preserve NDFS metadata" feature: export a real NDFS file to a
        /// native Windows folder (producing a .xat sidecar), then re-import that native file back
        /// into a DIFFERENT NDFS user directory. Asserts the re-imported entry's AccessBits match a
        /// distinctive value planted in the sidecar rather than NDFS's default for a freshly created
        /// file — proof the sidecar's metadata was actually persisted via
        /// IExtendedAttributeSupport.WriteFileWithProperties, not silently dropped.
        ///
        /// AccessBits (not UserName/owner) is the field asserted here deliberately: NDFS ties file
        /// ownership to the object-table slot the file physically occupies, assigned from the
        /// destination directory at write time — it is architecturally NOT a freeform property a
        /// re-import can reassign, only genuinely portable metadata (permissions, timestamps) can
        /// round-trip across a directory change. See NdfsPropertyBagSerializer.ApplyPortableMetadata.
        /// </summary>
        [Test]
        public async Task CopyFile_ExportThenReimport_RestoresPortableNdfsMetadata()
        {
            string image = MakeImage();
            using FileStream stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite);

            NdfsDriver driver = new NdfsDriver();
            driver.Mount(stream, readOnly: false);

            DriverAdapterFileSystemPanel ndfsPanel =
                new DriverAdapterFileSystemPanel("ndfs", driver, "ndfs:///ui.img");
            await ndfsPanel.RefreshAsync();

            await ndfsPanel.CreateDirectoryAsync("frank");
            await ndfsPanel.NavigateAsync("/FRANK");

            byte[] payload = Encoding.ASCII.GetBytes("round trip payload");
            using (MemoryStream src = new MemoryStream(payload))
            {
                await ndfsPanel.CreateFileAsync("round.txt", src);
            }
            await ndfsPanel.RefreshAsync();

            IFileSystemEntry? originalEntry = Find(ndfsPanel, "ROUND:TXT") ?? Find(ndfsPanel, "ROUND.TXT");
            Assert.That(originalEntry, Is.Not.Null);

            uint defaultAccessBits = originalEntry!.Properties.GetProperty<uint>(
                RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.AccessBits);

            // Export to a native folder — produces ROUND.TXT + ROUND.xat.
            string nativeDir = Path.Combine(_testDir, "roundtrip-native");
            Directory.CreateDirectory(nativeDir);
            NativeFileSystemPanel nativePanel = new NativeFileSystemPanel("native", nativeDir);

            SmartFileOperations fileOps = new SmartFileOperations();
            var exportResult = await fileOps.CopyAsync(originalEntry, nativePanel,
                new CopyOptions { Overwrite = true, PreserveNdfsMetadataOnExport = true });
            Assert.That(exportResult.Success, Is.True, $"export must succeed: {exportResult.ErrorMessage}");

            string xatPath = Path.Combine(nativeDir, "ROUND.xat");
            Assert.That(File.Exists(xatPath), Is.True);

            // Plant distinctive values in the sidecar — AccessBits different from NDFS's default
            // for a freshly created file, plus all three date fields (the exact values from a real
            // bug report where only DateCreated was actually restored on import, silently dropping
            // LastDateOpenedForRead/LastDateOpenedForWrite) — so restoring ALL of them proves the
            // whole sidecar is applied, not just whichever field happened to work.
            uint distinctiveAccessBits = defaultAccessBits ^ 0x0055;
            const uint expectedDateCreated = 2372846803u;
            const uint expectedLastRead = 2439864995u;
            const uint expectedLastWrite = 2372855763u;
            var sidecarProps = RetroIO.Storage.Ndfs.PropertyBags.NdfsXatSerializer.DeserializeFromJson(File.ReadAllText(xatPath));
            sidecarProps[RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.AccessBits] = distinctiveAccessBits;
            sidecarProps[RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.DateCreated] = expectedDateCreated;
            sidecarProps[RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.LastDateOpenedForRead] = expectedLastRead;
            sidecarProps[RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.LastDateOpenedForWrite] = expectedLastWrite;
            File.WriteAllText(xatPath, RetroIO.Storage.Ndfs.PropertyBags.NdfsXatSerializer.SerializeToJson(sidecarProps));

            // Re-import into a DIFFERENT NDFS user directory: GRACE.
            await ndfsPanel.NavigateAsync("/");
            await ndfsPanel.CreateDirectoryAsync("grace");
            await ndfsPanel.NavigateAsync("/GRACE");

            await nativePanel.RefreshAsync();
            IFileSystemEntry? nativeEntry = null;
            for (int i = 0; i < nativePanel.Entries.Count; i++)
            {
                if (string.Equals(nativePanel.Entries[i].Name, "ROUND.TXT", StringComparison.OrdinalIgnoreCase))
                {
                    nativeEntry = nativePanel.Entries[i];
                    break;
                }
            }
            Assert.That(nativeEntry, Is.Not.Null, "the exported ROUND.TXT must be listed on the native panel");

            var importResult = await fileOps.CopyAsync(nativeEntry!, ndfsPanel,
                new CopyOptions { Overwrite = true, UseXatMetadataOnImport = true });
            Assert.That(importResult.Success, Is.True, $"import must succeed: {importResult.ErrorMessage}");

            await ndfsPanel.RefreshAsync();
            IFileSystemEntry? reimportedEntry = Find(ndfsPanel, "ROUND:TXT") ?? Find(ndfsPanel, "ROUND.TXT");
            Assert.That(reimportedEntry, Is.Not.Null, "the re-imported file must be listed under GRACE");

            uint reimportedAccessBits = reimportedEntry!.Properties.GetProperty<uint>(
                RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.AccessBits);
            Assert.That(reimportedAccessBits, Is.EqualTo(distinctiveAccessBits),
                "the .xat sidecar's AccessBits must have been persisted via WriteFileWithProperties, " +
                "not silently dropped as it previously was");

            // The whole sidecar must be applied, not just AccessBits — regression coverage for a
            // real bug where ApplyPortableMetadata only restored DateCreated and silently dropped
            // LastDateOpenedForRead/LastDateOpenedForWrite.
            uint reimportedDateCreated = reimportedEntry.Properties.GetProperty<uint>(
                RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.DateCreated);
            uint reimportedLastRead = reimportedEntry.Properties.GetProperty<uint>(
                RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.LastDateOpenedForRead);
            uint reimportedLastWrite = reimportedEntry.Properties.GetProperty<uint>(
                RetroIO.Storage.Ndfs.PropertyBags.NdfsPropertyBagKeys.LastDateOpenedForWrite);
            Assert.That(reimportedDateCreated, Is.EqualTo(expectedDateCreated), "DateCreated must round-trip");
            Assert.That(reimportedLastRead, Is.EqualTo(expectedLastRead),
                "LastDateOpenedForRead must round-trip, not just DateCreated");
            Assert.That(reimportedLastWrite, Is.EqualTo(expectedLastWrite),
                "LastDateOpenedForWrite must round-trip, not just DateCreated");

            ndfsPanel.Dispose();
        }

        /// <summary>
        /// Same reproduction as <see cref="CopyFile_NdfsToNative_ContentIsNotZeroLength"/> but goes
        /// through the exact mounting path the real app uses: MountDialog hands an "ndfs://" URN
        /// to MainWindow.OnMountClick, which calls VolumeProviderFactory.CreatePanelAsync — not a
        /// hand-built DriverAdapterFileSystemPanel over an already-open FileStream. If the bug is
        /// specific to how CreatePanelAsync opens/mounts the image (URN parsing, stream lifetime,
        /// driver detection), this test will fail where the previous one passed.
        /// </summary>
        [Test]
        public async Task CopyFile_NdfsToNative_ViaVolumeProviderFactory_ContentIsNotZeroLength()
        {
            string image = MakeImage();

            // Seed the image with a user + file the same way a real user would, then unmount.
            using (FileStream seedStream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite))
            {
                NdfsDriver seedDriver = new NdfsDriver();
                seedDriver.Mount(seedStream, readOnly: false);
                DriverAdapterFileSystemPanel seedPanel =
                    new DriverAdapterFileSystemPanel("seed", seedDriver, $"ndfs:///{image.Replace('\\', '/')}");
                await seedPanel.RefreshAsync();
                await seedPanel.CreateDirectoryAsync("carol");
                await seedPanel.NavigateAsync("/CAROL");

                byte[] seedData = Encoding.ASCII.GetBytes("payload via VolumeProviderFactory mount path");
                using (MemoryStream src = new MemoryStream(seedData))
                {
                    await seedPanel.CreateFileAsync("report.txt", src);
                }
                seedPanel.Dispose();
            }

            byte[] expected = Encoding.ASCII.GetBytes("payload via VolumeProviderFactory mount path");

            DriverFactory driverFactory = new DriverFactory();
            driverFactory.Register(new RetroIO.Storage.Ndfs.Drivers.NdfsDriverProvider());
            VolumeProviderFactory factory = new VolumeProviderFactory(null, driverFactory);

            string urn = $"ndfs:///{image.Replace('\\', '/')}";
            IFileSystemPanel sourcePanel = await factory.CreatePanelAsync(urn);
            await sourcePanel.NavigateAsync("/CAROL");

            IFileSystemEntry? sourceFile = Find(sourcePanel, "REPORT:TXT") ?? Find(sourcePanel, "REPORT.TXT");
            Assert.That(sourceFile, Is.Not.Null, "the seeded file must be listed via the real mount path");
            sourcePanel.SelectedEntry = sourceFile;

            string nativeDir = Path.Combine(_testDir, "native-target-2");
            Directory.CreateDirectory(nativeDir);
            IFileSystemPanel targetPanel = await factory.CreatePanelAsync($"file:///{nativeDir.Replace('\\', '/')}");

            SmartFileOperations fileOps = new SmartFileOperations();
            var result = await fileOps.CopyAsync(sourceFile!, targetPanel, new CopyOptions { Overwrite = true });

            Assert.That(result.Success, Is.True, $"copy must succeed: {result.ErrorMessage}");

            // See CopyFile_NdfsToNative_ContentIsNotZeroLength for why ':' maps to '.'.
            string destPath = Path.Combine(nativeDir, "REPORT.TXT");
            Assert.That(File.Exists(destPath), Is.True, "destination file must exist (as REPORT.TXT, not REPORT:TXT)");

            byte[] actual = File.ReadAllBytes(destPath);
            Assert.That(actual.Length, Is.GreaterThan(0), "destination file must NOT be zero-length");
            Assert.That(actual, Is.EqualTo(expected), "destination file content must match the NDFS source content");

            Assert.That(File.Exists(Path.Combine(nativeDir, "REPORT:TXT")), Is.False,
                "must not silently write into an NTFS Alternate Data Stream");

            (sourcePanel as IDisposable)?.Dispose();
        }
    }
}
