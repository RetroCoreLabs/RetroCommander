//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroCommander.Tests.Shared;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Tests for <see cref="FileAssociationService"/> that read the registry back. Each test writes
    /// below a throw-away key (a new random-named key under HKEY_CURRENT_USER\Software\RetroCommanderTests)
    /// that stands in for the user's classes key, and deletes it in a finally block. The real file links are
    /// never touched, and Explorer is not notified. <see cref="FileAssociationWindowsTests"/> checks
    /// that Windows itself resolves what is written.
    /// </summary>
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public class FileAssociationServiceTests
    {
        private const string ExePath = @"C:\Apps\RetroCommander\RetroCommander.exe";
        private const string ProgId = FileAssociationService.DefaultProgId;

        [Test]
        public void Apply_Link_WritesFileTypeCommandIconAndExtension()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".d64" }, ReadOnlySpan<string>.Empty);

                Assert.That(ReadDefault(classes, ProgId + @"\shell\open\command"), Is.EqualTo("\"" + ExePath + "\" \"%1\""));
                Assert.That(ReadDefault(classes, ProgId + @"\DefaultIcon"), Is.EqualTo("\"" + ExePath + "\",0"));
                Assert.That(ReadDefault(classes, ProgId), Is.EqualTo("Retro disk image"));
                Assert.That(ReadDefault(classes, ".d64"), Is.EqualTo(ProgId));

                using RegistryKey? openWith = classes.OpenSubKey(@".d64\OpenWithProgids");
                Assert.That(openWith, Is.Not.Null);
                Assert.That(openWith!.GetValueKind(ProgId), Is.EqualTo(RegistryValueKind.None));
            });
        }

        [Test]
        public void GetState_ReportsNotLinkedLinkedAndOtherCopy()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Assert.That(Create(classes).GetState(".d81"), Is.EqualTo(FileAssociationState.NotLinked));

                Create(classes).Apply(new[] { ".d81" }, ReadOnlySpan<string>.Empty);
                Assert.That(Create(classes).GetState(".d81"), Is.EqualTo(FileAssociationState.Linked));

                // The same registry seen from a copy of the program at another path.
                Assert.That(Create(classes, @"D:\Other\RetroCommander.exe").GetState(".d81"),
                    Is.EqualTo(FileAssociationState.LinkedToOtherCopy));
            });
        }

        [Test]
        public void GetState_OnlyInOpenWithList_CountsAsLinked()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                // Another program is the default, RetroCommander only offered under "Open with".
                Create(classes).Apply(new[] { ".img" }, ReadOnlySpan<string>.Empty);
                using (RegistryKey ext = classes.CreateSubKey(".img", writable: true))
                {
                    ext.SetValue(null, "SomeOther.Image");
                }

                Assert.That(Create(classes).GetState(".img"), Is.EqualTo(FileAssociationState.Linked));
            });
        }

        [Test]
        public void GetState_ExtensionNamesFileTypeThatIsGone_IsNotLinked()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".d64" }, ReadOnlySpan<string>.Empty);
                classes.DeleteSubKeyTree(ProgId);

                Assert.That(Create(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.NotLinked));
            });
        }

        [TestCase("D64")]
        [TestCase(".D64")]
        [TestCase(" .d64 ")]
        public void Extension_CaseDotAndSpacesDoNotMatter(string written)
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { written }, ReadOnlySpan<string>.Empty);

                Assert.That(Create(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
                Assert.That(classes.OpenSubKey(".d64"), Is.Not.Null);
            });
        }

        [Test]
        public void Apply_Unlink_KeepsAnotherProgramsDefault()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".img", ".d64" }, ReadOnlySpan<string>.Empty);
                using (RegistryKey ext = classes.CreateSubKey(".img", writable: true))
                {
                    ext.SetValue(null, "SomeOther.Image");
                }

                Create(classes).Apply(new[] { ".d64" }, new[] { ".img" });

                Assert.That(ReadDefault(classes, ".img"), Is.EqualTo("SomeOther.Image"));
                Assert.That(classes.OpenSubKey(@".img\OpenWithProgids"), Is.Null, "our only Open-with entry is gone, so the empty key is removed");
                Assert.That(Create(classes).GetState(".img"), Is.EqualTo(FileAssociationState.NotLinked));
                Assert.That(Create(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
            });
        }

        [Test]
        public void Apply_Unlink_KeepsOtherProgramsInOpenWithList()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".d64" }, ReadOnlySpan<string>.Empty);
                using (RegistryKey openWith = classes.CreateSubKey(@".d64\OpenWithProgids", writable: true))
                {
                    openWith.SetValue("DirMaster", Array.Empty<byte>(), RegistryValueKind.None);
                }

                Create(classes).Apply(ReadOnlySpan<string>.Empty, new[] { ".d64" });

                using RegistryKey? kept = classes.OpenSubKey(@".d64\OpenWithProgids");
                Assert.That(kept, Is.Not.Null);
                Assert.That(kept!.GetValue("DirMaster"), Is.Not.Null);
                Assert.That(kept.GetValue(ProgId), Is.Null);
            });
        }

        [Test]
        public void Apply_NothingLinked_RemovesFileTypeAndLeavesNoEmptyKeys()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".d64", ".d81" }, ReadOnlySpan<string>.Empty);

                Create(classes).Apply(ReadOnlySpan<string>.Empty, new[] { ".d64", ".d81" });

                Assert.That(classes.SubKeyCount, Is.EqualTo(0), string.Join(", ", classes.GetSubKeyNames()));
            });
        }

        [Test]
        public void Apply_SomeUnlinked_KeepsFileTypeForTheRest()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".d64", ".d81" }, ReadOnlySpan<string>.Empty);

                Create(classes).Apply(new[] { ".d81" }, new[] { ".d64" });

                Assert.That(Create(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.NotLinked));
                Assert.That(Create(classes).GetState(".d81"), Is.EqualTo(FileAssociationState.Linked));
            });
        }

        [Test]
        public void Apply_AgainFromNewExe_PointsAtNewExe()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes, @"D:\Old\RetroCommander.exe").Apply(new[] { ".t64" }, ReadOnlySpan<string>.Empty);

                Create(classes).Apply(new[] { ".t64" }, ReadOnlySpan<string>.Empty);

                Assert.That(Create(classes).GetState(".t64"), Is.EqualTo(FileAssociationState.Linked));
            });
        }

        [Test]
        public void Apply_OwnProgId_DoesNotTouchTheDefaultOne()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Create(classes).Apply(new[] { ".d64" }, ReadOnlySpan<string>.Empty);

                new FileAssociationService(classes, ExePath, notifyShell: false, progId: "RetroCommander.Test")
                    .Apply(ReadOnlySpan<string>.Empty, new[] { ".d64" });

                Assert.That(Create(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
                Assert.That(classes.OpenSubKey(ProgId), Is.Not.Null);
            });
        }

        [TestCase("")]
        [TestCase(".")]
        [TestCase(@".a\b")]
        [TestCase(".a/b")]
        [TestCase(".tar.gz")]
        [TestCase(". d64")]
        public void NotAnExtension_Throws_AndWritesNothing(string bad)
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                FileAssociationService service = Create(classes);
                Assert.Throws<ArgumentException>(() => service.GetState(bad));
                Assert.Throws<ArgumentException>(() => service.Apply(ReadOnlySpan<string>.Empty, new[] { bad }));
                Assert.That(classes.SubKeyCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void OpenCommand_QuotesExeAndFile()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                Assert.That(Create(classes).OpenCommand, Is.EqualTo("\"" + ExePath + "\" \"%1\""));
            });
        }

        private static FileAssociationService Create(RegistryKey classes, string exePath = ExePath)
        {
            return new FileAssociationService(classes, exePath, notifyShell: false);
        }

        private static object? ReadDefault(RegistryKey classes, string subKey)
        {
            using RegistryKey? key = classes.OpenSubKey(subKey);
            return key?.GetValue(null);
        }
    }
}
