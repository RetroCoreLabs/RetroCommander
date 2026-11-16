//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- headless tests for the Settings "File associations" section.
// They tick the real CheckBoxes the way a user does and read the registry back. The registry is a
// throw-away key (HKEY_CURRENT_USER\Software\RetroCommanderTests\<guid>) deleted after each test;
// the user's real file links are never touched.
//
// Regression: ticking .d64 and .d71, closing Settings and opening it again showed them unticked,
// because ticks were only written by an Apply button. Ticks are now written at once.
//
// Each test creates and deletes its own key in try/finally. NUnit [SetUp]/[TearDown] are NOT
// used: under [AvaloniaTest] the TearDown did not run (measured: every test left its key behind).

using System;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Microsoft.Win32;
using NUnit.Framework;
using RetroCommander.Core.Services;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public class SettingsFileAssociationsTests
    {
        private const string TestRootPath = @"Software\RetroCommanderTests";
        private const string ExePath = @"C:\Apps\RetroCommander\RetroCommander.exe";

        // Runs a test body against a fresh throw-away classes key, and always deletes it.
        private static void WithTestKey(Action<RegistryKey> body)
        {
            string keyPath = TestRootPath + "\\" + Guid.NewGuid().ToString("N");
            RegistryKey classes = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
            try
            {
                body(classes);
            }
            finally
            {
                classes.Dispose();
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                DeleteRootIfEmpty();
            }
        }

        // Removes the shared parent key when no other test is using it.
        private static void DeleteRootIfEmpty()
        {
            using (RegistryKey? parent = Registry.CurrentUser.OpenSubKey(TestRootPath))
            {
                if (parent == null || parent.SubKeyCount > 0 || parent.ValueCount > 0)
                {
                    return;
                }
            }

            Registry.CurrentUser.DeleteSubKey(TestRootPath, throwOnMissingSubKey: false);
        }

        private static FileAssociationService Service(RegistryKey classes)
        {
            return new FileAssociationService(classes, ExePath, notifyShell: false);
        }

        private static CheckBox FindCheck(SettingsDialog dialog, string extension)
        {
            Avalonia.Controls.Controls children = dialog.FileAssociationList.Children;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is CheckBox check && (string?)check.Tag == extension)
                {
                    return check;
                }
            }

            Assert.Fail($"No tick box for {extension}");
            return null!;
        }

        [AvaloniaTest]
        public void Tick_WritesAtOnce_AndSurvivesClosingAndReopening()
        {
            WithTestKey(classes =>
            {
                SettingsDialog first = new SettingsDialog(Service(classes));
                FindCheck(first, ".d64").IsChecked = true;
                FindCheck(first, ".d71").IsChecked = true;
                first.Close();

                Assert.That(Service(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
                Assert.That(Service(classes).GetState(".d71"), Is.EqualTo(FileAssociationState.Linked));

                SettingsDialog second = new SettingsDialog(Service(classes));
                Assert.That(FindCheck(second, ".d64").IsChecked, Is.True);
                Assert.That(FindCheck(second, ".d71").IsChecked, Is.True);
                Assert.That(FindCheck(second, ".d81").IsChecked, Is.False);
                second.Close();
            });
        }

        [AvaloniaTest]
        public void Untick_RemovesTheLink()
        {
            WithTestKey(classes =>
            {
                SettingsDialog dialog = new SettingsDialog(Service(classes));
                FindCheck(dialog, ".d64").IsChecked = true;
                FindCheck(dialog, ".d64").IsChecked = false;
                dialog.Close();

                Assert.That(Service(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.NotLinked));
                Assert.That(classes.OpenSubKey(FileAssociationService.ProgId), Is.Null);
            });
        }

        [AvaloniaTest]
        public void TickAll_LinksEveryKnownType()
        {
            WithTestKey(classes =>
            {
                SettingsDialog dialog = new SettingsDialog(Service(classes));
                dialog.AssociationTickAllButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                dialog.Close();

                for (int i = 0; i < KnownDiskImageTypes.Extensions.Count; i++)
                {
                    Assert.That(Service(classes).GetState(KnownDiskImageTypes.Extensions[i]),
                        Is.EqualTo(FileAssociationState.Linked), KnownDiskImageTypes.Extensions[i]);
                }
            });
        }
    }
}
