//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.PropertyBags;

namespace RetroCommander.Core.Providers;

/// <summary>
/// Filesystem panel for native Windows filesystem
/// </summary>
public class NativeFileSystemPanel : IFileSystemPanel
{
    private readonly List<IFileSystemEntry> _entries = new();
    private readonly List<IFileSystemEntry> _selectedEntries = new();
    private string _currentPath;
    private int _selectedIndex = 0;

    /// <summary>
    /// Creates a panel on the host file system with <see cref="FileSystemType"/> set to <c>Windows</c>.
    /// </summary>
    /// <remarks>
    /// The entry list is empty until <see cref="RefreshAsync"/> or a navigate call runs.
    /// </remarks>
    /// <param name="id">
    /// The panel ID.
    /// </param>
    /// <param name="initialPath">
    /// The starting folder; it is turned into a full path but not checked to exist.
    /// </param>
    public NativeFileSystemPanel(string id, string initialPath)
    {
        Id = id;
        _currentPath = Path.GetFullPath(initialPath);
        FileSystemType = "Windows";
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <summary>
    /// Gets a <c>file:///</c> URN built from the current path, with backslashes turned into forward slashes.
    /// </summary>
    public string VolumeUrn => $"file:///{_currentPath.Replace("\\", "/")}";

    /// <summary>
    /// Gets the full host path of the folder being shown.
    /// </summary>
    public string CurrentPath => _currentPath;

    /// <summary>
    /// Gets the panel title, which is the current path.
    /// </summary>
    public string DisplayName => _currentPath;

    /// <summary>
    /// Gets the file system type name, always <c>Windows</c>.
    /// </summary>
    public string FileSystemType { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IFileSystemEntry> Entries => _entries;

    /// <summary>
    /// Gets or sets the entry at <see cref="SelectedIndex"/>, or null if the index is outside the list.
    /// </summary>
    /// <remarks>
    /// Setting an entry that is not in <see cref="Entries"/>, or null, leaves the selection unchanged.
    /// </remarks>
    public IFileSystemEntry? SelectedEntry
    {
        get => _selectedIndex >= 0 && _selectedIndex < _entries.Count ? _entries[_selectedIndex] : null;
        set
        {
            if (value != null)
            {
                var index = _entries.IndexOf(value);
                if (index >= 0)
                    _selectedIndex = index;
            }
        }
    }

    /// <summary>
    /// Gets or sets the index of the selected entry. A set value is clamped to the entry list, and never goes below 0.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Max(0, Math.Min(value, _entries.Count - 1));
    }

    /// <summary>
    /// Gets or sets all selected entries. Setting copies the given entries into the panel's own list; null clears it.
    /// </summary>
    public IReadOnlyList<IFileSystemEntry> SelectedEntries
    {
        get => _selectedEntries;
        set
        {
            _selectedEntries.Clear();
            if (value != null)
            {
                for (int i = 0; i < value.Count; i++)
                {
                    _selectedEntries.Add(value[i]);
                }
            }
        }
    }

    /// <summary>
    /// Gets false; the host file system panel is always treated as writable.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets false; the host file system is local.
    /// </summary>
    public bool IsRemote => false;

    /// <summary>
    /// Lists the current folder again, directories first and then files, and rebuilds <see cref="Entries"/>.
    /// </summary>
    /// <remarks>
    /// The work is done before the method returns. If the selected index is past the end of the
    /// new list, it moves to the last entry.
    /// </remarks>
    /// <param name="cancellationToken">
    /// Not used.
    /// </param>
    /// <returns>
    /// A task that is already complete.
    /// </returns>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _entries.Clear();

        var dirInfo = new DirectoryInfo(_currentPath);

        // Add directories first
        foreach (var dir in dirInfo.GetDirectories())
        {
            _entries.Add(new NativeFileSystemEntry(dir));
        }

        // Then add files
        foreach (var file in dirInfo.GetFiles())
        {
            _entries.Add(new NativeFileSystemEntry(file));
        }

        // Ensure selected index is valid
        if (_selectedIndex >= _entries.Count)
            _selectedIndex = Math.Max(0, _entries.Count - 1);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Makes <paramref name="path"/> the current folder, selects the first entry, and lists it.
    /// </summary>
    /// <param name="path">
    /// The folder to show; it is turned into a full path first.
    /// </param>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="RefreshAsync"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the new listing is loaded.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the folder does not exist.
    /// </exception>
    public Task NavigateAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");

        _currentPath = fullPath;
        _selectedIndex = 0;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Tells whether the current folder has a parent folder.
    /// </summary>
    /// <returns>
    /// True when the current folder has a parent; false at a drive or file system root.
    /// </returns>
    public bool CanNavigateUp()
    {
        var parent = Directory.GetParent(_currentPath);
        return parent != null;
    }

    /// <summary>
    /// Navigates to the parent of the current folder.
    /// </summary>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="NavigateAsync"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the new listing is loaded.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="CanNavigateUp"/> returns false.
    /// </exception>
    public Task NavigateUpAsync(CancellationToken cancellationToken = default)
    {
        if (!CanNavigateUp())
            throw new InvalidOperationException("Already at root directory");

        var parent = Directory.GetParent(_currentPath);
        return NavigateAsync(parent!.FullName, cancellationToken);
    }

    /// <summary>
    /// Opens a directory entry by navigating to its full path.
    /// </summary>
    /// <param name="entry">
    /// The directory to open.
    /// </param>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="NavigateAsync"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the new listing is loaded.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="entry"/> is not a directory.
    /// </exception>
    public async Task NavigateIntoAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default)
    {
        if (!entry.IsDirectory)
            throw new InvalidOperationException("Cannot navigate into a file");

        await NavigateAsync(entry.FullPath, cancellationToken);
    }

    /// <summary>
    /// Writes a new file into the current folder, then refreshes the listing.
    /// </summary>
    /// <remarks>
    /// The name is passed through <see cref="SanitizeWindowsFileName"/> first, and an existing file
    /// with that name is replaced. When <paramref name="properties"/> is given, its created and
    /// modified dates, read-only flag and Windows attributes are applied to the new file.
    /// </remarks>
    /// <param name="name">
    /// Name of the new file, before cleaning.
    /// </param>
    /// <param name="content">
    /// The file content, read from its current position to the end.
    /// </param>
    /// <param name="properties">
    /// Optional properties to apply to the new file.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the copy and the refresh.
    /// </param>
    /// <returns>
    /// An entry for the new file.
    /// </returns>
    public async Task<IFileSystemEntry> CreateFileAsync(
        string name,
        Stream content,
        IPropertyBag? properties = null,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_currentPath, SanitizeWindowsFileName(name));

        using (var fileStream = File.Create(filePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(filePath);

        // Apply properties if provided
        if (properties != null)
        {
            ApplyProperties(fileInfo, properties);
        }

        var entry = new NativeFileSystemEntry(fileInfo);
        await RefreshAsync(cancellationToken);
        return entry;
    }

    /// <summary>
    /// Creates a folder in the current folder. The listing is not refreshed.
    /// </summary>
    /// <param name="name">
    /// Name of the new folder; it is passed through <see cref="SanitizeWindowsFileName"/> first.
    /// </param>
    /// <param name="cancellationToken">
    /// Not used.
    /// </param>
    /// <returns>
    /// An entry for the new folder.
    /// </returns>
    public Task<IFileSystemEntry> CreateDirectoryAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var dirPath = Path.Combine(_currentPath, SanitizeWindowsFileName(name));
        var dirInfo = Directory.CreateDirectory(dirPath);
        var entry = new NativeFileSystemEntry(dirInfo);
        return Task.FromResult<IFileSystemEntry>(entry);
    }

    /// <summary>
    /// Maps a source filesystem's name onto NTFS-safe characters. Source filesystems such as
    /// NDFS use ':' as their NAME:TYPE extension separator (see NdfsFileSystem.ExtensionSeparator);
    /// on NTFS a ':' inside a filename is instead parsed as an Alternate Data Stream marker, so
    /// File.Create("NAME:TYPE") silently writes the payload into a hidden ADS and leaves the
    /// visible file at 0 bytes. Translate ':' to the Windows/FAT extension separator '.' (mirrors
    /// Fat12/16/32FileSystem.ExtensionSeparator) and neutralize the remaining Windows-reserved
    /// filename characters so the visible file actually contains the copied data.
    /// </summary>
    public static string SanitizeWindowsFileName(string name)
    {
        Span<char> buffer = name.Length <= 260 ? stackalloc char[name.Length] : new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == ':')
            {
                buffer[i] = '.';
            }
            else if (c == '<' || c == '>' || c == '"' || c == '|' || c == '?' || c == '*' ||
                     c == '\\' || c == '/' || c < 32)
            {
                buffer[i] = '_';
            }
            else
            {
                buffer[i] = c;
            }
        }

        return new string(buffer);
    }

    /// <summary>
    /// Deletes a file, or a folder together with everything inside it. The listing is not refreshed.
    /// </summary>
    /// <param name="entry">
    /// The file or folder to delete.
    /// </param>
    /// <param name="cancellationToken">
    /// Not used.
    /// </param>
    /// <returns>
    /// A task that is already complete.
    /// </returns>
    public Task DeleteAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.IsDirectory)
        {
            Directory.Delete(entry.FullPath, recursive: true);
        }
        else
        {
            File.Delete(entry.FullPath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Renames a file or folder within its own parent folder. The listing is not refreshed.
    /// </summary>
    /// <param name="entry">
    /// The file or folder to rename.
    /// </param>
    /// <param name="newName">
    /// The new name, used as given.
    /// </param>
    /// <param name="cancellationToken">
    /// Not used.
    /// </param>
    /// <returns>
    /// A task that is already complete.
    /// </returns>
    public Task RenameAsync(
        IFileSystemEntry entry,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var newPath = Path.Combine(Path.GetDirectoryName(entry.FullPath)!, newName);

        if (entry.IsDirectory)
        {
            Directory.Move(entry.FullPath, newPath);
        }
        else
        {
            File.Move(entry.FullPath, newPath);
        }

        return Task.CompletedTask;
    }

    private void ApplyProperties(FileInfo fileInfo, IPropertyBag properties)
    {
        // Apply timestamps
        if (properties.TryGetProperty<DateTime?>(StandardProperties.CreatedDate, out var created) && created.HasValue)
        {
            fileInfo.CreationTimeUtc = created.Value;
        }

        if (properties.TryGetProperty<DateTime?>(StandardProperties.ModifiedDate, out var modified) && modified.HasValue)
        {
            fileInfo.LastWriteTimeUtc = modified.Value;
        }

        // Apply read-only attribute
        if (properties.TryGetProperty<bool>(StandardProperties.IsReadOnly, out var isReadOnly))
        {
            fileInfo.IsReadOnly = isReadOnly;
        }

        // Apply Windows attributes if available
        if (properties.TryGetProperty<FileAttributes>(WindowsProperties.Attributes, out var attributes))
        {
            fileInfo.Attributes = attributes;
        }
    }
}
