//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// The build stamp is really there and names a real commit. The whole mechanism lives in MSBuild,
// where a mistyped property fails silently and the build stays green, so it needs tests of its own;
// a stamp that reads "unknown" would look fine and say nothing. Same checks as RetroTerm's.

using System;
using System.Diagnostics;
using NUnit.Framework;
using RetroCommander.Core;

namespace RetroCommander.Core.Tests;

[TestFixture]
public class BuildIdentityTests
{
    [Test]
    public void TheCommitIsRecorded_As12HexDigits()
    {
        Assert.That(BuildIdentity.Commit, Is.Not.EqualTo(BuildIdentity.Unknown));
        Assert.That(BuildIdentity.Commit.Length, Is.EqualTo(12));
        for (int i = 0; i < BuildIdentity.Commit.Length; i++)
        {
            char c = BuildIdentity.Commit[i];
            Assert.That((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'), Is.True, "not a hex digit: '" + c + "'");
        }
    }

    [Test]
    public void TheCommitIsARealCommitInThisRepository()
    {
        // Not compared with HEAD on purpose: build, test, then commit leaves the binary stamped with
        // the parent, and a test that goes red after every commit gets ignored.
        string kind = RunGit("cat-file -t " + BuildIdentity.Commit);
        if (kind.Length == 0)
        {
            Assert.Ignore("git is not available here, or this is not a checkout");
        }

        Assert.That(kind, Is.EqualTo("commit"));
    }

    [Test]
    public void BranchVersionDateTimeAndCopyright_AreRecorded()
    {
        Assert.That(BuildIdentity.Branch, Is.Not.EqualTo(BuildIdentity.Unknown));
        Assert.That(BuildIdentity.BuildDate, Is.Not.EqualTo(BuildIdentity.Unknown));
        Assert.That(BuildIdentity.BuildTime, Is.Not.EqualTo(BuildIdentity.Unknown));
        Assert.That(BuildIdentity.Version, Is.Not.EqualTo(BuildIdentity.Unknown));
        Assert.That(BuildIdentity.Version, Does.Not.Contain("+"), "the SDK's +commit is cut off");
        Assert.That(BuildIdentity.Copyright, Does.Contain("Ronny Hansen"));
    }

    [Test]
    public void TheDescriptionNamesEverything_AndSaysWhenTheTreeWasNotClean()
    {
        string line = BuildIdentity.Describe("x");
        Assert.That(line, Does.Contain(BuildIdentity.Version));
        Assert.That(line, Does.Contain(BuildIdentity.Commit));
        Assert.That(line, Does.Contain(BuildIdentity.Branch));
        Assert.That(line, Does.Contain(BuildIdentity.BuildDate + " " + BuildIdentity.BuildTime));
        Assert.That(line.Contains("uncommitted", StringComparison.Ordinal), Is.EqualTo(BuildIdentity.IsDirty));
    }

    // Asks git about the checkout this test assembly was built from; empty when git cannot answer.
    private static string RunGit(string arguments)
    {
        ProcessStartInfo start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = System.IO.Path.GetDirectoryName(typeof(BuildIdentityTests).Assembly.Location) ?? ".",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using Process? process = Process.Start(start);
            if (process == null)
            {
                return string.Empty;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty; // no git program on this machine
        }
    }
}
