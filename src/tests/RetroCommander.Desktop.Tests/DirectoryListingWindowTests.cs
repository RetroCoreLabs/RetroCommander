//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// The Directory listing window on a blank disk whose driver draws its names, with a character ROM
// made up inside the test (no real ROM is in the repository). The window is given the ROM path, so
// the user's own settings file is never read or written.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class DirectoryListingWindowTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc_listing_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        // The smallest blank disk whose driver draws its names, mounted in memory.
        private static IRawDirectoryNames DrawnDisk(out IFileSystemDriver driver)
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            BlankImageChoice? best = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (best != null && all[i].Layout.SizeBytes >= best.Layout.SizeBytes)
                {
                    continue;
                }

                using MemoryStream probe = new MemoryStream(all[i].Provider.CreateBlank(all[i].Layout.Id, new Dictionary<string, string>()));
                using IFileSystemDriver d = all[i].Provider.Mount(probe, true, all[i].Layout.Id);
                if (d is IRawDirectoryNames r && GlyphRaster.CanDraw(r.RawLayout))
                {
                    best = all[i];
                }
            }

            Assert.That(best, Is.Not.Null, "no blank disk draws its names; nothing to test");
            MemoryStream stream = new MemoryStream();
            byte[] image = best!.Provider.CreateBlank(best.Layout.Id, new Dictionary<string, string>());
            stream.Write(image, 0, image.Length);
            stream.Position = 0;
            driver = best.Provider.Mount(stream, false, best.Layout.Id);
            return (IRawDirectoryNames)driver;
        }

        // A ROM whose every glyph is blank except byte 41h's in the first set: a left pixel column.
        private string Rom(RawDirectoryLayout layout)
        {
            byte[] rom = new byte[layout.CharacterRomSize];
            int start = layout.CharacterSets[0].Offset + (layout.GlyphIndex[0x41] * layout.GlyphHeight);
            for (int r = 0; r < layout.GlyphHeight; r++)
            {
                rom[start + r] = 0x80;
            }

            string path = Path.Combine(_dir, "rom.bin");
            File.WriteAllBytes(path, rom);
            return path;
        }

        // The colour of one pixel of a drawn name, as a BGRA word.
        private static uint PixelAt(Image image, int x, int y)
        {
            WriteableBitmap bitmap = (WriteableBitmap)image.Source!;
            using ILockedFramebuffer frame = bitmap.Lock();
            int[] one = new int[1];
            Marshal.Copy(frame.Address + (y * frame.RowBytes) + (x * 4), one, 0, 1);
            return unchecked((uint)one[0]);
        }

        private static Image NameImage(DirectoryListingWindow window, int row)
        {
            Grid grid = (Grid)window.ListingPanel.Children[row];
            for (int i = 0; i < grid.Children.Count; i++)
            {
                if (grid.Children[i] is Image image)
                {
                    return image;
                }
            }

            Assert.Fail("row " + row + " has no drawn name");
            return null!;
        }

        [AvaloniaTest]
        public void WithARom_EveryEntryIsDrawnFromItsGlyphs()
        {
            IRawDirectoryNames raw = DrawnDisk(out IFileSystemDriver driver);
            using (driver)
            {
                RawDirectoryLayout layout = raw.RawLayout;
                byte[] name = new byte[layout.NameLength];
                Array.Fill(name, layout.PadByte);
                name[0] = 0x41;
                raw.InsertSeparator(0, name);

                DirectoryListingWindow window = new DirectoryListingWindow(raw, "test", Rom(layout), null);
                Assert.That(window.MessageText.IsVisible, Is.False, window.MessageText.Text);
                Assert.That(window.ListingPanel.Children.Count, Is.EqualTo(1 + raw.ReadRawDirectory().Length), "the header, then one row per entry");

                // Row 1 is the entry: its first glyph is a left column, so the left pixel is ink and
                // the next one is paper, on every glyph row.
                Image drawn = NameImage(window, 1);
                uint ink = PixelAt(drawn, 0, 0);
                uint paper = PixelAt(drawn, 1, 0);
                Assert.That(ink, Is.Not.EqualTo(paper));
                for (int y = 0; y < layout.GlyphHeight; y++)
                {
                    Assert.That(PixelAt(drawn, 0, y), Is.EqualTo(ink), "row " + y);
                    Assert.That(PixelAt(drawn, 1, y), Is.EqualTo(paper), "row " + y);
                }

                // The second glyph is the pad byte, blank in this ROM.
                Assert.That(PixelAt(drawn, layout.GlyphWidth, 0), Is.EqualTo(paper));
                Assert.That(drawn.Width, Is.EqualTo(layout.NameLength * layout.GlyphWidth * 2), "each ROM pixel is 2 screen pixels");
            }
        }

        [AvaloniaTest]
        public void NoRom_AsksForOne_AndDrawsNothing()
        {
            IRawDirectoryNames raw = DrawnDisk(out IFileSystemDriver driver);
            using (driver)
            {
                DirectoryListingWindow window = new DirectoryListingWindow(raw, "test", null, null);
                Assert.That(window.MessageText.IsVisible, Is.True);
                Assert.That(window.MessageText.Text, Does.Contain(raw.RawLayout.CharacterRomDescription));
                Assert.That(window.ListingPanel.Children.Count, Is.EqualTo(0));
            }
        }

        [AvaloniaTest]
        public void WrongSizeRom_SaysWhatIsNeeded_AndDrawsNothing()
        {
            IRawDirectoryNames raw = DrawnDisk(out IFileSystemDriver driver);
            using (driver)
            {
                string small = Path.Combine(_dir, "small.bin");
                File.WriteAllBytes(small, new byte[raw.RawLayout.CharacterRomSize - 1]);
                DirectoryListingWindow window = new DirectoryListingWindow(raw, "test", small, null);
                Assert.That(window.MessageText.IsVisible, Is.True);
                Assert.That(window.MessageText.Text, Does.Contain(raw.RawLayout.CharacterRomDescription));
                Assert.That(window.ListingPanel.Children.Count, Is.EqualTo(0));
            }
        }
    }
}
