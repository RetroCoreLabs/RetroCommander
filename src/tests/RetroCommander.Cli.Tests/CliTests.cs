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
using RetroCommander.Core.Services;

namespace RetroCommander.Cli.Tests
{
    /// <summary>
    /// retrocommander-cli run in-process: every expectation is taken from the drivers' own metadata.
    /// </summary>
    [TestFixture]
    public sealed class CliTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc-cli-" + Guid.NewGuid().ToString("N"));
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

        [Test]
        public async Task NoCommand_PrintsHelp_ExitCode2()
        {
            (int code, string output, _) = await Run();
            Assert.That(code, Is.EqualTo(2));
            Assert.That(output, Does.Contain("copy-in"));
        }

        [Test]
        public async Task UnknownCommand_ExitCode2()
        {
            (int code, _, string err) = await Run("nosuchcommand");
            Assert.That(code, Is.EqualTo(2));
            Assert.That(err, Does.Contain("nosuchcommand"));
        }

        [Test]
        public async Task Formats_ListsEveryFormatTheDriversPublish()
        {
            (int code, string output, _) = await Run("formats");
            Assert.That(code, Is.EqualTo(0));
            SupportedFormat[] formats = SupportedFormats.List();
            for (int i = 0; i < formats.Length; i++)
            {
                Assert.That(output, Does.Contain(formats[i].Description));
            }
        }

        [Test]
        public async Task NewList_ListsEveryLayoutAndOption()
        {
            (int code, string output, _) = await Run("new", "--list");
            Assert.That(code, Is.EqualTo(0));
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                Assert.That(output, Does.Contain(all[i].Layout.Id));
                for (int k = 0; k < all[i].Layout.Options.Count; k++)
                {
                    Assert.That(output, Does.Contain("--set " + all[i].Layout.Options[k].Id + "="));
                }
            }
        }

        // Every blank disk the drivers describe: made by the CLI, a file copied in, listed, copied
        // out byte for byte. Opened with --as, since some empty disks are the same bytes on
        // several machines.
        [Test]
        public async Task EveryWritableLayout_NewCopyInLsCopyOut_RoundTrips()
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            Assert.That(all, Is.Not.Empty);
            byte[] data = new byte[700];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)('A' + (i % 26));
            }
            string host = Path.Combine(_dir, "DATA");
            File.WriteAllBytes(host, data);

            for (int i = 0; i < all.Length; i++)
            {
                string id = all[i].Layout.Id;
                string disk = Path.Combine(_dir, "d" + i + all[i].Layout.Extension);
                (int code, _, string err) = await Run("new", disk, "--disk", id);
                Assert.That(code, Is.EqualTo(0), id + ": " + err);
                Assert.That(new FileInfo(disk).Length, Is.EqualTo(all[i].Layout.SizeBytes), id);

                (code, string copied, err) = await Run("copy-in", host, disk, "--as", id);
                Assert.That(code, Is.EqualTo(0), id + " copy-in: " + err);

                // Where the file landed and what it is called there is the driver's business
                // (NDFS puts it under the SYSTEM user and writes DATA as DATA:), so the path is
                // taken from what copy-in reports: "copied HOST -> /PATH (N bytes)".
                int from = copied.IndexOf(" -> ", StringComparison.Ordinal) + 4;
                string onDisk = copied.Substring(from, copied.IndexOf(" (", from, StringComparison.Ordinal) - from);
                int slash = onDisk.LastIndexOf('/');
                string folder = slash > 0 ? onDisk.Substring(0, slash) : "/";

                (code, string listing, err) = await Run("ls", disk + ":" + folder, "--as", id);
                Assert.That(code, Is.EqualTo(0), id + " ls: " + err);
                Assert.That(listing, Does.Contain(onDisk.Substring(slash + 1)), id);

                string back = Path.Combine(_dir, "back" + i);
                (code, _, err) = await Run("copy-out", disk + ":" + onDisk, back, "--as", id);
                Assert.That(code, Is.EqualTo(0), id + " copy-out: " + err);
                byte[] got = File.ReadAllBytes(back);
                Assert.That(got.Length, Is.GreaterThanOrEqualTo(data.Length), id);
                Assert.That(got.AsSpan(0, data.Length).SequenceEqual(data), Is.True, id);
            }
        }

        [Test]
        public async Task New_BadOptionValue_GivesTheDriversOwnMessage()
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                for (int k = 0; k < all[i].Layout.Options.Count; k++)
                {
                    RetroIO.Storage.Core.BlankImageOption opt = all[i].Layout.Options[k];
                    string bad = new string('X', 300);
                    string? message = opt.Check(bad);
                    if (message == null)
                    {
                        continue;
                    }
                    (int code, _, string err) = await Run("new", Path.Combine(_dir, "bad"), "--disk", all[i].Layout.Id, "--set", opt.Id + "=" + bad);
                    Assert.That(code, Is.EqualTo(2));
                    Assert.That(err, Does.Contain(message));
                    Assert.That(File.Exists(Path.Combine(_dir, "bad" + all[i].Layout.Extension)), Is.False);
                    return;
                }
            }
            Assert.Fail("no driver option refuses a 300-character value; nothing to test");
        }

        [Test]
        public async Task New_Unformatted_IsAllZeros()
        {
            BlankImageChoice first = BlankImageCatalogue.List()[0];
            string disk = Path.Combine(_dir, "z" + first.Layout.Extension);
            (int code, _, _) = await Run("new", disk, "--disk", first.Layout.Id, "--unformatted");
            Assert.That(code, Is.EqualTo(0));
            byte[] bytes = File.ReadAllBytes(disk);
            Assert.That(bytes.Length, Is.EqualTo(first.Layout.SizeBytes));
            Assert.That(Array.IndexOf(bytes, (byte)0) == 0 && Array.FindIndex(bytes, b => b != 0) < 0, Is.True);
        }

        [Test]
        public async Task New_ExistingFile_Refused()
        {
            BlankImageChoice first = BlankImageCatalogue.List()[0];
            string disk = Path.Combine(_dir, "e" + first.Layout.Extension);
            File.WriteAllText(disk, "keep");
            (int code, _, _) = await Run("new", disk, "--disk", first.Layout.Id);
            Assert.That(code, Is.EqualTo(1));
            Assert.That(File.ReadAllText(disk), Is.EqualTo("keep"));
        }

        [Test]
        public async Task CopyIn_WithRo_Refused()
        {
            BlankImageChoice first = BlankImageCatalogue.List()[0];
            string disk = Path.Combine(_dir, "r" + first.Layout.Extension);
            await Run("new", disk, "--disk", first.Layout.Id);
            string host = Path.Combine(_dir, "F");
            File.WriteAllText(host, "x");
            (int code, _, string err) = await Run("copy-in", host, disk, "--ro");
            Assert.That(code, Is.EqualTo(1));
            Assert.That(err, Does.Contain("--ro"));
        }

        [Test]
        public async Task CopyOut_MissingFile_ExitCode1()
        {
            BlankImageChoice first = BlankImageCatalogue.List()[0];
            string disk = Path.Combine(_dir, "m" + first.Layout.Extension);
            await Run("new", disk, "--disk", first.Layout.Id);
            (int code, _, string err) = await Run("copy-out", disk + ":NOPE", _dir, "--as", first.Layout.Id);
            Assert.That(code, Is.EqualTo(1));
            Assert.That(err, Does.Contain("NOPE"));
        }

        // The CLI must name no format: every machine, layout id, extension, option id and
        // format name comes from the drivers. A quoted one in the source fails this test.
        [Test]
        public void CliCode_NamesNoFormat()
        {
            string dir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "RetroCommander.Cli"));
            string[] files = Directory.GetFiles(dir, "*.cs");
            Assert.That(files, Is.Not.Empty, dir);
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int f = 0; f < files.Length; f++)
            {
                string src = File.ReadAllText(files[f]);
                for (int i = 0; i < all.Length; i++)
                {
                    string[] names = { all[i].Layout.Id, all[i].Layout.Machine, all[i].Layout.Extension, all[i].FileSystemType };
                    for (int n = 0; n < names.Length; n++)
                    {
                        Assert.That(src, Does.Not.Contain("\"" + names[n] + "\""), Path.GetFileName(files[f]) + " hard-codes " + names[n]);
                    }
                    for (int k = 0; k < all[i].Layout.Options.Count; k++)
                    {
                        Assert.That(src, Does.Not.Contain("\"" + all[i].Layout.Options[k].Id + "\""), Path.GetFileName(files[f]) + " hard-codes an option id");
                    }
                }
            }
        }
    }
}
