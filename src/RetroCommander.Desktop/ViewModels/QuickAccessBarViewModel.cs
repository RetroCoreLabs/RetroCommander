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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RetroCommander.Desktop.ViewModels
{
    public class QuickAccessBarViewModel : INotifyPropertyChanged
    {
        // Drive/folder hot-links were removed from the toolbar (the sidebar already covers them),
        // so this VM now only feeds the Mounts and Recents dropdowns.
        public ObservableCollection<QuickAccessItemViewModel> MountItems { get; } = new();
        public ObservableCollection<QuickAccessItemViewModel> RecentItems { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public void AddRecentItem(string path, string filesystemType = "")
        {
            var existingItem = RecentItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                RecentItems.Remove(existingItem);
            }

            var recentItem = QuickAccessItemViewModel.CreateRecent(path, filesystemType, DateTime.Now);
            RecentItems.Insert(0, recentItem);

            // Keep only last 10 items
            while (RecentItems.Count > 10)
            {
                RecentItems.RemoveAt(RecentItems.Count - 1);
            }
        }

        public void AddMount(string name, string path, string filesystemType)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                MountItems.Remove(existingItem);
            }

            var mountItem = QuickAccessItemViewModel.CreateMount(name, path, filesystemType);
            MountItems.Add(mountItem);
        }

        public void RemoveMount(string path)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                MountItems.Remove(existingItem);
            }
        }

        public void ClearRecents()
        {
            RecentItems.Clear();
        }

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
    }
}
