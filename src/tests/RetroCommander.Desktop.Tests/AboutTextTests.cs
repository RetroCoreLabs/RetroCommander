//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// Help, About shows the build's own stamp: version, commit, build date and time, copyright.

using NUnit.Framework;
using RetroCommander.Core;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class AboutTextTests
    {
        [Test]
        public void About_ShowsVersionCommitBuildTimeAndCopyright()
        {
            string text = MainWindow.AboutText();
            Assert.That(text, Does.Contain("Version " + BuildIdentity.Version));
            Assert.That(text, Does.Contain(BuildIdentity.CommitLine()));
            Assert.That(text, Does.Contain("Built " + BuildIdentity.BuildDate + " " + BuildIdentity.BuildTime));
            Assert.That(text, Does.Contain(BuildIdentity.Copyright));
            Assert.That(text, Does.Not.Contain(BuildIdentity.Unknown));
        }
    }
}
