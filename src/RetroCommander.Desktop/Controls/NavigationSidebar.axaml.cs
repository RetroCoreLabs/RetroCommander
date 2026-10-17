//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RetroCommander.Desktop.ViewModels;

namespace RetroCommander.Desktop.Controls
{
    public partial class NavigationSidebar : UserControl
    {
        public NavigationSidebarViewModel ViewModel { get; }

        public static readonly StyledProperty<SidebarItemViewModel?> SelectedItemProperty =
            AvaloniaProperty.Register<NavigationSidebar, SidebarItemViewModel?>(nameof(SelectedItem));

        public SidebarItemViewModel? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly RoutedEvent<SidebarItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<NavigationSidebar, SidebarItemSelectedEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

        public event EventHandler<SidebarItemSelectedEventArgs>? ItemSelected;

        public static readonly RoutedEvent<SidebarItemSelectedEventArgs> UnmountRequestedEvent =
            RoutedEvent.Register<NavigationSidebar, SidebarItemSelectedEventArgs>(nameof(UnmountRequested), RoutingStrategies.Bubble);

        public event EventHandler<SidebarItemSelectedEventArgs>? UnmountRequested;

        // Raised when the user picks "Mount" on a drive item; MainWindow opens the same Mount
        // dialog the toolbar's Mount button does (MountDialog has no path-seeding support, so
        // there's nothing more specific to pass through than which item triggered it).
        public static readonly RoutedEvent<SidebarItemSelectedEventArgs> MountRequestedEvent =
            RoutedEvent.Register<NavigationSidebar, SidebarItemSelectedEventArgs>(nameof(MountRequested), RoutingStrategies.Bubble);

        public event EventHandler<SidebarItemSelectedEventArgs>? MountRequested;

        // Raised when the user picks "Properties" on a sidebar item; MainWindow shows the details.
        public event EventHandler<SidebarItemSelectedEventArgs>? PropertiesRequested;

        public NavigationSidebar()
        {
            InitializeComponent();
            ViewModel = new NavigationSidebarViewModel();
            DataContext = ViewModel;
        }

        private void OnSectionHeaderClicked(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is string sectionName)
            {
                var section = sectionName switch
                {
                    "ThisPC" => ViewModel.ThisPcSection,
                    "UserFolders" => ViewModel.UserFoldersSection,
                    "Favorites" => ViewModel.FavoritesSection,
                    "Recents" => ViewModel.RecentsSection,
                    "Mounts" => ViewModel.MountsSection,
                    _ => null
                };

                if (section != null)
                {
                    section.IsExpanded = !section.IsExpanded;
                }
            }
        }

        private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Set selected item when right-clicking (before context menu opens)
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                if (sender is Button button && button.Tag is SidebarItemViewModel item)
                {
                    SelectedItem = item;
                    ViewModel.SelectedItem = item;
                }
            }
        }

        private void OnItemButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Try to get item directly from Tag first
                if (button.Tag is SidebarItemViewModel item)
                {
                    SelectedItem = item;
                    ViewModel.SelectedItem = item; // Also update ViewModel
                    var args = new SidebarItemSelectedEventArgs(item);
                    ItemSelected?.Invoke(this, args);
                    return;
                }

                // Fallback to path lookup
                if (button.Tag is string path)
                {
                    var foundItem = FindItemByPath(path);
                    if (foundItem != null)
                    {
                        SelectedItem = foundItem;
                        ViewModel.SelectedItem = foundItem; // Also update ViewModel
                        var args = new SidebarItemSelectedEventArgs(foundItem);
                        ItemSelected?.Invoke(this, args);
                    }
                }
            }
        }

        private void OnItemClicked(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is string path)
            {
                e.Handled = true; // Prevent event bubbling

                // Find the item that matches this path
                var item = FindItemByPath(path);
                if (item != null)
                {
                    SelectedItem = item;
                    // Raise the event
                    var args = new SidebarItemSelectedEventArgs(item);
                    ItemSelected?.Invoke(this, args);
                }
            }
        }

        private void OnItemTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is Border border && border.Tag is string path)
            {
                e.Handled = true;

                // Find the item that matches this path
                var item = FindItemByPath(path);
                if (item != null)
                {
                    SelectedItem = item;
                    // Raise the event
                    var args = new SidebarItemSelectedEventArgs(item);
                    ItemSelected?.Invoke(this, args);
                }
            }
        }

        private void OnItemHover(object? sender, PointerEventArgs e)
        {
            if (sender is Border border)
            {
                border.Classes.Add("hover");
            }
        }

        private void OnItemUnhover(object? sender, PointerEventArgs e)
        {
            if (sender is Border border)
            {
                border.Classes.Remove("hover");
            }
        }

        private SidebarItemViewModel? FindItemByPath(string path)
        {
            foreach (var section in new[] { ViewModel.ThisPcSection, ViewModel.UserFoldersSection, ViewModel.FavoritesSection, ViewModel.RecentsSection, ViewModel.MountsSection })
            {
                foreach (var item in section.Items)
                {
                    if (item.Path == path)
                        return item;
                }
            }
            return null;
        }

        // Context Menu Handlers
        private void OnOpenInLeftPanel(object? sender, RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                ItemSelected?.Invoke(this, new SidebarItemSelectedEventArgs(SelectedItem, true));
            }
        }

        private void OnOpenInRightPanel(object? sender, RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                ItemSelected?.Invoke(this, new SidebarItemSelectedEventArgs(SelectedItem, false));
            }
        }

        private void OnMount(object? sender, RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                MountRequested?.Invoke(this, new SidebarItemSelectedEventArgs(SelectedItem));
            }
        }

        private void OnUnmount(object? sender, RoutedEventArgs e)
        {
            // Use ViewModel.SelectedItem which is updated when items are clicked
            if (ViewModel.SelectedItem != null && ViewModel.SelectedItem.ItemType == SidebarItemType.Mount)
            {
                // Raise unmount event
                var args = new SidebarItemSelectedEventArgs(ViewModel.SelectedItem);
                UnmountRequested?.Invoke(this, args);
            }
        }

        private void OnProperties(object? sender, RoutedEventArgs e)
        {
            var item = SelectedItem ?? ViewModel.SelectedItem;
            if (item != null)
            {
                PropertiesRequested?.Invoke(this, new SidebarItemSelectedEventArgs(item));
            }
        }

        private void OnPinToFavorites(object? sender, RoutedEventArgs e)
        {
            var item = SelectedItem ?? ViewModel.SelectedItem;
            if (item == null)
                return;

            // Toggle: if this exact target is already a favorite, un-pin it; otherwise add it.
            if (ViewModel.IsFavorite(item.Path))
            {
                ViewModel.RemoveFavorite(item.Path);
                item.IsPinned = false;
                return;
            }

            // Preserve what the favorite points at (Mount vs Recent/folder) so the click handler
            // knows whether to re-mount by URN or simply navigate to the path.
            ViewModel.AddFavorite(item.Name, item.Path, item.FilesystemType, item.ItemType);
            item.IsPinned = true;
        }

        // Remove-from-Favorites: only meaningful for items already in the Favorites section.
        private void OnRemoveFromFavorites(object? sender, RoutedEventArgs e)
        {
            var item = SelectedItem ?? ViewModel.SelectedItem;
            if (item != null)
            {
                ViewModel.RemoveFavorite(item.Path);
            }
        }
    }

    public class SidebarItemSelectedEventArgs : RoutedEventArgs
    {
        public SidebarItemViewModel Item { get; }

        /// <summary>Which panel to target. Only meaningful when <see cref="PanelExplicit"/> is true.</summary>
        public bool OpenInLeftPanel { get; }

        /// <summary>True when the target panel was chosen explicitly (context menu), false for a
        /// normal click (which should use the active panel).</summary>
        public bool PanelExplicit { get; }

        // Normal click: no explicit panel choice -> caller uses the active panel.
        public SidebarItemSelectedEventArgs(SidebarItemViewModel item)
        {
            Item = item;
            OpenInLeftPanel = true;
            PanelExplicit = false;
        }

        // Explicit "Open in Left/Right Panel".
        public SidebarItemSelectedEventArgs(SidebarItemViewModel item, bool openInLeftPanel)
        {
            Item = item;
            OpenInLeftPanel = openInLeftPanel;
            PanelExplicit = true;
        }
    }
}
