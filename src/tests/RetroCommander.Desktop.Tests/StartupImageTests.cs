//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Desktop.Tests -- disk images opened through the real MainWindow:
//  - started the way Windows starts it for a linked disk image (the image path as the first
//    argument, see FileAssociationWindowsTests): mounted on the left, its folder on the right;
//  - clicked in the sidebar with an explicit "Open in Right Panel": mounted on the RIGHT, through
//    the same shared mount helper (the old separate sidebar code ignored that choice).

using Avalonia.Headless.NUnit;
using NUnit.Framework;
using RetroCommander.Desktop.Controls;
using RetroCommander.Desktop.ViewModels;

namespace RetroCommander.Desktop.Tests
{
    [TestFixture]
    public class StartupImageTests
    {
        [AvaloniaTest]
        public void StartupImage_MountsLeft_FolderRight()
        {
            using MainWindowTestHost host = new MainWindowTestHost(startupImage: true);
            host.Window.Show();

            host.WaitForStatus("✓ Mounted " + MainWindowTestHost.ImageName + " on left panel");

            Assert.That(host.Window.RightPanelPath.Text?.TrimEnd('\\', '/'), Is.EqualTo(host.Folder).IgnoreCase);
            Assert.That(host.Window.LeftPanelPath.Text?.TrimEnd('\\', '/'), Is.Not.EqualTo(host.Folder).IgnoreCase,
                "the left panel must show the mounted image, not the folder");
        }

        [AvaloniaTest]
        public void SidebarImage_OpenInRightPanel_MountsOnTheRight()
        {
            using MainWindowTestHost host = new MainWindowTestHost(startupImage: false);
            host.Window.Show();
            // Wait for the default panels, so the click does not race the startup navigation.
            Assert.That(MainWindowTestHost.PollUntil(() => host.Status.StartsWith("Ready")), Is.True, host.Status);

            SidebarItemViewModel item = new SidebarItemViewModel
            {
                Name = MainWindowTestHost.ImageName,
                Path = host.ImagePath,
                ItemType = SidebarItemType.Recent
            };
            host.Window.OnSidebarItemSelected(null, new SidebarItemSelectedEventArgs(item, openInLeftPanel: false));

            host.WaitForStatus("✓ Mounted " + MainWindowTestHost.ImageName + " on right panel");
        }
    }
}
