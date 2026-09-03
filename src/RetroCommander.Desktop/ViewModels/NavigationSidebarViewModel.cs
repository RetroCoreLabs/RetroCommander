//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
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
    public class NavigationSidebarViewModel : INotifyPropertyChanged
    {
        private SidebarItemViewModel? _selectedItem;

        public SidebarSectionViewModel ThisPcSection { get; } = SidebarSectionViewModel.CreateThisPcSection();
        public SidebarSectionViewModel UserFoldersSection { get; } = SidebarSectionViewModel.CreateUserFoldersSection();
        public SidebarSectionViewModel FavoritesSection { get; } = SidebarSectionViewModel.CreateFavoritesSection();
        public SidebarSectionViewModel RecentsSection { get; } = SidebarSectionViewModel.CreateRecentsSection();
        public SidebarSectionViewModel MountsSection { get; } = SidebarSectionViewModel.CreateMountsSection();

        public ObservableCollection<SidebarItemViewModel> RecentItems { get; } = new();
        public ObservableCollection<SidebarItemViewModel> MountItems { get; } = new();

        public SidebarItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public bool CanMount => SelectedItem?.ItemType == SidebarItemType.Drive;
        public bool CanUnmount => SelectedItem?.ItemType == SidebarItemType.Mount;
        public bool CanPin => SelectedItem?.ItemType == SidebarItemType.Recent || SelectedItem?.ItemType == SidebarItemType.Mount;

        public event PropertyChangedEventHandler? PropertyChanged;

        public NavigationSidebarViewModel()
        {
            InitializeDrives();
            InitializeUserFolders();
            LoadFavorites();

            // Wire up property changes
            PropertyChanged += OnPropertyChanged;
        }

        // Restore pinned favorites from disk into the Favorites section.
        private void LoadFavorites()
        {
            try
            {
                List<FavoriteRecord> records = FavoritesStore.Load();
                for (int i = 0; i < records.Count; i++)
                {
                    FavoriteRecord r = records[i];
                    if (!Enum.TryParse(r.OriginType, out SidebarItemType originType))
                        originType = SidebarItemType.UserFolder;

                    var item = SidebarItemViewModel.CreateFavorite(r.Name, r.Path, r.FilesystemType, originType);
                    FavoritesSection.Items.Add(item);
                }
            }
            catch (Exception)
            {
                // Never let a bad favorites file break the sidebar.
            }
        }

        // Serialize the current favorites back to disk (no LINQ: explicit projection loop).
        private void SaveFavorites()
        {
            var records = new List<FavoriteRecord>(FavoritesSection.Items.Count);
            for (int i = 0; i < FavoritesSection.Items.Count; i++)
            {
                SidebarItemViewModel item = FavoritesSection.Items[i];
                records.Add(new FavoriteRecord
                {
                    Name = item.Name,
                    Path = item.Path,
                    FilesystemType = item.FilesystemType,
                    OriginType = item.OriginType.ToString()
                });
            }
            FavoritesStore.Save(records);
        }

        // True if a favorite already points at this path (mount URN or folder path).
        public bool IsFavorite(string path)
        {
            for (int i = 0; i < FavoritesSection.Items.Count; i++)
            {
                if (string.Equals(FavoritesSection.Items[i].Path, path, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Pin a Recent/Mount as a Favorite. Idempotent (ignores duplicates by path) and persists.
        public void AddFavorite(string name, string path, string filesystemType, SidebarItemType originType)
        {
            if (IsFavorite(path))
                return;

            var item = SidebarItemViewModel.CreateFavorite(name, path, filesystemType, originType);
            FavoritesSection.Items.Add(item);
            SaveFavorites();
        }

        // Remove a favorite by its path and persist. No-op if not present.
        public void RemoveFavorite(string path)
        {
            for (int i = 0; i < FavoritesSection.Items.Count; i++)
            {
                if (string.Equals(FavoritesSection.Items[i].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    FavoritesSection.Items.RemoveAt(i);
                    SaveFavorites();
                    return;
                }
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedItem))
            {
                OnPropertyChanged(nameof(CanMount));
                OnPropertyChanged(nameof(CanUnmount));
                OnPropertyChanged(nameof(CanPin));
            }
        }

        private void InitializeDrives()
        {
            try
            {
                // Add system drives
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .OrderBy(d => d.Name);

                foreach (var drive in drives)
                {
                    var item = SidebarItemViewModel.CreateDrive(drive.Name[0].ToString(), drive.Name);
                    ThisPcSection.Items.Add(item);
                }
            }
            catch (Exception)
            {
                // Handle permission errors gracefully
            }
        }

        private void InitializeUserFolders()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                // Media folders (Pictures/Music/Videos) are intentionally excluded — they are
                // not relevant for a retro-filesystem manager.
                var specialFolders = new[]
                {
                    (Environment.SpecialFolder.UserProfile, "Home"),
                    (Environment.SpecialFolder.Desktop, "Desktop"),
                    (Environment.SpecialFolder.MyDocuments, "Documents")
                };

                foreach (var (folder, name) in specialFolders)
                {
                    try
                    {
                        var path = Environment.GetFolderPath(folder);
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            var item = SidebarItemViewModel.CreateUserFolder(name, path);
                            UserFoldersSection.Items.Add(item);
                        }
                    }
                    catch (Exception)
                    {
                        // Skip folders that can't be accessed
                    }
                }
            }
            catch (Exception)
            {
                // Handle permission errors gracefully
            }
        }

        public void AddRecentItem(string path, string filesystemType = "")
        {
            var existingItem = RecentItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                RecentItems.Remove(existingItem);
            }

            var recentItem = SidebarItemViewModel.CreateRecent(path, filesystemType, DateTime.Now);
            RecentItems.Insert(0, recentItem);
            RecentsSection.Items.Insert(0, recentItem);

            // Keep only last 10 items
            while (RecentItems.Count > 10)
            {
                var lastItem = RecentItems.Last();
                RecentItems.Remove(lastItem);
                RecentsSection.Items.Remove(lastItem);
            }
        }

        public void AddMount(string name, string path, string filesystemType)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                MountItems.Remove(existingItem);
                MountsSection.Items.Remove(existingItem);
            }

            var mountItem = SidebarItemViewModel.CreateMount(name, path, filesystemType);
            MountItems.Add(mountItem);
            MountsSection.Items.Add(mountItem);
        }

        public void RemoveMount(string path)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                MountItems.Remove(existingItem);
                MountsSection.Items.Remove(existingItem);
            }
        }

        public void UpdateMountStatus(string path, string status)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                existingItem.Tooltip = $"Mounted {existingItem.FilesystemType} filesystem: {path}\nStatus: {status}";
            }
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
