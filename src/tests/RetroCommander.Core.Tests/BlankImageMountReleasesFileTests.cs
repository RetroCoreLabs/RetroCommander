//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Closing a disk opened through a driver's Mount must close the disk file, or the file stays
    /// locked after it is unmounted (seen first as a locked image after a retrocommander-cli run).
    /// </summary>
    [TestFixture]
    public sealed class BlankImageMountReleasesFileTests
    {
        [Test]
        public void EveryLayout_DisposingTheDriver_ClosesTheFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "rc-release-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            System.Text.StringBuilder locked = new System.Text.StringBuilder();
            try
            {
                BlankImageChoice[] all = BlankImageCatalogue.List();
                for (int i = 0; i < all.Length; i++)
                {
                    string path = Path.Combine(dir, i + all[i].Layout.Extension);
                    BlankImageCatalogue.Create(all[i], new System.Collections.Generic.Dictionary<string, string>(), path);
                    FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    IFileSystemDriver driver = all[i].Provider.Mount(stream, false, all[i].Layout.Id);
                    driver.Dispose();
                    try
                    {
                        using FileStream again = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (IOException)
                    {
                        locked.Append(' ').Append(all[i].Layout.Id);
                        stream.Dispose();
                    }
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
            Assert.That(locked.ToString(), Is.Empty, "still open after the driver was disposed:");
        }
    }
}
