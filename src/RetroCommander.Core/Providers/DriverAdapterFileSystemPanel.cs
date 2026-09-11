//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using RetroCommander.Core.Abstractions;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Providers;

/// <summary>
/// Adapter that wraps IFileSystemDriver into IFileSystemPanel
/// </summary>
public class DriverAdapterFileSystemPanel : IFileSystemPanel, IDisposable
{
    private readonly IFileSystemDriver _driver;
    private readonly List<IFileSystemEntry> _entries = new();
    private readonly List<IFileSystemEntry> _selectedEntries = new();
    private string _currentPath;
    private int _selectedIndex = 0;
    private readonly string _volumeUrn;

    public DriverAdapterFileSystemPanel(string id, IFileSystemDriver driver, string volumeUrn)
    {
        Id = id;
        _driver = driver;
        _volumeUrn = volumeUrn;
        _currentPath = "/";
        FileSystemType = driver.FileSystemType;
    }

    public string Id { get; }

    /// <summary>
    /// The live mounted driver backing this panel. Exposed so filesystem-specific UI (e.g. the
    /// NDFS disk-info window's Clear-Entered / Rename actions) can reach the driver's native API
    /// on the SAME live mount — opening a second handle to the image would hit a sharing conflict.
    /// </summary>
    public IFileSystemDriver Driver => _driver;

    public string VolumeUrn => _volumeUrn;

    public string CurrentPath => _currentPath;

    public string DisplayName => $"{_driver.VolumeLabel} ({FileSystemType}) - {_currentPath}";

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

    public bool IsReadOnly => !_driver.CanWrite;

    public bool IsRemote => false; // Local disk images

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _entries.Clear();

        var directoryEntries = _driver.ListDirectory(_currentPath);
        foreach (var entry in directoryEntries)
        {
            var fileEntry = new DriverAdapterFileSystemEntry(_driver, entry.Metadata);
            _entries.Add(fileEntry);
        }

        // Ensure selected index is valid
        if (_selectedIndex >= _entries.Count)
            _selectedIndex = Math.Max(0, _entries.Count - 1);

        return Task.CompletedTask;
    }

    public Task NavigateAsync(string path, CancellationToken cancellationToken = default)
    {
        _currentPath = path;
        _selectedIndex = 0;
        return RefreshAsync(cancellationToken);
    }

    public bool CanNavigateUp()
    {
        return _currentPath != "/" && !string.IsNullOrEmpty(_currentPath);
    }

    public Task NavigateUpAsync(CancellationToken cancellationToken = default)
    {
        if (!CanNavigateUp())
            throw new InvalidOperationException("Already at root");

        // Go up one directory level
        var lastSlash = _currentPath.TrimEnd('/').LastIndexOf('/');
        _currentPath = lastSlash <= 0 ? "/" : _currentPath.Substring(0, lastSlash);

        return RefreshAsync(cancellationToken);
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
        // Read content
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var data = ms.ToArray();

        // Create path
        var filePath = _currentPath == "/"
            ? $"/{name}"
            : $"{_currentPath}/{name}";

        // Drivers with richer, filesystem-native metadata (currently NDFS) accept it via
        // IExtendedAttributeSupport.WriteFileWithProperties instead of a plain byte write. Write
        // through that path whenever the driver supports it and the caller supplied properties, so
        // metadata a copy already collected (see SmartFileOperations.CopyAsync) is actually
        // persisted instead of silently dropped — this adapter stays filesystem-agnostic and passes
        // the whole bag through; it is the driver's job (not this generic adapter's) to pick out
        // whichever keys it understands and ignore the rest, exactly as NdfsPropertyBagSerializer
        // already does key-by-key.
        if (properties != null && _driver is IExtendedAttributeSupport xa &&
            xa.GetCapabilities().SupportsExtendedAttributes)
        {
            xa.WriteFileWithProperties(filePath, data, ToPropertyDictionary(properties));
        }
        else
        {
            _driver.WriteFile(filePath, data);
        }

        // Get metadata for the new file
        var metadata = _driver.GetMetadata(filePath);
        if (metadata == null)
            throw new InvalidOperationException("File created but metadata not found");

        var entry = new DriverAdapterFileSystemEntry(_driver, metadata);
        await RefreshAsync(cancellationToken);

        return entry;
    }

    public async Task<IFileSystemEntry> CreateDirectoryAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        // Build the path the same way CreateFileAsync does (root vs sub-path).
        var dirPath = _currentPath == "/"
            ? $"/{name}"
            : $"{_currentPath}/{name}";

        // Delegates to the driver (drivers without directory support throw NotSupported).
        // For NDFS this creates a User at root level.
        _driver.CreateDirectory(dirPath);
        await RefreshAsync(cancellationToken);

        // Return the freshly-listed entry so the caller has a real handle to it. Match
        // case-INSENSITIVELY: some filesystems normalise names (NDFS upper-cases user names, so
        // "test" becomes "TEST"). An exact-case match here used to miss it and throw even though
        // the directory was created fine.
        for (int i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return Entries[i];
            }
        }

        // Created but not listed under that exact name — surface metadata if the driver has it.
        var metadata = _driver.GetMetadata(dirPath);
        if (metadata != null)
        {
            return new DriverAdapterFileSystemEntry(_driver, metadata);
        }

        // The create itself SUCCEEDED (the driver call did not throw); we just couldn't resolve a
        // handle to return. Do NOT throw here — that would surface a false "failed" to the user
        // and skip their refresh. Returning the first entry (or a directory entry) is acceptable;
        // the panel has already been refreshed so the new directory is visible.
        if (Entries.Count > 0)
        {
            return Entries[0];
        }

        throw new InvalidOperationException($"Directory '{name}' was created but the panel is empty after refresh");
    }

    public Task DeleteAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.IsDirectory)
            throw new NotSupportedException("Directory deletion not supported");

        _driver.DeleteFile(entry.FullPath);
        return Task.CompletedTask;
    }

    public Task RenameAsync(
        IFileSystemEntry entry,
        string newName,
        CancellationToken cancellationToken = default)
    {
        // Delegates to the driver's rename (drivers without rename support throw NotSupported).
        _driver.RenameFile(entry.FullPath, newName);
        return RefreshAsync(cancellationToken);
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }

    /// <summary>
    /// Adapts an IPropertyBag (whose values may be null) to the non-nullable
    /// IDictionary&lt;string, object&gt; shape IExtendedAttributeSupport.WriteFileWithProperties
    /// expects, dropping null-valued entries (not representable there).
    /// </summary>
    private static IDictionary<string, object> ToPropertyDictionary(IPropertyBag properties)
    {
        var result = new Dictionary<string, object>();
        var all = properties.GetAllProperties();
        foreach (var kvp in all)
        {
            if (kvp.Value != null)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }
}
