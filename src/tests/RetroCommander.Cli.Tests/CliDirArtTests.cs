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
    /// dirart: raw directory names, separators and the disk header. The disk is the first blank disk
    /// whose driver offers raw directory names, found by asking the drivers; no format is named.
    /// </summary>
    [TestFixture]
    public sealed class CliDirArtTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc-cli-art-" + Guid.NewGuid().ToString("N"));
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

        // The smallest blank disk whose driver offers raw directory names, and its layout.
        private static (BlankImageChoice Choice, RawDirectoryLayout Layout) ArtDisk()
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
                if (driver is IRawDirectoryNames raw)
                {
                    best = all[i];
                    layout = raw.RawLayout;
                }
            }

            Assert.That(best, Is.Not.Null, "no blank disk offers raw directory names; nothing to test");
            return (best!, layout!);
        }

        [Test]
        public async Task AddNameHeader_ThenList_ShowsTheBytes()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = ArtDisk();
            string id = choice.Layout.Id;
            string disk = Path.Combine(_dir, "art" + choice.Layout.Extension);
            Assert.That((await Run("new", disk, "--disk", id)).Code, Is.EqualTo(0));
            string host = Path.Combine(_dir, "GAME");
            File.WriteAllText(host, "x");
            Assert.That((await Run("copy-in", host, disk, "--as", id)).Code, Is.EqualTo(0));

            (int code, string output, string err) = await Run("dirart", "add", disk, "0", "B0 C0 B0", "--as", id);
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(output, Does.Contain(layout.SeparatorTypeName));
            (code, _, err) = await Run("dirart", "add", disk, "2", "text:THE END", "--as", id);
            Assert.That(code, Is.EqualTo(0), err);
            (code, _, err) = await Run("dirart", "name", disk, "0", "AA", "--as", id);
            Assert.That(code, Is.EqualTo(0), err);
            (code, _, err) = await Run("dirart", "header", disk, "text:ART", "text:Z", "--as", id);
            Assert.That(code, Is.EqualTo(0), err);

            (code, output, err) = await Run("dirart", "list", disk, "--as", id);
            Assert.That(code, Is.EqualTo(0), err);
            string pad = " " + layout.PadByte.ToString("X2");
            Assert.That(output, Does.Contain(layout.CharacterSet));
            Assert.That(output, Does.Contain("  0  " + layout.SeparatorTypeName));
            Assert.That(output, Does.Contain("AA" + pad + pad), "entry 0 renamed and padded");
            Assert.That(output, Does.Contain("54 48 45 20 45 4E 44"), "THE END as bytes");
            Assert.That(output, Does.Contain("THE END"));
            Assert.That(output, Does.Contain("disk name  41 52 54" + pad));
            Assert.That(output, Does.Contain("disk id    5A"));

            // The file is still there and still reads back.
            Assert.That((await Run("ls", disk, "--as", id)).Out, Does.Contain("GAME"));
        }

        [Test]
        public async Task TooLong_BadHex_PastTheEnd_AreRefused()
        {
            (BlankImageChoice choice, RawDirectoryLayout layout) = ArtDisk();
            string id = choice.Layout.Id;
            string disk = Path.Combine(_dir, "bad" + choice.Layout.Extension);
            await Run("new", disk, "--disk", id);

            string tooLong = "text:" + new string('A', layout.NameLength + 1);
            Assert.That((await Run("dirart", "add", disk, "0", tooLong, "--as", id)).Code, Is.EqualTo(2));
            Assert.That((await Run("dirart", "add", disk, "0", "B0C", "--as", id)).Code, Is.EqualTo(2));
            Assert.That((await Run("dirart", "add", disk, "0", "ZZ", "--as", id)).Code, Is.EqualTo(2));
            (int code, _, string err) = await Run("dirart", "add", disk, "5", "B0", "--as", id);
            Assert.That(code, Is.EqualTo(1), err);
            (_, string listing, _) = await Run("dirart", "list", disk, "--as", id);
            Assert.That(listing, Does.Not.Contain("  0  "), "nothing was added");
        }

        [Test]
        public async Task ADiskWithoutRawNames_IsRefused()
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                using MemoryStream stream = new MemoryStream(all[i].Provider.CreateBlank(all[i].Layout.Id, new Dictionary<string, string>()));
                using IFileSystemDriver driver = all[i].Provider.Mount(stream, true, all[i].Layout.Id);
                if (driver is IRawDirectoryNames || all[i].Layout.SizeBytes > 4 * 1024 * 1024)
                {
                    continue;
                }

                string disk = Path.Combine(_dir, "plain" + all[i].Layout.Extension);
                await Run("new", disk, "--disk", all[i].Layout.Id);
                (int code, _, string err) = await Run("dirart", "list", disk, "--as", all[i].Layout.Id);
                Assert.That(code, Is.EqualTo(1));
                Assert.That(err, Does.Contain("does not offer raw directory names"));
                return;
            }

            Assert.Inconclusive("every small blank disk offers raw directory names");
        }

        [Test]
        public void Name_PadsWithThePadByte_TakesHex_AndTurnsTextIntoBytesByTheDriversRule()
        {
            (BlankImageChoice choice, RawDirectoryLayout _) = ArtDisk();
            using MemoryStream stream = new MemoryStream(choice.Provider.CreateBlank(choice.Layout.Id, new Dictionary<string, string>()));
            using IFileSystemDriver driver = choice.Provider.Mount(stream, true, choice.Layout.Id);
            IRawDirectoryNames raw = (IRawDirectoryNames)driver;
            byte pad = raw.RawLayout.PadByte;

            Assert.That(CliApp.Name(raw, "B0 C0", 4, "NAME"), Is.EqualTo(new byte[] { 0xB0, 0xC0, pad, pad }));
            Assert.That(CliApp.Name(raw, "B0C0", 2, "NAME"), Is.EqualTo(new byte[] { 0xB0, 0xC0 }));

            // Text is the driver's business: the CLI pads what the driver gives and adds nothing.
            byte[] text = raw.EncodeText("hello AB");
            byte[] expected = new byte[text.Length + 1];
            text.CopyTo(expected, 0);
            expected[text.Length] = pad;
            Assert.That(CliApp.Name(raw, "text:hello AB", text.Length + 1, "NAME"), Is.EqualTo(expected));

            // A character the driver has no byte for is refused as a usage error, with its message.
            CliException e = Assert.Throws<CliException>(() => CliApp.Name(raw, "text:é", 3, "NAME"))!;
            Assert.That(e.Message, Does.Contain("NAME: "));
        }
    }
}
