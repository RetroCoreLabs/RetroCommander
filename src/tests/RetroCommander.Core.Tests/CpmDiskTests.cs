//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Services;
using RetroIO.Storage.Local.Drivers;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// CP/M disks opened the way the application opens them: detection through the application's
    /// own driver list, and writes through the panel the two panes use.
    /// </summary>
    /// <remarks>
    /// The CP/M images are the personal test-image collection, in the folder the
    /// <c>RETROIO_TEST_IMAGES</c> environment variable names (the same variable RetroIO's tests
    /// read), under <c>cpm/</c>. Without it these tests are ignored with a message saying so.
    /// The CP/M disks share their extensions with other formats:
    ///  - .d64, .d71 and .d81 with ordinary Commodore DOS disks.
    ///  - .img with PC floppies, .dsk with Dragon disks.
    /// So the detection tests also open a plain C64 disk and check it still opens as Commodore.
    /// </remarks>
    [TestFixture]
    [Category("CPM")]
    public class CpmDiskTests
    {
        private const string ImagesVariable = "RETROIO_TEST_IMAGES";

        [TestCase("c128/cpm.d64")]
        [TestCase("c128/MBASIC.d71")]
        [TestCase("c128/WordStar40.d81")]
        [TestCase("kaypro/Zork.img")]
        [TestCase("kaypro/cpm22-rom149.img")]
        [TestCase("amstrad/Deadline_1982_Infocom_cpm_version.dsk")]
        [TestCase("cpm86/CPM86_10.IMG")]
        [TestCase("cpm86/CPM86_10.IMD")]
        [TestCase("osborne/OCC1-001.IMD")]
        [TestCase("tiki100/tiko_kjerne_v4.01.dsk")]
        [TestCase("tiki100/AR-games.dsk")]
        [TestCase("tiki100/ar-games-200k.dsk")]
        [TestCase("tiki100/FD0054.dsk")]
        public async Task CpmImage_OpensAsCpm_AndListsFiles(string relative)
        {
            string path = CpmImage(relative);
            IFileSystemPanel panel = await DriverFactoryBuilderPanel(path);
            try
            {
                await panel.RefreshAsync();
                Assert.That(panel.FileSystemType, Is.EqualTo("CPM"), relative);
                Assert.That(panel.Entries.Count, Is.GreaterThan(0), relative + " lists its files");
            }
            finally
            {
                (panel as IDisposable)?.Dispose();
            }
        }

        [TestCase("PLUSH.D64", "d64")]
        [TestCase("test.d71", "d71")]
        [TestCase("test.d81", "d81")]
        public async Task PlainCommodoreDisk_IsNotTakenByCpm(string name, string folder)
        {
            string? path = FindDemoImage(Path.Combine("demo", "test-images", "c64", folder, name));
            if (path == null)
            {
                Assert.Ignore("demo/test-images/c64/" + folder + "/" + name + " is not present");
                return;
            }

            IFileSystemPanel panel = await DriverFactoryBuilderPanel(path);
            try
            {
                await panel.RefreshAsync();
                Assert.That(panel.FileSystemType, Is.Not.EqualTo("CPM"), "an ordinary Commodore DOS disk is not CP/M");
                Assert.That(panel.Entries.Count, Is.GreaterThan(0), name + " lists its files");
            }
            finally
            {
                (panel as IDisposable)?.Dispose();
            }
        }

        [Test]
        public async Task KayproDisk_Create_Rename_Delete_AndBadName()
        {
            string blank = CpmImage("kaypro/cpm22-kaypro4-blank.img");
            string temp = Path.Combine(Path.GetTempPath(), "rc_cpm_" + Path.GetRandomFileName() + ".img");
            File.Copy(blank, temp, overwrite: true);
            try
            {
                byte[] content = new byte[300];
                for (int i = 0; i < content.Length; i++)
                {
                    content[i] = (byte)('A' + (i % 26));
                }

                using (FileStream stream = new FileStream(temp, FileMode.Open, FileAccess.ReadWrite))
                {
                    CpmDriver driver = new CpmDriver();
                    driver.Mount(stream, readOnly: false);
                    DriverAdapterFileSystemPanel panel =
                        new DriverAdapterFileSystemPanel("test", driver, "urn:retrofs:diskimage:test:/");
                    Assert.That(panel.IsReadOnly, Is.False);
                    await panel.RefreshAsync();
                    int before = panel.Entries.Count;

                    using (MemoryStream src = new MemoryStream(content))
                    {
                        await panel.CreateFileAsync("HELLO.TXT", src);
                    }
                    await panel.RefreshAsync();
                    IFileSystemEntry? hello = Find(panel, "HELLO.TXT");
                    Assert.That(hello, Is.Not.Null, "HELLO.TXT is listed after the copy");
                    AssertCpmContent(driver.ReadFile("HELLO.TXT"), content);

                    await panel.RenameAsync(hello!, "WORLD.TXT");
                    await panel.RefreshAsync();
                    Assert.That(Find(panel, "HELLO.TXT"), Is.Null);
                    IFileSystemEntry? world = Find(panel, "WORLD.TXT");
                    Assert.That(world, Is.Not.Null, "WORLD.TXT is listed after the rename");
                    AssertCpmContent(driver.ReadFile("WORLD.TXT"), content);

                    // A space is not allowed in a CP/M name; the driver's message reaches the caller.
                    using (MemoryStream src = new MemoryStream(content))
                    {
                        Assert.ThrowsAsync<ArgumentException>(async () => await panel.CreateFileAsync("BAD NAME.TXT", src));
                    }

                    await panel.DeleteAsync(world!);
                    await panel.RefreshAsync();
                    Assert.That(panel.Entries.Count, Is.EqualTo(before), "back to where it started after the delete");
                    panel.Dispose();
                }

                // The saved image, opened again, has no trace of the file.
                using (FileStream stream = new FileStream(temp, FileMode.Open, FileAccess.Read))
                {
                    CpmDriver verify = new CpmDriver();
                    verify.Mount(stream, readOnly: true);
                    var listing = verify.ListDirectory("/");
                    string[] names = new string[listing.Length];
                    for (int i = 0; i < listing.Length; i++)
                    {
                        names[i] = listing[i].Name;
                    }
                    Assert.That(names, Has.None.EqualTo("WORLD.TXT").IgnoreCase);
                    Assert.That(names, Has.None.EqualTo("HELLO.TXT").IgnoreCase);
                    verify.Dispose();
                }
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private static async Task<IFileSystemPanel> DriverFactoryBuilderPanel(string path)
        {
            VolumeProviderFactory factory = new VolumeProviderFactory(null, DriverFactoryBuilder.CreateWithAllDrivers());
            return await factory.CreatePanelAsync("file:///" + path.Replace('\\', '/'));
        }

        // CP/M keeps sizes in 128-byte records, so the file reads back padded with Ctrl-Z (1Ah)
        // to the end of its last record.
        private static void AssertCpmContent(byte[] read, byte[] written)
        {
            int padded = (written.Length + 127) / 128 * 128;
            Assert.That(read.Length, Is.EqualTo(padded));
            for (int i = 0; i < written.Length; i++)
            {
                Assert.That(read[i], Is.EqualTo(written[i]), "byte " + i);
            }
            for (int i = written.Length; i < padded; i++)
            {
                Assert.That(read[i], Is.EqualTo((byte)0x1A), "padding byte " + i);
            }
        }

        private static IFileSystemEntry? Find(IFileSystemPanel panel, string name)
        {
            for (int i = 0; i < panel.Entries.Count; i++)
            {
                if (string.Equals(panel.Entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return panel.Entries[i];
                }
            }
            return null;
        }

        private static string CpmImage(string relative)
        {
            string? root = Environment.GetEnvironmentVariable(ImagesVariable);
            if (string.IsNullOrEmpty(root))
            {
                Assert.Ignore(ImagesVariable + " is not set; it names the folder holding cpm/" + relative);
            }
            string path = Path.Combine(root!, "cpm", relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                Assert.Ignore("not found: " + path);
            }
            return path;
        }

        private static string? FindDemoImage(string relative)
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
