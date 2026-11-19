//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using NUnit.Framework;
using RetroCommander.Core.Services;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Tests for <see cref="KnownDiskImageTypes"/>, the one extension list the app builds from the
    /// registered drivers.
    /// </summary>
    [TestFixture]
    public class KnownDiskImageTypesTests
    {
        [Test]
        public void Extensions_ComeFromEveryRegisteredDriver()
        {
            // The drivers registered by DriverFactoryBuilder.CreateWithAllDrivers, sorted, each once.
            string[] expected =
            {
                ".bsd", ".crt", ".d64", ".d71", ".d81", ".dmk", ".dsk", ".ffs", ".flp", ".hdd", ".ima",
                ".img", ".jvc", ".nd", ".ndf", ".ndfs", ".nord", ".p00", ".prg", ".t64", ".tap", ".ufs",
                ".vdk", ".vfd", ".vhd"
            };
            Assert.That(KnownDiskImageTypes.Extensions, Is.EqualTo(expected));
        }

        [TestCase("GAME.D64", true)]
        [TestCase(@"C:\disks\sintran.img", true)]
        [TestCase("demo.d81", true)]
        [TestCase("system.ndfs", true)]
        [TestCase("readme.txt", false)]
        [TestCase("noextension", false)]
        public void IsKnown_UsesTheDriverList(string fileName, bool expected)
        {
            Assert.That(KnownDiskImageTypes.IsKnown(fileName), Is.EqualTo(expected));
        }

        [Test]
        public void Describe_SharedExtension_NamesEveryFormat()
        {
            string text = KnownDiskImageTypes.Describe(".img");

            // .img is listed by the PC floppy, V7/BSD and FFS drivers.
            Assert.That(text.Split("; ").Length, Is.EqualTo(3), text);
        }

        [Test]
        public void GetFilePickerPatterns_OnePatternPerExtension()
        {
            string[] patterns = KnownDiskImageTypes.GetFilePickerPatterns();

            Assert.That(patterns.Length, Is.EqualTo(KnownDiskImageTypes.Extensions.Count));
            Assert.That(patterns, Does.Contain("*.d64"));
        }
    }
}
