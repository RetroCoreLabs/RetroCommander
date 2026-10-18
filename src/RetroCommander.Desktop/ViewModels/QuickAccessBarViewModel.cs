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
    /// View model for the quick access bar. It holds the lists behind the Mounts and Recents
    /// dropdowns.
    /// </summary>
    public class QuickAccessBarViewModel : INotifyPropertyChanged
    {
        // Drive/folder hot-links were removed from the toolbar (the sidebar already covers them),
        // so this VM now only feeds the Mounts and Recents dropdowns.
        /// <summary>
        /// Gets the items shown in the Mounts dropdown, in the order they were added.
        /// </summary>
        public ObservableCollection<QuickAccessItemViewModel> MountItems { get; } = new();

        /// <summary>
        /// Gets the items shown in the Recents dropdown, newest first, at most 10.
        /// </summary>
        public ObservableCollection<QuickAccessItemViewModel> RecentItems { get; } = new();

        /// <summary>
        /// Raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Puts <paramref name="path"/> at the top of the recent list, stamped with the current
        /// time.
        /// </summary>
        /// <remarks>
        /// An existing entry with the same path is removed first. Only the newest 10 are kept.
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

            var recentItem = QuickAccessItemViewModel.CreateRecent(path, filesystemType, DateTime.Now);
            RecentItems.Insert(0, recentItem);

            // Keep only last 10 items
            while (RecentItems.Count > 10)
            {
                RecentItems.RemoveAt(RecentItems.Count - 1);
            }
        }

        /// <summary>
        /// Adds a mount item to the end of the mount list. An existing mount with the same path is
        /// removed first.
        /// </summary>
        /// <param name="name">
        /// The name shown for the mount.
        /// </param>
        /// <param name="path">
        /// The path that identifies the mount.
        /// </param>
        /// <param name="filesystemType">
        /// The filesystem type label, used for the icon and tooltip.
        /// </param>
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

        /// <summary>
        /// Removes the mount item with the given path. Does nothing if there is none.
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
            }
        }

        /// <summary>
        /// Empties the recent list.
        /// </summary>
        public void ClearRecents()
        {
            RecentItems.Clear();
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
