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
using RetroCommander.Core.Providers;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Parity tests for the NATIVE (Windows / Linux OS) filesystem panel, mirroring the operations
    /// that are validated for NDFS and CBM panels: open, read, write/create, rename and delete.
    ///
    /// The point is that the exact same UI-facing surface (IFileSystemPanel + IFileSystemEntry) that
    /// the dual-pane app drives works identically on the host filesystem. These run against a real
    /// temporary directory (created and cleaned up per test), so they are equally valid on Windows
    /// and Linux -- Path.Combine / FileInfo are cross-platform.
    /// </summary>
    [TestFixture]
    [Category("Native")]
    public class NativeFileSystemPanelTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            // A fresh, empty scratch directory for each test.
            _tempDir = Path.Combine(Path.GetTempPath(), "rfs_native_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; never fail a test because the OS is holding a handle.
            }
        }

        // Deterministic content so read-back comparisons are exact.
        private static byte[] Pattern(int length)
        {
            byte[] b = new byte[length];
            for (int i = 0; i < length; i++)
            {
                b[i] = (byte)((i * 7 + 3) & 0xFF);
            }
            return b;
        }

        // Find an entry by name without LINQ (project rule: no LINQ).
        private static IFileSystemEntry? FindEntry(IFileSystemPanel panel, string name)
        {
            for (int i = 0; i < panel.Entries.Count; i++)
            {
                if (string.Equals(panel.Entries[i].Name, name, StringComparison.Ordinal))
                {
                    return panel.Entries[i];
                }
            }
            return null;
        }

        [Test]
        public async Task NativePanel_StartsEmpty_AndIsWritable()
        {
            NativeFileSystemPanel panel = new NativeFileSystemPanel("native", _tempDir);

            Assert.That(panel.FileSystemType, Is.EqualTo("Windows"));
            Assert.That(panel.IsReadOnly, Is.False, "the host filesystem panel is read-write");
            Assert.That(panel.IsRemote, Is.False);

            await panel.RefreshAsync();
            Assert.That(panel.Entries.Count, Is.EqualTo(0), "fresh scratch directory is empty");
        }

        // The headline parity test: create -> read (open) -> rename -> delete, exactly like the
        // CBM/NDFS round-trip tests, but on the native filesystem.
        [Test]
        public async Task NativePanel_Create_Read_Rename_Delete_RoundTrip()
        {
            NativeFileSystemPanel panel = new NativeFileSystemPanel("native", _tempDir);
            await panel.RefreshAsync();

            byte[] content = Pattern(4096);

            // --- Create (F5 Copy target side) ---
            IFileSystemEntry created;
            using (MemoryStream src = new MemoryStream(content))
            {
                created = await panel.CreateFileAsync("hello.txt", src);
            }
            Assert.That(created, Is.Not.Null);
            Assert.That(created.IsDirectory, Is.False);
            Assert.That(created.Size, Is.EqualTo(content.Length));

            await panel.RefreshAsync();
            Assert.That(panel.Entries.Count, Is.EqualTo(1));
            Assert.That(panel.Entries[0].Name, Is.EqualTo("hello.txt"));

            // --- Open + Read (this is the F3/View path that was reported broken) ---
            IFileSystemEntry? entry = FindEntry(panel, "hello.txt");
            Assert.That(entry, Is.Not.Null, "created file must be listed");
            byte[] readBack;
            using (Stream stream = await entry!.OpenReadAsync())
            using (MemoryStream ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                readBack = ms.ToArray();
            }
            Assert.That(readBack, Is.EqualTo(content), "OpenReadAsync returns the exact bytes written");

            // --- Rename ---
            await panel.RenameAsync(entry!, "world.txt");
            await panel.RefreshAsync();
            Assert.That(panel.Entries.Count, Is.EqualTo(1));
            Assert.That(FindEntry(panel, "hello.txt"), Is.Null, "old name is gone after rename");
            IFileSystemEntry? renamed = FindEntry(panel, "world.txt");
            Assert.That(renamed, Is.Not.Null, "new name is present after rename");

            // Content survives the rename.
            using (Stream stream = await renamed!.OpenReadAsync())
            using (MemoryStream ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                Assert.That(ms.ToArray(), Is.EqualTo(content), "content survives native rename");
            }

            // --- Delete (F8) ---
            await panel.DeleteAsync(renamed!);
            await panel.RefreshAsync();
            Assert.That(panel.Entries.Count, Is.EqualTo(0), "directory is empty after delete");

            // And the underlying file is really gone on disk.
            Assert.That(File.Exists(Path.Combine(_tempDir, "world.txt")), Is.False);
        }

        // Reading a text file specifically: regression guard for the reported "F3/View doesn't work
        // on txt files in the Windows filesystem" bug. A native .txt must open and read its text.
        [Test]
        public async Task NativePanel_OpenRead_TextFile_ReturnsText()
        {
            NativeFileSystemPanel panel = new NativeFileSystemPanel("native", _tempDir);
            await panel.RefreshAsync();

            const string text = "The quick brown fox\r\njumps over the lazy dog\r\n";
            byte[] utf8 = Encoding.UTF8.GetBytes(text);

            using (MemoryStream src = new MemoryStream(utf8))
            {
                await panel.CreateFileAsync("readme.txt", src);
            }
            await panel.RefreshAsync();

            IFileSystemEntry? entry = FindEntry(panel, "readme.txt");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.IsDirectory, Is.False, "a .txt must NOT report as a directory");

            string roundTrip;
            using (Stream stream = await entry.OpenReadAsync())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                roundTrip = await reader.ReadToEndAsync();
            }
            Assert.That(roundTrip, Is.EqualTo(text), "native text file reads back its exact text");
        }

        // Overwrite semantics: writing the same name again replaces contents (parity with re-create).
        [Test]
        public async Task NativePanel_Overwrite_ReplacesContent()
        {
            NativeFileSystemPanel panel = new NativeFileSystemPanel("native", _tempDir);
            await panel.RefreshAsync();

            byte[] first = Pattern(100);
            byte[] second = Pattern(250);

            using (MemoryStream src = new MemoryStream(first))
            {
                await panel.CreateFileAsync("data.bin", src);
            }
            using (MemoryStream src = new MemoryStream(second))
            {
                await panel.CreateFileAsync("data.bin", src); // same name -> File.Create truncates
            }
            await panel.RefreshAsync();

            Assert.That(panel.Entries.Count, Is.EqualTo(1), "overwrite must not create a duplicate");
            IFileSystemEntry? entry = FindEntry(panel, "data.bin");
            Assert.That(entry, Is.Not.Null);

            using (Stream stream = await entry!.OpenReadAsync())
            using (MemoryStream ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                Assert.That(ms.ToArray(), Is.EqualTo(second), "second write fully replaces the first");
            }
        }

        // Directory operations + navigation parity (create dir, navigate into, navigate up).
        [Test]
        public async Task NativePanel_CreateDirectory_NavigateInto_And_Up()
        {
            NativeFileSystemPanel panel = new NativeFileSystemPanel("native", _tempDir);
            await panel.RefreshAsync();

            IFileSystemEntry dirEntry = await panel.CreateDirectoryAsync("sub");
            Assert.That(dirEntry.IsDirectory, Is.True);

            await panel.RefreshAsync();
            IFileSystemEntry? sub = FindEntry(panel, "sub");
            Assert.That(sub, Is.Not.Null, "created directory is listed");

            // Navigate into the subdirectory and create a file there.
            await panel.NavigateIntoAsync(sub!);
            Assert.That(panel.CurrentPath, Is.EqualTo(Path.Combine(_tempDir, "sub")));

            using (MemoryStream src = new MemoryStream(Pattern(32)))
            {
                await panel.CreateFileAsync("inner.dat", src);
            }
            await panel.RefreshAsync();
            Assert.That(FindEntry(panel, "inner.dat"), Is.Not.Null, "file created inside subdirectory");

            // Navigate back up to the scratch root.
            Assert.That(panel.CanNavigateUp(), Is.True);
            await panel.NavigateUpAsync();
            Assert.That(panel.CurrentPath, Is.EqualTo(_tempDir));

            // Deleting the directory recursively removes its contents too.
            await panel.RefreshAsync();
            IFileSystemEntry? subAgain = FindEntry(panel, "sub");
            Assert.That(subAgain, Is.Not.Null);
            await panel.DeleteAsync(subAgain!);
            await panel.RefreshAsync();
            Assert.That(FindEntry(panel, "sub"), Is.Null, "recursive delete removes the directory");
        }

        // The selected entry must round-trip through SelectedEntry (this is what the UI selection
        // handler sets and F3/View reads back).
        [Test]
        public async Task NativePanel_SelectedEntry_RoundTrips()
        {
            NativeFileSystemPanel panel = new NativeFileSystemPanel("native", _tempDir);

            using (MemoryStream src = new MemoryStream(Pattern(10)))
            {
                NativeFileSystemPanel seed = new NativeFileSystemPanel("seed", _tempDir);
                await seed.CreateFileAsync("pick.txt", src);
            }
            await panel.RefreshAsync();

            IFileSystemEntry? entry = FindEntry(panel, "pick.txt");
            Assert.That(entry, Is.Not.Null);

            panel.SelectedEntry = entry;
            Assert.That(panel.SelectedEntry, Is.Not.Null);
            Assert.That(panel.SelectedEntry!.Name, Is.EqualTo("pick.txt"));
            Assert.That(panel.SelectedEntry.IsDirectory, Is.False);
        }
    }
}
