//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroCommander.Tests.Shared;

namespace RetroCommander.Cli.Tests
{
    /// <summary>
    /// copy, move, mkdir, rm, rename, props, view, convert, assoc, script and the prompt.
    /// The disks are blank disks the drivers describe; no format is named here.
    /// </summary>
    [TestFixture]
    public sealed class CliCommandTests
    {
        private string _dir = string.Empty;
        private BlankImageChoice _disk = null!;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc-cli-cmd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _disk = BlankImageCatalogue.List()[0];
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_dir, true);
        }

        private static async Task<(int Code, string Out, string Err)> RunWithInput(string? input, params string[] args)
        {
            StringWriter o = new StringWriter();
            StringWriter e = new StringWriter();
            int code = await new CliApp(o, e, input == null ? null : new StringReader(input)).RunAsync(args);
            return (code, o.ToString(), e.ToString());
        }

        private static Task<(int Code, string Out, string Err)> Run(params string[] args) => RunWithInput(null, args);

        // A new blank disk of the first layout, holding one file NOTE with known bytes; returns
        // the disk path and the file's name on the disk as copy-in reported it.
        private async Task<(string Disk, string Name)> DiskWithFile(string disk)
        {
            string path = Path.Combine(_dir, disk + _disk.Layout.Extension);
            Assert.That((await Run("new", path, "--disk", _disk.Layout.Id)).Code, Is.EqualTo(0));
            string host = Path.Combine(_dir, "NOTE");
            File.WriteAllText(host, "NOTE BYTES");
            (int code, string copied, string err) = await Run("copy-in", host, path, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            int from = copied.IndexOf(" -> /", StringComparison.Ordinal) + 5;
            return (path, copied.Substring(from, copied.IndexOf(" (", from, StringComparison.Ordinal) - from));
        }

        private async Task<string> Listing(string disk)
        {
            (int code, string output, string err) = await Run("ls", disk, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            return output;
        }

        private async Task<string> ReadBack(string disk, string name)
        {
            string back = Path.Combine(_dir, "back-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(back);
            (int code, _, string err) = await Run("copy-out", disk + ":" + name, back, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            string[] files = Directory.GetFiles(back);
            for (int i = 0; i < files.Length; i++)
            {
                if (!files[i].EndsWith(".xat", StringComparison.OrdinalIgnoreCase))
                {
                    return File.ReadAllText(files[i]);
                }
            }
            Assert.Fail("nothing copied out");
            return string.Empty;
        }

        [Test]
        public async Task Copy_BetweenTwoDisks_TheBytesArrive()
        {
            (string a, string name) = await DiskWithFile("a");
            string b = Path.Combine(_dir, "b" + _disk.Layout.Extension);
            await Run("new", b, "--disk", _disk.Layout.Id);

            (int code, _, string err) = await Run("copy", a + ":" + name, b, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(await ReadBack(b, name), Does.StartWith("NOTE BYTES"));
            Assert.That(await Listing(a), Does.Contain(name), "copy keeps the original");
        }

        [Test]
        public async Task Move_BetweenTwoDisks_TheOriginalIsGone()
        {
            (string a, string name) = await DiskWithFile("a");
            string b = Path.Combine(_dir, "b" + _disk.Layout.Extension);
            await Run("new", b, "--disk", _disk.Layout.Id);

            (int code, _, string err) = await Run("move", a + ":" + name, b, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(await Listing(b), Does.Contain(name));
            Assert.That(await Listing(a), Does.Not.Contain(name));
        }

        // Measured before the guard: this emptied the disk.
        [Test]
        public async Task MoveOrCopy_IntoTheFolderItIsIn_RefusedAndTheFileKept()
        {
            (string a, string name) = await DiskWithFile("a");
            (int code, _, _) = await Run("move", a + ":" + name, a + ":/", "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(2));
            (code, _, _) = await Run("copy", a + ":" + name, a, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(2));
            Assert.That(await ReadBack(a, name), Does.StartWith("NOTE BYTES"));
        }

        [Test]
        public async Task Rm_TheFileIsGone()
        {
            (string a, string name) = await DiskWithFile("a");
            (int code, _, string err) = await Run("rm", a + ":" + name, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(await Listing(a), Does.Not.Contain(name));
        }

        [Test]
        public async Task Rename_TheNewNameIsListed()
        {
            (string a, string name) = await DiskWithFile("a");
            (int code, _, string err) = await Run("rename", a + ":" + name, "OTHER", "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            string listing = await Listing(a);
            Assert.That(listing, Does.Contain("OTHER"));
            Assert.That(listing, Does.Not.Contain(name));
        }

        // Folders on the first blank disk whose driver can make one. Red while BUGS.md B3 is open:
        // a FAT driver gives a file in a folder the path of the root, so deleting it fails.
        [Test]
        public async Task Mkdir_CopyIntoTheFolder_ListAndDeleteThere()
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            string host = Path.Combine(_dir, "INSIDE");
            File.WriteAllText(host, "IN A FOLDER");
            for (int i = 0; i < all.Length; i++)
            {
                string disk = Path.Combine(_dir, "f" + i + all[i].Layout.Extension);
                string id = all[i].Layout.Id;
                await Run("new", disk, "--disk", id);
                if ((await Run("mkdir", disk + ":/SUB", "--as", id)).Code != 0)
                {
                    continue;
                }
                (int code, _, string err) = await Run("copy-in", host, disk + ":/SUB", "--as", id);
                Assert.That(code, Is.EqualTo(0), id + ": " + err);
                (code, string listing, err) = await Run("ls", disk + ":/SUB", "--as", id);
                Assert.That(code, Is.EqualTo(0), id + ": " + err);
                Assert.That(listing, Does.Contain("INSIDE"), id);
                (code, _, err) = await Run("rm", disk + ":/SUB/INSIDE", "--as", id);
                Assert.That(code, Is.EqualTo(0), id + ": " + err);
                (_, listing, _) = await Run("ls", disk + ":/SUB", "--as", id);
                Assert.That(listing, Does.Not.Contain("INSIDE"), id);
                return;
            }
            Assert.Fail("no blank disk can hold a folder; nothing was tested");
        }

        [Test]
        public async Task Props_ShowsTheFileName()
        {
            (string a, string name) = await DiskWithFile("a");
            (int code, string output, string err) = await Run("props", a + ":" + name, "--as", _disk.Layout.Id);
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(output, Does.Contain(name));
        }

        [Test]
        public async Task View_TextAsText_HexWhenAsked()
        {
            (string a, string name) = await DiskWithFile("a");
            (int code, string text, string err) = await Run("view", a + ":" + name, "--as", _disk.Layout.Id, "--text");
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(text, Does.Contain("NOTE BYTES"));
            (code, string hex, err) = await Run("view", a + ":" + name, "--as", _disk.Layout.Id, "--hex");
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(hex, Does.StartWith("00000000  4E 4F 54 45 20"));
        }

        [Test]
        public async Task Convert_RawToVhdAndBack_SameBytes()
        {
            byte[] raw = new byte[40 * 2 * 9 * 512];
            new Random(7).NextBytes(raw);
            string img = Path.Combine(_dir, "disk.img");
            File.WriteAllBytes(img, raw);

            (int code, _, string err) = await Run("convert", img, Path.Combine(_dir, "disk.vhd"), "--chs", "40,2,9");
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(new FileInfo(Path.Combine(_dir, "disk.vhd")).Length, Is.EqualTo(raw.Length + 512), "a fixed VHD is the bytes plus a 512-byte footer");

            (code, _, err) = await Run("convert", Path.Combine(_dir, "disk.vhd"), Path.Combine(_dir, "again.img"));
            Assert.That(code, Is.EqualTo(0), err);
            Assert.That(File.ReadAllBytes(Path.Combine(_dir, "again.img")), Is.EqualTo(raw));
        }

        [Test]
        public async Task Convert_RawWithNoShape_AsksForChs()
        {
            string img = Path.Combine(_dir, "zero.img");
            File.WriteAllBytes(img, new byte[40 * 2 * 9 * 512]);
            (int code, _, string err) = await Run("convert", img, Path.Combine(_dir, "zero.vhd"));
            Assert.That(code, Is.EqualTo(2));
            Assert.That(err, Does.Contain("--chs"));
        }

        [Test]
        [Platform("Win")]
        [SupportedOSPlatform("windows")]
        public void Assoc_AddListRemove_InAThrowAwayRegistryKey()
        {
            string exe = Path.Combine(_dir, "RetroCommander.exe");
            File.WriteAllText(exe, "stand-in program");
            string ext = KnownDiskImageTypes.Extensions[0];
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                StringWriter o = new StringWriter();
                StringWriter e = new StringWriter();
                CliApp app = new CliApp(o, e)
                {
                    Associations = path => new FileAssociationService(classes, path, notifyShell: false)
                };
                Assert.That(app.RunAsync(new[] { "assoc", "add", ext, "--exe", exe }).GetAwaiter().GetResult(), Is.EqualTo(0), e.ToString());
                using (FileAssociationService check = new FileAssociationService(classes, exe, notifyShell: false))
                {
                    Assert.That(check.GetState(ext), Is.EqualTo(FileAssociationState.Linked));
                }
                Assert.That(app.RunAsync(new[] { "assoc", "list", "--exe", exe }).GetAwaiter().GetResult(), Is.EqualTo(0));
                Assert.That(o.ToString(), Does.Contain(ext).And.Contain("linked"));
                Assert.That(app.RunAsync(new[] { "assoc", "remove", ext, "--exe", exe }).GetAwaiter().GetResult(), Is.EqualTo(0));
                using (FileAssociationService check = new FileAssociationService(classes, exe, notifyShell: false))
                {
                    Assert.That(check.GetState(ext), Is.EqualTo(FileAssociationState.NotLinked));
                }
                Assert.That(app.RunAsync(new[] { "assoc", "add", ".notadisk", "--exe", exe }).GetAwaiter().GetResult(), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task Script_RunsEachLine_StopsAtTheFirstFailure()
        {
            string disk = Path.Combine(_dir, "s" + _disk.Layout.Extension);
            string script = Path.Combine(_dir, "run.txt");
            File.WriteAllLines(script, new[]
            {
                "# a note",
                "new \"" + disk + "\" --disk " + _disk.Layout.Id,
                "",
                "ls \"" + disk + "\" --as " + _disk.Layout.Id,
                "nosuchcommand",
                "rm \"" + disk + ":NEVER\""
            });
            (int code, _, string err) = await Run("script", script);
            Assert.That(code, Is.EqualTo(2));
            Assert.That(err, Does.Contain("line 5"));
            Assert.That(File.Exists(disk), Is.True, "the lines before the failure ran");
        }

        [Test]
        public async Task Prompt_RunsCommandsUntilExit_AndGoesOnAfterAFailure()
        {
            string disk = Path.Combine(_dir, "p" + _disk.Layout.Extension);
            string input = "nosuchcommand\nnew \"" + disk + "\" --disk " + _disk.Layout.Id + "\nexit\nnew never.img --disk " + _disk.Layout.Id + "\n";
            (int code, string output, string err) = await RunWithInput(input);
            Assert.That(code, Is.EqualTo(0));
            Assert.That(err, Does.Contain("nosuchcommand"));
            Assert.That(File.Exists(disk), Is.True);
            Assert.That(output, Does.Contain("rc> "));
        }

        [Test]
        public void SplitLine_QuotesKeepSpaces()
        {
            List<string> words = CliApp.SplitLine("copy-in \"My File.txt\" disk.img:/A B");
            Assert.That(words, Is.EqualTo(new[] { "copy-in", "My File.txt", "disk.img:/A", "B" }));
            Assert.Throws<CliException>(() => CliApp.SplitLine("ls \"open"));
        }
    }
}
