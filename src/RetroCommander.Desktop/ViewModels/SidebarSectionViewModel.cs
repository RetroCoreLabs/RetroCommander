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
    public class SidebarSectionViewModel : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _isExpanded = true;
        private string _icon = string.Empty;
        private bool _isVisible = true;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public ObservableCollection<SidebarItemViewModel> Items { get; } = new();

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

        public static SidebarSectionViewModel CreateThisPcSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "This PC",
                Icon = "🖥️",
                IsExpanded = true
            };
        }

        public static SidebarSectionViewModel CreateUserFoldersSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "User Folders",
                Icon = "👤",
                IsExpanded = true
            };
        }

        public static SidebarSectionViewModel CreateRecentsSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "Recents",
                Icon = "🕒",
                IsExpanded = false
            };
        }

        public static SidebarSectionViewModel CreateFavoritesSection()
        {
            return new SidebarSectionViewModel
            {
                Title = "Favorites",
                Icon = "⭐",
                IsExpanded = true
            };
        }

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
