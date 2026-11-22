//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- Help > Supported formats: the table holds one row per
// registered driver, the search box filters it, and the CP/M row names the TIKI-100.

using Avalonia.Headless.NUnit;
using NUnit.Framework;
using RetroCommander.Core.Services;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class SupportedFormatsWindowTests
    {
        [AvaloniaTest]
        public void Window_ListsEveryRegisteredDriver()
        {
            int drivers = DriverFactoryBuilder.CreateWithAllDrivers().GetRegisteredTypes().Length;
            SupportedFormatsWindow window = new SupportedFormatsWindow();
            window.Show();
            Assert.That(window.ShownFormats.Count, Is.EqualTo(drivers));
            window.Close();
        }

        [AvaloniaTest]
        public void Search_Tiki_ShowsOnlyTheCpmRow()
        {
            SupportedFormatsWindow window = new SupportedFormatsWindow();
            window.Show();
            window.Filter("tiki");
            Assert.That(window.ShownFormats.Count, Is.EqualTo(1));
            Assert.That(window.ShownFormats[0].FileSystemType, Is.EqualTo("CPM"));
            Assert.That(window.ShownFormats[0].CanWrite, Is.True, "CP/M disks can be written");
            window.Close();
        }

        [AvaloniaTest]
        public void Ticks_FollowTheDrivers()
        {
            SupportedFormatsWindow window = new SupportedFormatsWindow();
            window.Show();
            window.Filter(".t64");
            Assert.That(window.ShownFormats.Count, Is.EqualTo(1));
            Assert.That(window.ShownFormats[0].CanWrite, Is.False, "the T64 tape driver only reads");
            Assert.That(window.ShownFormats[0].CanCreate, Is.False);

            // The Commodore disk driver describes blank D64/D71/D81 layouts, so it gets "New disk".
            window.Filter(".d64");
            bool commodoreCreates = false;
            for (int i = 0; i < window.ShownFormats.Count; i++)
            {
                commodoreCreates |= window.ShownFormats[i].FileSystemType != "CPM" && window.ShownFormats[i].CanCreate;
            }
            Assert.That(commodoreCreates, Is.True);
            window.Close();
        }

        [AvaloniaTest]
        public void Search_Extension_FindsEveryFormatUsingIt()
        {
            SupportedFormatsWindow window = new SupportedFormatsWindow();
            window.Show();
            window.Filter(".d81");
            bool commodore = false;
            bool cpm = false;
            for (int i = 0; i < window.ShownFormats.Count; i++)
            {
                commodore |= window.ShownFormats[i].Extensions.Contains(".d81") && window.ShownFormats[i].FileSystemType != "CPM";
                cpm |= window.ShownFormats[i].FileSystemType == "CPM";
            }
            Assert.That(commodore && cpm, Is.True, "both the Commodore DOS and the CP/M driver open .d81");
            window.Close();
        }
    }
}
