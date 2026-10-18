//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RetroCommander.Desktop.ViewModels;

namespace RetroCommander.Desktop.Controls
{
    /// <summary>
    /// The quick access toolbar: Back button, address bar, and the Mounts and Recents flyouts.
    /// </summary>
    public partial class QuickAccessBar : UserControl
    {
        /// <summary>
        /// Gets the view model that holds the mount and recent lists. The constructor creates it
        /// and sets it as the control's data context.
        /// </summary>
        public QuickAccessBarViewModel ViewModel { get; }

        /// <summary>
        /// Identifies the bubbling routed event registered for <see cref="ItemSelected"/>.
        /// </summary>
        /// <remarks>
        /// The control raises <see cref="ItemSelected"/> as a plain CLR event; this routed event
        /// is registered but not raised by the code in this class.
        /// </remarks>
        public static readonly RoutedEvent<QuickAccessItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<QuickAccessBar, QuickAccessItemSelectedEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

        /// <summary>
        /// Raised when the user clicks an item in the Mounts or Recents flyout. The flyout is
        /// closed after the event is raised.
        /// </summary>
        public event EventHandler<QuickAccessItemSelectedEventArgs>? ItemSelected;

        /// <summary>
        /// Raised when the toolbar's Back button is clicked. The actual navigation history
        /// is owned by MainWindow, so it handles this and decides where "back" goes.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? BackRequested;

        /// <summary>
        /// Raised when the user clicks "Mount New..." in the Mounts flyout. MainWindow opens the
        /// same Mount dialog the main toolbar's Mount button does.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? MountNewRequested;

        /// <summary>
        /// Creates the bar, builds its view model and sets that view model as the data context.
        /// </summary>
        public QuickAccessBar()
        {
            InitializeComponent();
            ViewModel = new QuickAccessBarViewModel();
            DataContext = ViewModel;
        }

        /// <summary>
        /// Raised when the user types a path in the address bar and presses Enter. MainWindow
        /// navigates the active panel to it.
        /// </summary>
        public event EventHandler<string>? AddressNavigationRequested;

        /// <summary>
        /// Enables/disables the Back button (driven by MainWindow's history state).
        /// </summary>
        public void SetBackEnabled(bool enabled)
        {
            if (BackButton != null)
                BackButton.IsEnabled = enabled;
        }

        /// <summary>
        /// Updates the address bar to reflect the active panel's current path.
        /// </summary>
        public void SetAddressPath(string? path)
        {
            if (AddressBox != null)
                AddressBox.Text = path ?? string.Empty;
        }

        private void OnAddressKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            string path = AddressBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(path))
                AddressNavigationRequested?.Invoke(this, path);
        }

        private void OnBackClicked(object? sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, e);
        }

        private void OnMountItemClicked(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is string path)
            {
                var item = ViewModel.MountItems.FirstOrDefault(m => m.Path == path);
                if (item != null)
                {
                    ItemSelected?.Invoke(this, new QuickAccessItemSelectedEventArgs(item));
                    if (MountsButton?.Flyout != null)
                        MountsButton.Flyout.Hide();
                }
            }
        }

        private void OnRecentItemClicked(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is string path)
            {
                var item = ViewModel.RecentItems.FirstOrDefault(r => r.Path == path);
                if (item != null)
                {
                    ItemSelected?.Invoke(this, new QuickAccessItemSelectedEventArgs(item));
                    if (RecentsButton?.Flyout != null)
                        RecentsButton.Flyout.Hide();
                }
            }
        }

        private void OnMountNewClicked(object? sender, RoutedEventArgs e)
        {
            MountNewRequested?.Invoke(this, e);
            if (MountsButton?.Flyout != null)
                MountsButton.Flyout.Hide();
        }

        private void OnClearRecentsClicked(object? sender, RoutedEventArgs e)
        {
            ViewModel.ClearRecents();
            if (RecentsButton?.Flyout != null)
                RecentsButton.Flyout.Hide();
        }
    }

    /// <summary>
    /// Event data for <see cref="QuickAccessBar.ItemSelected"/>: the item the user clicked.
    /// </summary>
    public class QuickAccessItemSelectedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Gets the quick access item the user clicked.
        /// </summary>
        public QuickAccessItemViewModel Item { get; }

        /// <summary>
        /// Creates event data for the given item.
        /// </summary>
        /// <param name="item">
        /// The quick access item the user clicked.
        /// </param>
        public QuickAccessItemSelectedEventArgs(QuickAccessItemViewModel item)
        {
            Item = item;
        }
    }
}
