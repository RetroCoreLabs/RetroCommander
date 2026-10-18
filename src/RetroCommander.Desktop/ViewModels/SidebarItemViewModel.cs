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
    /// One entry in the navigation sidebar: a drive, user folder, mount, recent item or favorite.
    /// </summary>
    public class SidebarItemViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _path = string.Empty;
        private string _icon = string.Empty;
        private bool _isExpanded;
        private bool _isSelected;
        private SidebarItemType _itemType;
        private string _filesystemType = string.Empty;
        private string _tooltip = string.Empty;
        private bool _isPinned;
        private DateTime _lastAccessed;
        private SidebarItemType _originType;

        /// <summary>
        /// Gets or sets the text shown for the item in the sidebar.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Gets or sets the path the item points at: a drive root, a folder path, or a mount URN.
        /// </summary>
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        /// <summary>
        /// Gets or sets the icon text (an emoji) shown in front of the name.
        /// </summary>
        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        /// <summary>
        /// Gets or sets whether the item is expanded. False by default.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// Gets or sets whether the item is selected. False by default.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Gets or sets which kind of sidebar entry this is.
        /// </summary>
        public SidebarItemType ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        /// <summary>
        /// Gets or sets the filesystem type label, such as <c>NDFS</c> or <c>D64</c>. Empty for
        /// drives and user folders.
        /// </summary>
        public string FilesystemType
        {
            get => _filesystemType;
            set => SetProperty(ref _filesystemType, value);
        }

        /// <summary>
        /// Gets or sets the tooltip text shown when hovering over the item.
        /// </summary>
        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        /// <summary>
        /// Gets or sets whether the item is pinned as a favorite.
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        /// <summary>
        /// Gets or sets when the item was last visited. Set for recent items; left at the default
        /// value for the other kinds.
        /// </summary>
        public DateTime LastAccessed
        {
            get => _lastAccessed;
            set => SetProperty(ref _lastAccessed, value);
        }

        /// <summary>
        /// For a Favorite, the kind of thing it points at (Mount vs a filesystem folder). This
        /// drives how a click is resolved: a Mount favorite re-mounts by URN if the disk is no
        /// longer mounted; any other kind navigates to its path. For non-favorite items this is
        /// just the same as <see cref="ItemType"/>.
        /// </summary>
        public SidebarItemType OriginType
        {
            get => _originType;
            set => SetProperty(ref _originType, value);
        }

        /// <summary>
        /// Gets the child items under this item. Empty unless something adds to it.
        /// </summary>
        public ObservableCollection<SidebarItemViewModel> Children { get; } = new();

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
        /// Builds a <see cref="SidebarItemType.Drive"/> item named after
        /// <paramref name="driveLetter"/> with a colon, for example <c>C:</c>.
        /// </summary>
        /// <param name="driveLetter">
        /// The drive letter, without the colon.
        /// </param>
        /// <param name="path">
        /// The root path of the drive.
        /// </param>
        /// <returns>
        /// The new drive item.
        /// </returns>
        public static SidebarItemViewModel CreateDrive(string driveLetter, string path)
        {
            return new SidebarItemViewModel
            {
                Name = $"{driveLetter}:",
                Path = path,
                Icon = "💾",
                ItemType = SidebarItemType.Drive,
                Tooltip = $"Local drive {driveLetter}:"
            };
        }

        /// <summary>
        /// Builds a <see cref="SidebarItemType.UserFolder"/> item. The icon is picked from
        /// <paramref name="name"/> (Home, Desktop, Documents and a few others), with a plain folder
        /// icon otherwise.
        /// </summary>
        /// <param name="name">
        /// The folder name shown in the sidebar; also picks the icon, ignoring case.
        /// </param>
        /// <param name="path">
        /// The full path of the folder.
        /// </param>
        /// <returns>
        /// The new user folder item.
        /// </returns>
        public static SidebarItemViewModel CreateUserFolder(string name, string path)
        {
            var icon = name.ToLower() switch
            {
                "home" => "🏠",
                "desktop" => "🖥️",
                "documents" => "📄",
                "downloads" => "⬇️",
                "pictures" => "🖼️",
                "music" => "🎵",
                "videos" => "🎬",
                _ => "📁"
            };

            return new SidebarItemViewModel
            {
                Name = name,
                Path = path,
                Icon = icon,
                ItemType = SidebarItemType.UserFolder,
                Tooltip = $"User folder: {path}"
            };
        }

        /// <summary>
        /// Builds a <see cref="SidebarItemType.Mount"/> item. Its name is
        /// <paramref name="name"/> followed by the filesystem type in brackets.
        /// </summary>
        /// <param name="name">
        /// The name of the mount.
        /// </param>
        /// <param name="path">
        /// The path that identifies the mount.
        /// </param>
        /// <param name="filesystemType">
        /// The filesystem type label; picks the icon, ignoring case.
        /// </param>
        /// <returns>
        /// The new mount item.
        /// </returns>
        public static SidebarItemViewModel CreateMount(string name, string path, string filesystemType)
        {
            var icon = filesystemType.ToUpper() switch
            {
                "D64" => "💿",
                "D71" => "💿",
                "D81" => "💿",
                "NDFS" => "🔷",
                "FAT" => "💾",
                "UFS" => "🐧",
                "BSD" => "🐧",
                _ => "📀"
            };

            return new SidebarItemViewModel
            {
                Name = $"{name} [{filesystemType}]",
                Path = path,
                Icon = icon,
                ItemType = SidebarItemType.Mount,
                FilesystemType = filesystemType,
                Tooltip = $"Mounted {filesystemType} filesystem: {path}"
            };
        }

        /// <summary>
        /// Builds a <see cref="SidebarItemType.Recent"/> item named after the last part of
        /// <paramref name="path"/>.
        /// </summary>
        /// <param name="path">
        /// The path that was visited.
        /// </param>
        /// <param name="filesystemType">
        /// The filesystem type label; picks the icon, ignoring case.
        /// </param>
        /// <param name="lastAccessed">
        /// When the path was visited; stored in <see cref="LastAccessed"/> and shown in the tooltip.
        /// </param>
        /// <returns>
        /// The new recent item.
        /// </returns>
        public static SidebarItemViewModel CreateRecent(string path, string filesystemType, DateTime lastAccessed)
        {
            var fileName = System.IO.Path.GetFileName(path);
            var icon = filesystemType.ToUpper() switch
            {
                "D64" => "💿",
                "D71" => "💿",
                "D81" => "💿",
                "NDFS" => "🔷",
                "FAT" => "💾",
                "UFS" => "🐧",
                "BSD" => "🐧",
                _ => "📁"
            };

            return new SidebarItemViewModel
            {
                Name = fileName,
                Path = path,
                Icon = icon,
                ItemType = SidebarItemType.Recent,
                FilesystemType = filesystemType,
                LastAccessed = lastAccessed,
                Tooltip = $"Recently visited: {path}\nFilesystem: {filesystemType}\nLast accessed: {lastAccessed:yyyy-MM-dd HH:mm}"
            };
        }

        /// <summary>
        /// Build a pinned Favorite. <paramref name="originType"/> records what the favorite points
        /// at (e.g. Mount or UserFolder) so the click handler knows whether to re-mount or navigate.
        /// </summary>
        public static SidebarItemViewModel CreateFavorite(string name, string path, string filesystemType, SidebarItemType originType)
        {
            // A mount favorite gets a media icon; a folder favorite a folder icon.
            var icon = originType == SidebarItemType.Mount
                ? filesystemType.ToUpper() switch
                {
                    "D64" => "💿",
                    "D71" => "💿",
                    "D81" => "💿",
                    "NDFS" => "🔷",
                    "FAT" => "💾",
                    "UFS" => "🐧",
                    "BSD" => "🐧",
                    _ => "📀"
                }
                : "📁";

            return new SidebarItemViewModel
            {
                Name = name,
                Path = path,
                Icon = icon,
                ItemType = SidebarItemType.Favorite,
                OriginType = originType,
                FilesystemType = filesystemType,
                IsPinned = true,
                Tooltip = $"Favorite ({originType}): {path}"
            };
        }
    }

    /// <summary>
    /// The kinds of entry the navigation sidebar can show.
    /// </summary>
    public enum SidebarItemType
    {
        /// <summary>
        /// A local fixed drive.
        /// </summary>
        Drive,

        /// <summary>
        /// A user folder such as Home, Desktop or Documents.
        /// </summary>
        UserFolder,

        /// <summary>
        /// A mounted disk image.
        /// </summary>
        Mount,

        /// <summary>
        /// A recently visited path.
        /// </summary>
        Recent,

        /// <summary>
        /// A pinned favorite.
        /// </summary>
        Favorite,

        /// <summary>
        /// A section entry. No code in this file creates an item of this kind.
        /// </summary>
        Section
    }
}
