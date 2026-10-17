//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Abstractions;

/// <summary>
/// Represents a panel displaying a filesystem
/// </summary>
public interface IFileSystemPanel
{
    /// <summary>
    /// Unique identifier for this panel
    /// </summary>
    string Id { get; }

    /// <summary>
    /// URN of the mounted volume
    /// </summary>
    string VolumeUrn { get; }

    /// <summary>
    /// Current path within the filesystem
    /// </summary>
    string CurrentPath { get; }

    /// <summary>
    /// Display name for the panel (shown in UI)
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Filesystem type (NDFS, Azure, TNFS, Windows, etc.)
    /// </summary>
    string FileSystemType { get; }

    /// <summary>
    /// Current directory entries
    /// </summary>
    IReadOnlyList<IFileSystemEntry> Entries { get; }

    /// <summary>
    /// Currently selected entry (the anchor/primary selection — first of <see cref="SelectedEntries"/>
    /// when multiple entries are selected)
    /// </summary>
    IFileSystemEntry? SelectedEntry { get; set; }

    /// <summary>
    /// All currently selected entries for multi-select copy/move/delete. Empty when nothing is
    /// selected. Independent of <see cref="SelectedEntry"/>/<see cref="SelectedIndex"/>, which
    /// remain single-selection concepts used by view/rename/etc.
    /// </summary>
    IReadOnlyList<IFileSystemEntry> SelectedEntries { get; set; }

    /// <summary>
    /// Index of selected entry
    /// </summary>
    int SelectedIndex { get; set; }

    /// <summary>
    /// True if filesystem supports writing
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// True if this is a remote filesystem (needs caching)
    /// </summary>
    bool IsRemote { get; }

    /// <summary>
    /// Refresh directory listing
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Navigate to specified path
    /// </summary>
    Task NavigateAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if can navigate up
    /// </summary>
    bool CanNavigateUp();

    /// <summary>
    /// Navigate to parent directory
    /// </summary>
    Task NavigateUpAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Navigate into selected directory or file
    /// </summary>
    Task NavigateIntoAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new file in this filesystem
    /// </summary>
    Task<IFileSystemEntry> CreateFileAsync(
        string name,
        Stream content,
        IPropertyBag? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new directory
    /// </summary>
    Task<IFileSystemEntry> CreateDirectoryAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an entry
    /// </summary>
    Task DeleteAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rename an entry
    /// </summary>
    Task RenameAsync(
        IFileSystemEntry entry,
        string newName,
        CancellationToken cancellationToken = default);
}
