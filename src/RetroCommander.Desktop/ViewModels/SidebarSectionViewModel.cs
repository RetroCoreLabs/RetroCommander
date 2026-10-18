//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RetroCommander.Desktop.ViewModels
{
    /// <summary>
    /// One collapsible section of the navigation sidebar, with a title, an icon and a list of items.
    /// </summary>
    public class SidebarSectionViewModel : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _isExpanded = true;
        private string _icon = string.Empty;
        private bool _isVisible = true;

        /// <summary>
        /// Gets or sets the section heading text, for example <c>This PC</c>.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets whether the section is open and shows its items. True by default.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// Gets or sets the icon text (an emoji) shown in front of the title.
        /// </summary>
        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        /// <summary>
        /// Gets or sets whether the section is visible. True by default.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        /// Gets the items in this section.
        /// </summary>
        public ObservableCollection<SidebarItemViewModel> Items { get; } = new();

        /// <summary>
        /// Raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for the given property.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property that changed. Filled in with the caller's member name when
        /// left out.
        /// </param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Stores a new value in a backing field and raises <see cref="PropertyChanged"/>, but only
        /// when the value differs from the current one.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the property.
        /// </typeparam>
        /// <param name="field">
        /// The backing field to update.
        /// </param>
        /// <param name="value">
        /// The new value.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property. Filled in with the caller's member name when left out.
        /// </param>
        /// <returns>
        /// True if the value changed; false if it was equal and nothing was done.
        /// </returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Builds the empty "This PC" section, expanded.
        /// </summary>
        /// <returns>
        /// The new section.
        /// </returns>
        public static SidebarSectionViewModel CreateThisPcSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "This PC",
                Icon = "🖥️",
                IsExpanded = true
            };
        }

        /// <summary>
        /// Builds the empty "User Folders" section, expanded.
        /// </summary>
        /// <returns>
        /// The new section.
        /// </returns>
        public static SidebarSectionViewModel CreateUserFoldersSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "User Folders",
                Icon = "👤",
                IsExpanded = true
            };
        }

        /// <summary>
        /// Builds the empty "Recents" section, collapsed.
        /// </summary>
        /// <returns>
        /// The new section.
        /// </returns>
        public static SidebarSectionViewModel CreateRecentsSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "Recents",
                Icon = "🕒",
                IsExpanded = false
            };
        }

        /// <summary>
        /// Builds the empty "Favorites" section, expanded.
        /// </summary>
        /// <returns>
        /// The new section.
        /// </returns>
        public static SidebarSectionViewModel CreateFavoritesSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "Favorites",
                Icon = "⭐",
                IsExpanded = true
            };
        }

        /// <summary>
        /// Builds the empty "Mounts" section, expanded.
        /// </summary>
        /// <returns>
        /// The new section.
        /// </returns>
        public static SidebarSectionViewModel CreateMountsSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "Mounts",
                Icon = "📀",
                IsExpanded = true
            };
        }
    }
}
