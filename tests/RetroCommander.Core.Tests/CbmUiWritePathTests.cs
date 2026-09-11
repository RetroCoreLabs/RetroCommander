//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local.BlockDevices;
using RetroIO.Storage.Local.Drivers;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// End-to-end "UI verification" for CBM writes: exercises the exact panel the RetroCommander UI
    /// uses (DriverAdapterFileSystemPanel) over a real CommodoreDiskDriver on a blank D64, proving
    /// create / delete / rename reach CbmDosFileSystem and persist. This is the GUI-free equivalent
    /// of F5-Copy / F8-Delete / rename in the app.
    /// </summary>
    [TestFixture]
    [Category("CBM")]
    public class CbmUiWritePathTests
    {
        private static string? FindBlankD64()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "demo", "test-images", "c64", "d64", "blank.d64");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private static byte[] Pattern(int length)
        {
            byte[] b = new byte[length];
            for (int i = 0; i < length; i++)
            {
                b[i] = (byte)((i * 7 + 3) & 0xFF);
            }
            return b;
        }

        [Test]
        public async Task UiPanel_D64_Create_Rename_Delete_RoundTrip()
        {
            string? blank = FindBlankD64();
            if (blank == null)
            {
                Assert.Ignore("blank.d64 fixture not present");
                return;
            }

            string temp = Path.Combine(Path.GetTempPath(), "rfs_ui_" + Path.GetRandomFileName() + ".d64");
            File.Copy(blank, temp, overwrite: true);
            try
            {
                byte[] content = Pattern(400);

                using (FileStream stream = new FileStream(temp, FileMode.Open, FileAccess.ReadWrite))
                {
                    CommodoreDiskDriver driver = new CommodoreDiskDriver();
                    driver.Mount(stream, readOnly: false);

                    // The panel is what the dual-pane UI drives.
                    DriverAdapterFileSystemPanel panel =
                        new DriverAdapterFileSystemPanel("test", driver, "urn:retrofs:diskimage:test:/");

                    Assert.That(panel.IsReadOnly, Is.False, "a read-write D64 mount must be writable in the UI");
                    await panel.RefreshAsync();
                    Assert.That(panel.Entries.Count, Is.EqualTo(0), "blank disk starts empty");

                    // --- Create (F5 Copy target side) ---
                    using (MemoryStream src = new MemoryStream(content))
                    {
                        IFileSystemEntry created = await panel.CreateFileAsync("HELLO", src);
                        Assert.That(created, Is.Not.Null);
                    }
                    await panel.RefreshAsync();
                    Assert.That(panel.Entries.Count, Is.EqualTo(1));
                    Assert.That(panel.Entries[0].Name, Is.EqualTo("HELLO"));
                    Assert.That(driver.ReadFile("HELLO"), Is.EqualTo(content), "file readable after UI create");

                    // --- Rename ---
                    await panel.RenameAsync(panel.Entries[0], "WORLD");
                    await panel.RefreshAsync();
                    Assert.That(panel.Entries.Count, Is.EqualTo(1));
                    Assert.That(panel.Entries[0].Name, Is.EqualTo("WORLD"));
                    Assert.That(driver.ReadFile("WORLD"), Is.EqualTo(content), "content survives UI rename");

                    // --- Delete (F8) ---
                    await panel.DeleteAsync(panel.Entries[0]);
                    await panel.RefreshAsync();
                    Assert.That(panel.Entries.Count, Is.EqualTo(0), "directory empty after UI delete");

                    panel.Dispose(); // disposes driver -> flushes to the temp image
                }

                // Re-open the persisted image and confirm it is empty.
                using (FileStream stream2 = new FileStream(temp, FileMode.Open, FileAccess.Read))
                {
                    CommodoreDiskDriver verify = new CommodoreDiskDriver();
                    verify.Mount(stream2, readOnly: true);
                    Assert.That(verify.ListDirectory("/").Length, Is.EqualTo(0), "persisted image is empty");
                    verify.Dispose();
                }
            }
            finally
            {
                File.Delete(temp);
            }
        }
    }
}
