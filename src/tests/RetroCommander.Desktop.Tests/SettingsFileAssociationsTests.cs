//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- headless tests for the Settings "File associations" section.
// They tick the real CheckBoxes the way a user does and read the registry back. The registry is a
// throw-away key (see TestRegistry) deleted after each test; the user's real file links are never
// touched.
//
// Regression: ticking .d64 and .d71, closing Settings and opening it again showed them unticked,
// because ticks were only written by an Apply button. Ticks are now written at once.

using System;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Microsoft.Win32;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroCommander.Tests.Shared;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public class SettingsFileAssociationsTests
    {
        private const string ExePath = @"C:\Apps\RetroCommander\RetroCommander.exe";

        [AvaloniaTest]
        public void Tick_WritesAtOnce_AndSurvivesClosingAndReopening()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                SettingsDialog first = Open(classes);
                FindCheck(first, ".d64").IsChecked = true;
                FindCheck(first, ".d71").IsChecked = true;
                first.Close();

                Assert.That(Service(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
                Assert.That(Service(classes).GetState(".d71"), Is.EqualTo(FileAssociationState.Linked));

                SettingsDialog second = Open(classes);
                Assert.That(FindCheck(second, ".d64").IsChecked, Is.True);
                Assert.That(FindCheck(second, ".d71").IsChecked, Is.True);
                Assert.That(FindCheck(second, ".d81").IsChecked, Is.False);
                second.Close();
            });
        }

        [AvaloniaTest]
        public void Untick_RemovesTheLink_AndTheFileType()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                SettingsDialog dialog = Open(classes);
                FindCheck(dialog, ".d64").IsChecked = true;
                FindCheck(dialog, ".d64").IsChecked = false;
                dialog.Close();

                Assert.That(Service(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.NotLinked));
                Assert.That(classes.SubKeyCount, Is.EqualTo(0), string.Join(", ", classes.GetSubKeyNames()));
            });
        }

        [AvaloniaTest]
        public void TickAll_LinksEveryKnownType_TickNone_RemovesThemAll()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                SettingsDialog dialog = Open(classes);
                dialog.AssociationTickAllButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                for (int i = 0; i < KnownDiskImageTypes.Extensions.Count; i++)
                {
                    Assert.That(Service(classes).GetState(KnownDiskImageTypes.Extensions[i]),
                        Is.EqualTo(FileAssociationState.Linked), KnownDiskImageTypes.Extensions[i]);
                }

                dialog.AssociationTickNoneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                dialog.Close();

                Assert.That(classes.SubKeyCount, Is.EqualTo(0), string.Join(", ", classes.GetSubKeyNames()));
            });
        }

        [AvaloniaTest]
        public void Tick_KeepsTheSameBoxes_SoKeyboardFocusIsNotLost()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                SettingsDialog dialog = Open(classes);
                CheckBox before = FindCheck(dialog, ".d81");
                int count = dialog.FileAssociationList.Children.Count;

                FindCheck(dialog, ".d64").IsChecked = true;

                Assert.That(FindCheck(dialog, ".d81"), Is.SameAs(before));
                Assert.That(dialog.FileAssociationList.Children.Count, Is.EqualTo(count));
                Assert.That(count, Is.EqualTo(KnownDiskImageTypes.Extensions.Count));
                dialog.Close();
            });
        }

        [AvaloniaTest]
        public void LinkedToOtherCopy_ShowsTicked_AndAnyChangeRepointsToThisExe()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                new FileAssociationService(classes, @"D:\Old\RetroCommander.exe", notifyShell: false)
                    .Apply(new[] { ".d64" }, ReadOnlySpan<string>.Empty);

                SettingsDialog dialog = Open(classes);
                Assert.That(FindCheck(dialog, ".d64").IsChecked, Is.True);
                Assert.That(dialog.FileAssociationStatus.Text, Does.Contain(@"D:\Old\RetroCommander.exe"));

                FindCheck(dialog, ".d71").IsChecked = true;
                dialog.Close();

                Assert.That(Service(classes).GetState(".d64"), Is.EqualTo(FileAssociationState.Linked));
                Assert.That(Service(classes).GetState(".d71"), Is.EqualTo(FileAssociationState.Linked));
            });
        }

        [AvaloniaTest]
        public void ExePath_IsShownToTheUser()
        {
            TestRegistry.WithThrowAwayClassesKey(classes =>
            {
                SettingsDialog dialog = Open(classes);
                Assert.That(dialog.FileAssociationExePath.Text, Does.Contain(ExePath));
                Assert.That(dialog.FileAssociationsSection.IsVisible, Is.True);
                dialog.Close();
            });
        }

        private static FileAssociationService Service(RegistryKey classes)
        {
            return new FileAssociationService(classes, ExePath, notifyShell: false);
        }

        private static SettingsDialog Open(RegistryKey classes)
        {
            SettingsDialog dialog = new SettingsDialog(Service(classes));
            // Show it, so Close() raises Closed as it does for the user.
            dialog.Show();
            return dialog;
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
    }
}
