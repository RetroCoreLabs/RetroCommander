//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RetroCommander.Desktop.ViewModels
{
    public class QuickAccessItemViewModel : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _path = string.Empty;
        private string _icon = string.Empty;
        private string _filesystemType = string.Empty;
        private string _tooltip = string.Empty;
        private DateTime _lastAccessed;

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
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

        public DateTime LastAccessed
        {
            get => _lastAccessed;
            set => SetProperty(ref _lastAccessed, value);
        }

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
