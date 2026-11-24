//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// retrocommander-cli version and --version print the build's own stamp.

using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core;

namespace RetroCommander.Cli.Tests
{
    [TestFixture]
    public sealed class CliVersionTests
    {
        [TestCase("version")]
        [TestCase("--version")]
        public async Task Version_PrintsVersionCommitAndBuildTime(string word)
        {
            StringWriter o = new StringWriter();
            StringWriter e = new StringWriter();
            int code = await new CliApp(o, e).RunAsync(new[] { word });
            Assert.That(code, Is.EqualTo(0), e.ToString());
            Assert.That(o.ToString().Trim(), Is.EqualTo(BuildIdentity.Describe("retrocommander-cli")));
            Assert.That(o.ToString(), Does.Contain(BuildIdentity.Commit));
            Assert.That(o.ToString(), Does.Contain(BuildIdentity.BuildDate));
        }
    }
}
