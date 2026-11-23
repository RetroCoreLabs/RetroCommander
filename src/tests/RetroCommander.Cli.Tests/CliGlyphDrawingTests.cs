//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Cli.Tests
{
    /// <summary>
    /// Names drawn from a character ROM in braille. The ROM here is made up inside the test (no
    /// real ROM is in the repository); its glyphs are chosen so each check has one right answer.
    /// </summary>
    [TestFixture]
    [NonParallelizable] // one test sets the process-wide environment variable
    public sealed class CliGlyphDrawingTests
    {
        private const char Blank = '⠀';
        private const char LeftColumn = '⡇'; // dots 1, 2, 3, 7: the left column of a cell
        private const char Full = '⣿';       // all eight dots

        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc-cli-glyph-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_dir, true);
        }

        private static async Task<(int Code, string Out, string Err)> Run(params string[] args)
        {
            StringWriter o = new StringWriter();
            StringWriter e = new StringWriter();
            int code = await new CliApp(o, e).RunAsync(args);
            return (code, o.ToString(), e.ToString());
        }

        // The smallest blank disk whose driver draws its names, and its layout.
        private static (BlankImageChoice Choice, RawDirectoryLayout Layout) DrawnDisk()
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            BlankImageChoice? best = null;
            RawDirectoryLayout? layout = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (best != null && all[i].Layout.SizeBytes >= best.Layout.SizeBytes)
                {
                    continue;
                }

                using MemoryStream stream = new MemoryStream(all[i].Provider.CreateBlank(all[i].Layout.Id, new Dictionary<string, string>()));
                using IFileSystemDriver driver = all[i].Provider.Mount(stream, true, all[i].Layout.Id);
                if (driver is IRawDirectoryNames raw && raw.RawLayout.GlyphIndex.Length == 256)
                {
                    best = all[i];
                    layout = raw.RawLayout;
                }
            }

            Assert.That(best, Is.Not.Null, "no blank disk draws its names; nothing to test");
            Assert.That(layout!.GlyphWidth, Is.EqualTo(8), "these checks are written for 8 by 8 glyphs");
            Assert.That(layout.GlyphHeight, Is.EqualTo(8));
            return (best!, layout);
        }

        // A made-up ROM: every glyph of every set is blank, except
        //  - in the first set, the glyph of byte `marked` is a solid left-most pixel column;
        //  - in the second set, the glyph of byte `marked` is all pixels on;
        //  - in the first set, the glyph of byte `inverse` is all pixels on.
        private static string Rom(string dir, RawDirectoryLayout layout, byte marked, byte inverse)
        {
            byte[] rom = new byte[layout.CharacterRomSize];
            int first = layout.CharacterSets[0].Offset + (layout.GlyphIndex[marked] * 8);
            for (int r = 0; r < 8; r++)
            {
                rom[first + r] = 0x80;
            }

            if (layout.CharacterSets.Length > 1)
            {
                int second = layout.CharacterSets[1].Offset + (layout.GlyphIndex[marked] * 8);
                for (int r = 0; r < 8; r++)
                {
                    rom[second + r] = 0xFF;
                }
            }

            int inv = layout.CharacterSets[0].Offset + (layout.GlyphIndex[inverse] * 8);
            for (int r = 0; r < 8; r++)
            {
                rom[inv + r] = 0xFF;
            }

            string path = Path.Combine(dir, "rom.bin");
            File.WriteAllBytes(path, rom);
            return path;
        }

        private async Task<string> DiskWithHeader(BlankImageChoice choice, string nameHex)
        {
            string disk = Path.Combine(_dir, "d" + Guid.NewGuid().ToString("N").Substring(0, 6) + choice.Layout.Extension);
            Assert.That((await Run("new", disk, "--disk", choice.Layout.Id)).Code, Is.EqualTo(0));
            (int code, _, string err) = await Run("dirart", "header", disk, nameHex, "00", "--as", choice.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            return disk;
        }

        // The disk name's first byte draws as the ROM's glyph for it: a left pixel column is one
        // left-column cell over two lines, the other three cells of that glyph blank.
        [Test]
        public async Task Ls_WithARom_DrawsEachByteFromItsGlyph()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = DrawnDisk();
            string rom = Rom(_dir, layout, 0x41, 0x01);
            string disk = await DiskWithHeader(choice, "41");

            (int code, string output, string err) = await Run("ls", disk, "--as", choice.Layout.Id, "--charset", rom);
            Assert.That(code, Is.EqualTo(0), err);
            string[] lines = output.Replace("\r", string.Empty).Split('\n');
            string expected = "      " + LeftColumn + Blank + Blank + Blank;
            Assert.That(lines[1], Does.StartWith(expected), "first line of the disk name");
            Assert.That(lines[2], Does.StartWith(expected), "second line of the disk name");
        }

        [Test]
        public async Task Lower_DrawsFromTheSecondSet()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = DrawnDisk();
            if (layout.CharacterSets.Length < 2)
            {
                Assert.Inconclusive("this disk's ROM has one character set");
            }

            string rom = Rom(_dir, layout, 0x41, 0x01);
            string disk = await DiskWithHeader(choice, "41");
            (int code, string output, string err) = await Run("ls", disk, "--as", choice.Layout.Id, "--charset", rom, "--lower");
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(output, Does.Contain(layout.CharacterSets[1].Name));
            Assert.That(output, Does.Contain("      " + Full + Full + Full + Full));
        }

        // A byte whose glyph is a reversed one is drawn from that glyph in the ROM. Here the ROM's
        // glyph for it is all on while the plain glyph is blank, which a bit-flip would also give;
        // the second disk makes them differ: the plain glyph is marked and the reversed one is not.
        [Test]
        public async Task ReversedGlyphs_AreReadFromTheRom_NotFlipped()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = DrawnDisk();
            byte reversedByte = 0;
            for (int b = 0; b < 256; b++)
            {
                if (layout.GlyphIndex[b] >= 0x80 && layout.GlyphIndex[b] - 0x80 == layout.GlyphIndex[0x41])
                {
                    reversedByte = (byte)b;
                    break;
                }
            }

            if (reversedByte == 0)
            {
                Assert.Inconclusive("this layout has no reversed copy of the marked glyph");
            }

            // The plain glyph is a left column; its reversed glyph in this ROM is blank, not the
            // flip of a left column. (The ROM's all-on glyph goes to the space, out of the way.)
            string rom = Rom(_dir, layout, 0x41, 0x20);
            string disk = await DiskWithHeader(choice, reversedByte.ToString("X2"));
            (int code, string output, string err) = await Run("ls", disk, "--as", choice.Layout.Id, "--charset", rom);
            Assert.That(code, Is.EqualTo(0), err);
            string[] lines = output.Replace("\r", string.Empty).Split('\n');
            Assert.That(lines[1], Does.StartWith("      " + Blank + Blank + Blank + Blank));
        }

        [Test]
        public async Task WrongSizeOrMissingRom_IsRefused()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = DrawnDisk();
            string disk = await DiskWithHeader(choice, "41");
            string small = Path.Combine(_dir, "small.bin");
            File.WriteAllBytes(small, new byte[layout.CharacterRomSize - 1]);

            (int code, _, string err) = await Run("ls", disk, "--as", choice.Layout.Id, "--charset", small);
            Assert.That(code, Is.EqualTo(1));
            Assert.That(err, Does.Contain(layout.CharacterRomDescription));
            (code, _, err) = await Run("ls", disk, "--as", choice.Layout.Id, "--charset", Path.Combine(_dir, "none.bin"));
            Assert.That(code, Is.EqualTo(1));
            Assert.That(err, Does.Contain("no character ROM file"));
        }

        [Test]
        public async Task TheEnvironmentVariable_NamesTheRom_AndNoRomListsAsBefore()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = DrawnDisk();
            string rom = Rom(_dir, layout, 0x41, 0x01);
            string disk = await DiskWithHeader(choice, "41");

            (_, string plain, _) = await Run("ls", disk, "--as", choice.Layout.Id);
            Assert.That(plain, Does.Not.Contain(LeftColumn.ToString()));

            string? before = Environment.GetEnvironmentVariable(GlyphDrawing.CharsetVariable);
            Environment.SetEnvironmentVariable(GlyphDrawing.CharsetVariable, rom);
            try
            {
                (int code, string drawn, string err) = await Run("ls", disk, "--as", choice.Layout.Id);
                Assert.That(code, Is.EqualTo(0), err);
                Assert.That(drawn, Does.Contain(LeftColumn.ToString()));
            }
            finally
            {
                Environment.SetEnvironmentVariable(GlyphDrawing.CharsetVariable, before);
            }
        }
    }
}
