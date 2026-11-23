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
        public async Task Help_ListsEveryCommand()
        {
            (int code, string output, _) = await Run("--help");
            Assert.That(code, Is.EqualTo(0));
            string[] commands =
            {
                "formats", "new", "ls", "info", "copy-out", "copy-in", "conversions", "copy", "move",
                "mkdir", "rm", "rename", "props", "view", "convert", "assoc", "script"
            };
            for (int i = 0; i < commands.Length; i++)
            {
                Assert.That(output, Does.Contain("  " + commands[i] + " "), commands[i]);
            }
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
                Directory.CreateDirectory(back);
                (code, _, err) = await Run("copy-out", disk + ":" + onDisk, back, "--as", id);
                Assert.That(code, Is.EqualTo(0), id + " copy-out: " + err);
                // The file, plus a .xat only if the driver declares properties.
                string[] outFiles = Directory.GetFiles(back);
                string? dataFile = null;
                for (int f = 0; f < outFiles.Length; f++)
                {
                    if (!outFiles[f].EndsWith(".xat", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.That(dataFile, Is.Null, id + ": more than one file copied out");
                        dataFile = outFiles[f];
                    }
                }
                Assert.That(dataFile, Is.Not.Null, id);
                byte[] got = File.ReadAllBytes(dataFile!);
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

        // Every conversion every disk format offers: plain text copied in and out through it comes
        // back as it went in. The text has no line ends, so no line-end option matters.
        //
        // A conversion belongs to the format's provider, not to a disk size, so each provider is
        // tried once, on its smallest blank disk. The first version made every layout, the ND hard
        // disks of 300 to 600 MB included; those tested nothing more, and on 2026-11-23 one of them
        // failed with OutOfMemoryException while the machine had 6 GB of commit space left.
        [Test]
        public async Task EveryOfferedConversion_CopyInThenOut_GivesTheTextBack()
        {
            string host = Path.Combine(_dir, "NOTE.TXT");
            File.WriteAllText(host, "HELLO World 123");
            BlankImageChoice[] all = BlankImageCatalogue.List();
            int tried = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Provider is not RetroIO.Storage.Core.ITransferConversionProvider provider)
                {
                    continue;
                }
                bool smallest = true;
                for (int j = 0; j < all.Length; j++)
                {
                    if (ReferenceEquals(all[j].Provider, all[i].Provider)
                        && (all[j].Layout.SizeBytes < all[i].Layout.SizeBytes
                            || (all[j].Layout.SizeBytes == all[i].Layout.SizeBytes && j < i)))
                    {
                        smallest = false;
                        break;
                    }
                }
                if (!smallest)
                {
                    continue;
                }
                string id = all[i].Layout.Id;
                for (int c = 0; c < provider.Conversions.Count; c++)
                {
                    RetroIO.Storage.Core.TransferConversion conv = provider.Conversions[c];
                    if (conv.Directions != RetroIO.Storage.Core.TransferDirections.Both)
                    {
                        continue;
                    }
                    string disk = Path.Combine(_dir, "c" + i + "-" + c + all[i].Layout.Extension);
                    await Run("new", disk, "--disk", id);
                    (int code, string copied, string err) = await Run("copy-in", host, disk, "--as", id, "--convert", conv.Id);
                    Assert.That(code, Is.EqualTo(0), id + " " + conv.Id + " in: " + err);
                    int from = copied.IndexOf(" -> ", StringComparison.Ordinal) + 4;
                    string onDisk = copied.Substring(from, copied.IndexOf(" (", from, StringComparison.Ordinal) - from);

                    string back = Path.Combine(_dir, "cb" + i + "-" + c);
                    Directory.CreateDirectory(back);
                    (code, _, err) = await Run("copy-out", disk + ":" + onDisk, back, "--as", id, "--convert", conv.Id);
                    Assert.That(code, Is.EqualTo(0), id + " " + conv.Id + " out: " + err);
                    string[] files = Directory.GetFiles(back, "*", SearchOption.TopDirectoryOnly);
                    string text = string.Empty;
                    for (int f = 0; f < files.Length; f++)
                    {
                        if (!files[f].EndsWith(".xat", StringComparison.OrdinalIgnoreCase))
                        {
                            text = File.ReadAllText(files[f]);
                        }
                    }
                    Assert.That(text, Is.EqualTo("HELLO World 123"), id + " " + conv.Id);
                    tried++;
                }
            }
            Assert.That(tried, Is.GreaterThan(0), "no blank disk offers a conversion; nothing was tested");

            // Every two-way conversion of every provider was tried once: none was skipped by
            // picking only the smallest disk.
            List<object> seen = new List<object>();
            int expected = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Provider is RetroIO.Storage.Core.ITransferConversionProvider p && !seen.Contains(p))
                {
                    seen.Add(p);
                    for (int c = 0; c < p.Conversions.Count; c++)
                    {
                        if (p.Conversions[c].Directions == RetroIO.Storage.Core.TransferDirections.Both)
                        {
                            expected++;
                        }
                    }
                }
            }
            Assert.That(tried, Is.EqualTo(expected), "every conversion of every format");
        }

        [Test]
        public async Task Convert_NotOfferedByTheDisk_ExitCode2()
        {
            BlankImageChoice first = BlankImageCatalogue.List()[0];
            string disk = Path.Combine(_dir, "nc" + first.Layout.Extension);
            await Run("new", disk, "--disk", first.Layout.Id);
            string host = Path.Combine(_dir, "F");
            File.WriteAllText(host, "x");
            (int code, _, string err) = await Run("copy-in", host, disk, "--as", first.Layout.Id, "--convert", "no-such-conversion");
            Assert.That(code, Is.EqualTo(2));
            Assert.That(err, Does.Contain("no-such-conversion"));
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
                    if (all[i].Provider is RetroIO.Storage.Core.ITransferConversionProvider conversions)
                    {
                        for (int c = 0; c < conversions.Conversions.Count; c++)
                        {
                            Assert.That(src, Does.Not.Contain("\"" + conversions.Conversions[c].Id + "\""), Path.GetFileName(files[f]) + " hard-codes a conversion id");
                        }
                    }
                }
            }
        }
    }
}
