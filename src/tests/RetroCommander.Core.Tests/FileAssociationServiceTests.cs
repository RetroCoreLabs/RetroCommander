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

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Tests for <see cref="FileAssociationService"/>. Each test writes below a throw-away key
    /// (HKEY_CURRENT_USER\Software\RetroCommanderTests\&lt;guid&gt;) that stands in for the user's
    /// classes key, and deletes it afterwards. The real file links are never touched, and Explorer
    /// is not notified.
    /// </summary>
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public class FileAssociationServiceTests
    {
        private const string TestRootPath = @"Software\RetroCommanderTests";
        private const string ExePath = @"C:\Apps\RetroCommander\RetroCommander.exe";

        private string _keyPath = string.Empty;
        private RegistryKey? _classes;

        [SetUp]
        public void SetUp()
        {
            _keyPath = TestRootPath + "\\" + Guid.NewGuid().ToString("N");
            _classes = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
        }

        [TearDown]
        public void TearDown()
        {
            _classes?.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

            // Remove the shared parent too when no other test run is using it.
            using (RegistryKey? parent = Registry.CurrentUser.OpenSubKey(TestRootPath))
            {
                if (parent == null || parent.SubKeyCount > 0 || parent.ValueCount > 0)
                {
                    return;
                }
            }

            Registry.CurrentUser.DeleteSubKey(TestRootPath, throwOnMissingSubKey: false);
        }

        private FileAssociationService Create(string exePath = ExePath)
        {
            return new FileAssociationService(_classes!, exePath, notifyShell: false);
        }

        [Test]
        public void Link_WritesFileTypeCommandIconAndExtension()
        {
            FileAssociationService service = Create();

            service.Link(new[] { ".d64" });

            using RegistryKey? command = _classes!.OpenSubKey(FileAssociationService.ProgId + @"\shell\open\command");
            Assert.That(command?.GetValue(null), Is.EqualTo("\"" + ExePath + "\" \"%1\""));

            using RegistryKey? icon = _classes.OpenSubKey(FileAssociationService.ProgId + @"\DefaultIcon");
            Assert.That(icon?.GetValue(null), Is.EqualTo("\"" + ExePath + "\",0"));

            using RegistryKey? ext = _classes.OpenSubKey(".d64");
            Assert.That(ext?.GetValue(null), Is.EqualTo(FileAssociationService.ProgId));

            using RegistryKey? openWith = _classes.OpenSubKey(@".d64\OpenWithProgids");
            Assert.That(openWith?.GetValue(FileAssociationService.ProgId), Is.Not.Null);
            Assert.That(openWith!.GetValueKind(FileAssociationService.ProgId), Is.EqualTo(RegistryValueKind.None));
        }

        [Test]
        public void GetState_ReportsNotLinkedLinkedAndOtherCopy()
        {
            Assert.That(Create().GetState(".d81"), Is.EqualTo(FileAssociationState.NotLinked));

            Create().Link(new[] { ".d81" });
            Assert.That(Create().GetState(".d81"), Is.EqualTo(FileAssociationState.Linked));

            // The same registry seen from a copy of the program at another path.
            Assert.That(Create(@"D:\Other\RetroCommander.exe").GetState(".d81"),
                Is.EqualTo(FileAssociationState.LinkedToOtherCopy));
        }

        [Test]
        public void Extension_CaseAndDotDoNotMatter()
        {
            Create().Link(new[] { "D64" });

            Assert.That(Create().GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
            Assert.That(Create().GetState(".D64"), Is.EqualTo(FileAssociationState.Linked));
        }

        [Test]
        public void Unlink_KeepsAnotherProgramsLink()
        {
            // Another program owns .img as its default; RetroCommander is only in "Open with".
            using (RegistryKey ext = _classes!.CreateSubKey(".img", writable: true))
            {
                ext.SetValue(null, "SomeOther.Image");
            }

            FileAssociationService service = Create();
            using (RegistryKey openWith = _classes.CreateSubKey(@".img\OpenWithProgids", writable: true))
            {
                openWith.SetValue(FileAssociationService.ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            service.Link(new[] { ".d64" });
            service.Unlink(new[] { ".img" }, new[] { ".d64", ".img" });

            using RegistryKey? img = _classes.OpenSubKey(".img");
            Assert.That(img?.GetValue(null), Is.EqualTo("SomeOther.Image"));
            Assert.That(service.GetState(".img"), Is.EqualTo(FileAssociationState.NotLinked));
            Assert.That(service.GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
        }

        [Test]
        public void Unlink_LastExtension_RemovesFileTypeAndLeavesNoEmptyKeys()
        {
            FileAssociationService service = Create();
            service.Link(new[] { ".d64", ".d81" });

            service.Unlink(new[] { ".d64", ".d81" }, new[] { ".d64", ".d81" });

            Assert.That(_classes!.OpenSubKey(FileAssociationService.ProgId), Is.Null);
            Assert.That(_classes.OpenSubKey(".d64"), Is.Null);
            Assert.That(_classes.OpenSubKey(".d81"), Is.Null);
        }

        [Test]
        public void Unlink_SomeExtensions_KeepsFileTypeForTheRest()
        {
            FileAssociationService service = Create();
            service.Link(new[] { ".d64", ".d81" });

            service.Unlink(new[] { ".d64" }, new[] { ".d64", ".d81" });

            Assert.That(service.GetState(".d64"), Is.EqualTo(FileAssociationState.NotLinked));
            Assert.That(service.GetState(".d81"), Is.EqualTo(FileAssociationState.Linked));
        }

        [Test]
        public void Link_Again_PointsAtNewExe()
        {
            Create(@"D:\Old\RetroCommander.exe").Link(new[] { ".t64" });

            Create().Link(new[] { ".t64" });

            Assert.That(Create().GetState(".t64"), Is.EqualTo(FileAssociationState.Linked));
        }

        [Test]
        public void BadExtension_Throws()
        {
            FileAssociationService service = Create();
            Assert.Throws<ArgumentException>(() => service.GetState(""));
            Assert.Throws<ArgumentException>(() => service.GetState(@".a\b"));
        }
    }
}
