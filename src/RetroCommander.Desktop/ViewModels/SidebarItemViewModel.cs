//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RetroCommander.Desktop.ViewModels
{
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

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SidebarItemType ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        public string FilesystemType
        {
            get => _filesystemType;
            set => SetProperty(ref _filesystemType, value);
        }

        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

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

        public ObservableCollection<SidebarItemViewModel> Children { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

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

    public enum SidebarItemType
    {
        Drive,
        UserFolder,
        Mount,
        Recent,
        Favorite,
        Section
    }
}
