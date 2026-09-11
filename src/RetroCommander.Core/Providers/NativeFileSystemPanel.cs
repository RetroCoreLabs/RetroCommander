//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
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

    public NativeFileSystemPanel(string id, string initialPath)
    {
        Id = id;
        _currentPath = Path.GetFullPath(initialPath);
        FileSystemType = "Windows";
    }

    public string Id { get; }

    public string VolumeUrn => $"file:///{_currentPath.Replace("\\", "/")}";

    public string CurrentPath => _currentPath;

    public string DisplayName => _currentPath;

    public string FileSystemType { get; }

    public IReadOnlyList<IFileSystemEntry> Entries => _entries;

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

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Max(0, Math.Min(value, _entries.Count - 1));
    }

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

    public bool IsReadOnly => false;

    public bool IsRemote => false;

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

    public Task NavigateAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");

        _currentPath = fullPath;
        _selectedIndex = 0;
        return RefreshAsync(cancellationToken);
    }

    public bool CanNavigateUp()
    {
        var parent = Directory.GetParent(_currentPath);
        return parent != null;
    }

    public Task NavigateUpAsync(CancellationToken cancellationToken = default)
    {
        if (!CanNavigateUp())
            throw new InvalidOperationException("Already at root directory");

        var parent = Directory.GetParent(_currentPath);
        return NavigateAsync(parent!.FullName, cancellationToken);
    }

    public async Task NavigateIntoAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default)
    {
        if (!entry.IsDirectory)
            throw new InvalidOperationException("Cannot navigate into a file");

        await NavigateAsync(entry.FullPath, cancellationToken);
    }

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
