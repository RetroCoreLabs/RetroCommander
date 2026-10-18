//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RetroCommander.Desktop.ViewModels
{
    /// <summary>
    /// One entry in a quick access bar dropdown: a drive, folder, mount or recent path.
    /// </summary>
    public class QuickAccessItemViewModel : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _path = string.Empty;
        private string _icon = string.Empty;
        private string _filesystemType = string.Empty;
        private string _tooltip = string.Empty;
        private DateTime _lastAccessed;

        /// <summary>
        /// Gets or sets the text shown for the item.
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// Gets or sets the path the item points at.
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
        /// Gets or sets the filesystem type label, such as <c>NDFS</c> or <c>D64</c>. Empty for
        /// drives and folders.
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
        /// Gets or sets when the item was last visited. Set for recent items; left at the default
        /// value for the other kinds.
        /// </summary>
        public DateTime LastAccessed
        {
            get => _lastAccessed;
            set => SetProperty(ref _lastAccessed, value);
        }

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
        /// Builds a drive item named after <paramref name="driveLetter"/> with a colon, for
        /// example <c>C:</c>.
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
        public static QuickAccessItemViewModel CreateDrive(string driveLetter, string path)
        {
            return new QuickAccessItemViewModel
            {
                DisplayName = $"{driveLetter}:",
                Path = path,
                Icon = "💾",
                Tooltip = $"Local drive {driveLetter}:"
            };
        }

        /// <summary>
        /// Builds a folder item.
        /// </summary>
        /// <param name="name">
        /// The name shown for the folder.
        /// </param>
        /// <param name="path">
        /// The full path of the folder.
        /// </param>
        /// <param name="icon">
        /// The icon text; a folder emoji when left out.
        /// </param>
        /// <returns>
        /// The new folder item.
        /// </returns>
        public static QuickAccessItemViewModel CreateFolder(string name, string path, string icon = "📁")
        {
            return new QuickAccessItemViewModel
            {
                DisplayName = name,
                Path = path,
                Icon = icon,
                Tooltip = $"User folder: {path}"
            };
        }

        /// <summary>
        /// Builds a mount item.
        /// </summary>
        /// <param name="name">
        /// The name shown for the mount.
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
        public static QuickAccessItemViewModel CreateMount(string name, string path, string filesystemType)
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

            return new QuickAccessItemViewModel
            {
                DisplayName = name,
                Path = path,
                Icon = icon,
                FilesystemType = filesystemType,
                Tooltip = $"Mounted {filesystemType} filesystem: {path}"
            };
        }

        /// <summary>
        /// Builds a recent item named after the last part of <paramref name="path"/>.
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
        public static QuickAccessItemViewModel CreateRecent(string path, string filesystemType, DateTime lastAccessed)
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

            return new QuickAccessItemViewModel
            {
                DisplayName = fileName,
                Path = path,
                Icon = icon,
                FilesystemType = filesystemType,
                LastAccessed = lastAccessed,
                Tooltip = $"Recently visited: {path}\nFilesystem: {filesystemType}\nLast accessed: {lastAccessed:yyyy-MM-dd HH:mm}"
            };
        }
    }
}
