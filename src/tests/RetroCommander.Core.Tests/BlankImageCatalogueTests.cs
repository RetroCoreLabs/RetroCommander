//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// File, New Image, below the window: every blank disk the drivers describe can be created
    /// through <see cref="BlankImageCatalogue"/> and opened again as the layout that was picked.
    /// </summary>
    [TestFixture]
    public class BlankImageCatalogueTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rc_blank_" + Guid.NewGuid().ToString("N"));
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

        [Test]
        public void List_HasTheFormatsRetroIOCreates()
        {
            BlankImageChoice[] choices = BlankImageCatalogue.List();
            Assert.That(Ids(choices), Does.Contain("cbm-d64"));
            Assert.That(Ids(choices), Does.Contain("pc-1440k"));
            Assert.That(Ids(choices), Does.Contain("ndfs-smd-75m"));
            Assert.That(Ids(choices), Does.Contain("tiki100-400k"));
            Assert.That(Ids(choices), Does.Contain("tiki100-hd"));
            Assert.That(Ids(choices), Does.Contain("kaypro4"));
            Assert.That(Ids(choices), Does.Contain("dragon-dsk-40ss"));
        }

        [Test]
        public void Machines_AreEachListedOnce()
        {
            string[] machines = BlankImageCatalogue.Machines(BlankImageCatalogue.List());
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < machines.Length; i++)
            {
                Assert.That(seen.Add(machines[i]), Is.True, "listed twice: " + machines[i]);
            }
            Assert.That(machines.Length, Is.GreaterThan(3));
        }

        [Test]
        public void EveryLayout_CreatesAFileOfItsSize_ThatOpensAsThatLayout()
        {
            BlankImageChoice[] choices = BlankImageCatalogue.List();
            Dictionary<string, string> noOptions = new Dictionary<string, string>();
            for (int i = 0; i < choices.Length; i++)
            {
                BlankImageLayout layout = choices[i].Layout;
                string path = Path.Combine(_dir, layout.Id + layout.Extension);
                BlankImageCatalogue.Create(choices[i], noOptions, path);
                Assert.That(new FileInfo(path).Length, Is.EqualTo(layout.SizeBytes), layout.Id);

                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
                IFileSystemDriver driver = choices[i].Provider.Mount(stream, readOnly: false, layout.Id);
                try
                {
                    // Every new disk is empty, except NDFS: a SINTRAN disk is always made with the
                    // SYSTEM user, and the NDFS driver lists users at the top level.
                    var root = driver.ListDirectory("/");
                    if (choices[i].FileSystemType == "NDFS")
                    {
                        Assert.That(root.Length, Is.EqualTo(1), layout.Id);
                        Assert.That(root[0].Name, Is.EqualTo("SYSTEM"), layout.Id);
                    }
                    else
                    {
                        Assert.That(root.Length, Is.EqualTo(0), layout.Id + " starts empty");
                    }
                }
                finally
                {
                    driver.Dispose();
                }
            }
        }

        [Test]
        public void Create_RefusesToOverwriteAnExistingFile()
        {
            BlankImageChoice d64 = Find(BlankImageCatalogue.List(), "cbm-d64");
            string path = Path.Combine(_dir, "taken.d64");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.Throws<IOException>(() => BlankImageCatalogue.Create(d64, new Dictionary<string, string>(), path));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3 }), "the old file is untouched");
        }

        private static BlankImageChoice Find(BlankImageChoice[] choices, string id)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i].Layout.Id == id)
                {
                    return choices[i];
                }
            }
            throw new AssertionException("no layout " + id);
        }

        private static List<string> Ids(BlankImageChoice[] choices)
        {
            List<string> ids = new List<string>(choices.Length);
            for (int i = 0; i < choices.Length; i++)
            {
                ids.Add(choices[i].Layout.Id);
            }
            return ids;
        }
    }
}
