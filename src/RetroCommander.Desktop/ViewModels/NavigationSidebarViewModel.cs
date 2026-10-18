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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RetroCommander.Desktop.ViewModels
{
    /// <summary>
    /// View model for the navigation sidebar: the five sections, the recent and mount lists, the
    /// selected item, and the favorites that are saved to disk.
    /// </summary>
    public class NavigationSidebarViewModel : INotifyPropertyChanged
    {
        private SidebarItemViewModel? _selectedItem;

        /// <summary>
        /// Gets the "This PC" section, filled with the fixed drives that are ready at startup.
        /// </summary>
        public SidebarSectionViewModel ThisPcSection { get; } = SidebarSectionViewModel.CreateThisPcSection();

        /// <summary>
        /// Gets the "User Folders" section, filled at startup with Home, Desktop and Documents
        /// when those folders exist.
        /// </summary>
        public SidebarSectionViewModel UserFoldersSection { get; } = SidebarSectionViewModel.CreateUserFoldersSection();

        /// <summary>
        /// Gets the "Favorites" section, loaded from the saved favorites file at startup.
        /// </summary>
        public SidebarSectionViewModel FavoritesSection { get; } = SidebarSectionViewModel.CreateFavoritesSection();

        /// <summary>
        /// Gets the "Recents" section. <see cref="AddRecentItem"/> keeps it in step with
        /// <see cref="RecentItems"/>.
        /// </summary>
        public SidebarSectionViewModel RecentsSection { get; } = SidebarSectionViewModel.CreateRecentsSection();

        /// <summary>
        /// Gets the "Mounts" section. <see cref="AddMount"/> and <see cref="RemoveMount"/> keep it
        /// in step with <see cref="MountItems"/>.
        /// </summary>
        public SidebarSectionViewModel MountsSection { get; } = SidebarSectionViewModel.CreateMountsSection();

        /// <summary>
        /// Gets the recent items, newest first, at most 10.
        /// </summary>
        public ObservableCollection<SidebarItemViewModel> RecentItems { get; } = new();

        /// <summary>
        /// Gets the mounted disk image items, in the order they were added.
        /// </summary>
        public ObservableCollection<SidebarItemViewModel> MountItems { get; } = new();

        /// <summary>
        /// Gets or sets the selected sidebar item, or null when none. Changing it also raises
        /// change notifications for <see cref="CanMount"/>, <see cref="CanUnmount"/> and
        /// <see cref="CanPin"/>.
        /// </summary>
        public SidebarItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        /// <summary>
        /// Gets a value that is true when the selected item is a <see cref="SidebarItemType.Drive"/>.
        /// </summary>
        public bool CanMount => SelectedItem?.ItemType == SidebarItemType.Drive;

        /// <summary>
        /// Gets a value that is true when the selected item is a <see cref="SidebarItemType.Mount"/>.
        /// </summary>
        public bool CanUnmount => SelectedItem?.ItemType == SidebarItemType.Mount;

        /// <summary>
        /// Gets a value that is true when the selected item is a <see cref="SidebarItemType.Recent"/>
        /// or a <see cref="SidebarItemType.Mount"/>.
        /// </summary>
        public bool CanPin => SelectedItem?.ItemType == SidebarItemType.Recent || SelectedItem?.ItemType == SidebarItemType.Mount;

        /// <summary>
        /// Raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates the view model and fills the drive, user folder and favorites sections.
        /// </summary>
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
        /// <summary>
        /// Checks whether a favorite already points at the given path. The match ignores case.
        /// </summary>
        /// <param name="path">
        /// The mount URN or folder path to look for.
        /// </param>
        /// <returns>
        /// True if the Favorites section has an item with <paramref name="path"/>; otherwise false.
        /// </returns>
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
        /// <summary>
        /// Adds a favorite to the Favorites section and saves the favorites file. Does nothing if a
        /// favorite with the same path already exists.
        /// </summary>
        /// <param name="name">
        /// The name shown for the favorite.
        /// </param>
        /// <param name="path">
        /// The mount URN or folder path the favorite points at.
        /// </param>
        /// <param name="filesystemType">
        /// The filesystem type label of the target, used to pick the icon of a mount favorite.
        /// </param>
        /// <param name="originType">
        /// The kind of item the favorite points at, which decides whether a click re-mounts or
        /// navigates.
        /// </param>
        public void AddFavorite(string name, string path, string filesystemType, SidebarItemType originType)
        {
            if (IsFavorite(path))
                return;

            var item = SidebarItemViewModel.CreateFavorite(name, path, filesystemType, originType);
            FavoritesSection.Items.Add(item);
            SaveFavorites();
        }

        // Remove a favorite by its path and persist. No-op if not present.
        /// <summary>
        /// Removes the first favorite whose path matches, ignoring case, and saves the favorites
        /// file. Does nothing if no favorite matches.
        /// </summary>
        /// <param name="path">
        /// The mount URN or folder path of the favorite to remove.
        /// </param>
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

        /// <summary>
        /// Puts <paramref name="path"/> at the top of the recent list and the Recents section,
        /// stamped with the current time.
        /// </summary>
        /// <remarks>
        /// An existing entry with the same path is taken out of the recent list first. Only the
        /// newest 10 are kept.
        /// </remarks>
        /// <param name="path">
        /// The path that was visited.
        /// </param>
        /// <param name="filesystemType">
        /// The filesystem type label of the path, used for the icon and tooltip. May be empty.
        /// </param>
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

        /// <summary>
        /// Adds a mount item to the end of the mount list and the Mounts section. An existing
        /// mount with the same path is removed first.
        /// </summary>
        /// <param name="name">
        /// The name of the mount; the item shows it followed by the filesystem type in brackets.
        /// </param>
        /// <param name="path">
        /// The path that identifies the mount.
        /// </param>
        /// <param name="filesystemType">
        /// The filesystem type label, used for the icon, name and tooltip.
        /// </param>
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

        /// <summary>
        /// Removes the mount item with the given path from the mount list and the Mounts section.
        /// Does nothing if there is none.
        /// </summary>
        /// <param name="path">
        /// The path of the mount to remove.
        /// </param>
        public void RemoveMount(string path)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                MountItems.Remove(existingItem);
                MountsSection.Items.Remove(existingItem);
            }
        }

        /// <summary>
        /// Rewrites the tooltip of the mount item with the given path to include a status line.
        /// Does nothing if there is no such mount.
        /// </summary>
        /// <param name="path">
        /// The path of the mount to update.
        /// </param>
        /// <param name="status">
        /// The status text to show in the tooltip.
        /// </param>
        public void UpdateMountStatus(string path, string status)
        {
            var existingItem = MountItems.FirstOrDefault(item => item.Path == path);
            if (existingItem != null)
            {
                existingItem.Tooltip = $"Mounted {existingItem.FilesystemType} filesystem: {path}\nStatus: {status}";
            }
        }

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
    }
}
