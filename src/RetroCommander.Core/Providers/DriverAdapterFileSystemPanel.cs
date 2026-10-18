//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
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

    /// <summary>
    /// Creates a panel over a mounted driver, starting at the root path <c>/</c>.
    /// </summary>
    /// <remarks>
    /// The entry list is empty until <see cref="RefreshAsync"/> or a navigate call runs.
    /// </remarks>
    /// <param name="id">
    /// The panel ID.
    /// </param>
    /// <param name="driver">
    /// The mounted file system driver the panel reads and writes through. It is disposed with the panel.
    /// </param>
    /// <param name="volumeUrn">
    /// The URN the volume was mounted from.
    /// </param>
    public DriverAdapterFileSystemPanel(string id, IFileSystemDriver driver, string volumeUrn)
    {
        Id = id;
        _driver = driver;
        _volumeUrn = volumeUrn;
        _currentPath = "/";
        FileSystemType = driver.FileSystemType;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <summary>
    /// The live mounted driver backing this panel. Exposed so filesystem-specific UI (e.g. the
    /// NDFS disk-info window's Clear-Entered / Rename actions) can reach the driver's native API
    /// on the SAME live mount — opening a second handle to the image would hit a sharing conflict.
    /// </summary>
    public IFileSystemDriver Driver => _driver;

    /// <inheritdoc/>
    public string VolumeUrn => _volumeUrn;

    /// <summary>
    /// Gets the current path inside the volume, using <c>/</c> as separator; the root is <c>/</c>.
    /// </summary>
    public string CurrentPath => _currentPath;

    /// <summary>
    /// Gets the panel title, built as the driver's volume label, the file system type in brackets, and the current path.
    /// </summary>
    public string DisplayName => $"{_driver.VolumeLabel} ({FileSystemType}) - {_currentPath}";

    /// <summary>
    /// Gets the file system type name, taken from the driver when the panel was created.
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
    /// Gets whether the volume can only be read; true when the driver reports it cannot write.
    /// </summary>
    public bool IsReadOnly => !_driver.CanWrite;

    /// <summary>
    /// Gets false; a driver panel is always treated as a local disk image.
    /// </summary>
    public bool IsRemote => false; // Local disk images

    /// <summary>
    /// Lists the current path through the driver again and rebuilds <see cref="Entries"/>.
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

    /// <summary>
    /// Makes <paramref name="path"/> the current path, selects the first entry, and lists it.
    /// </summary>
    /// <param name="path">
    /// The path inside the volume to show. It is used as given, without checks.
    /// </param>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="RefreshAsync"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the new listing is loaded.
    /// </returns>
    public Task NavigateAsync(string path, CancellationToken cancellationToken = default)
    {
        _currentPath = path;
        _selectedIndex = 0;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Tells whether there is a parent folder to go up to.
    /// </summary>
    /// <returns>
    /// False when the current path is <c>/</c> or empty; otherwise true.
    /// </returns>
    public bool CanNavigateUp()
    {
        return _currentPath != "/" && !string.IsNullOrEmpty(_currentPath);
    }

    /// <summary>
    /// Goes up one folder level by cutting the current path at its last <c>/</c>, then lists the new path.
    /// </summary>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="RefreshAsync"/>.
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
            throw new InvalidOperationException("Already at root");

        // Go up one directory level
        var lastSlash = _currentPath.TrimEnd('/').LastIndexOf('/');
        _currentPath = lastSlash <= 0 ? "/" : _currentPath.Substring(0, lastSlash);

        return RefreshAsync(cancellationToken);
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
    /// Writes a new file into the current path through the driver, then refreshes the listing.
    /// </summary>
    /// <remarks>
    /// The whole content is read into memory first. When <paramref name="properties"/> is given and
    /// the driver supports extended attributes, the file is written together with those properties;
    /// otherwise only the bytes are written.
    /// </remarks>
    /// <param name="name">
    /// Name of the new file in the current path.
    /// </param>
    /// <param name="content">
    /// The file content, read from its current position to the end.
    /// </param>
    /// <param name="properties">
    /// Optional properties to store with the file. Entries with a null value are dropped.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel reading the content and the refresh.
    /// </param>
    /// <returns>
    /// An entry for the new file, built from the metadata the driver reports for it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the driver has no metadata for the file after writing it.
    /// </exception>
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

    /// <summary>
    /// Creates a directory in the current path through the driver, then refreshes the listing.
    /// </summary>
    /// <remarks>
    /// The returned entry is found by name, ignoring case, in the new listing; failing that, from the
    /// driver's metadata for the new path; failing that, the first entry in the listing is returned.
    /// </remarks>
    /// <param name="name">
    /// Name of the new directory.
    /// </param>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="RefreshAsync"/>.
    /// </param>
    /// <returns>
    /// An entry for the new directory, or the first listed entry if the new one cannot be found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the listing is empty after the directory was created.
    /// </exception>
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

    /// <summary>
    /// Deletes a file through the driver. The listing is not refreshed.
    /// </summary>
    /// <param name="entry">
    /// The file to delete.
    /// </param>
    /// <param name="cancellationToken">
    /// Not used.
    /// </param>
    /// <returns>
    /// A task that is already complete.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="entry"/> is a directory.
    /// </exception>
    public Task DeleteAsync(IFileSystemEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.IsDirectory)
            throw new NotSupportedException("Directory deletion not supported");

        _driver.DeleteFile(entry.FullPath);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Renames an entry through the driver, then refreshes the listing.
    /// </summary>
    /// <param name="entry">
    /// The entry to rename.
    /// </param>
    /// <param name="newName">
    /// The new name, passed to the driver as given.
    /// </param>
    /// <param name="cancellationToken">
    /// Passed on to <see cref="RefreshAsync"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the listing is refreshed.
    /// </returns>
    public Task RenameAsync(
        IFileSystemEntry entry,
        string newName,
        CancellationToken cancellationToken = default)
    {
        // Delegates to the driver's rename (drivers without rename support throw NotSupported).
        _driver.RenameFile(entry.FullPath, newName);
        return RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Disposes the driver, which closes the mounted volume.
    /// </summary>
    public void Dispose()
    {
        _driver?.Dispose();
    }

    /// <summary>
    /// Adapts an IPropertyBag (whose values may be null) to the non-nullable
    /// string-to-object dictionary shape that IExtendedAttributeSupport.WriteFileWithProperties
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
