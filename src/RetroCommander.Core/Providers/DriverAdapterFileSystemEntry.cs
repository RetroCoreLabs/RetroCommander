//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.PropertyBags;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Providers;

/// <summary>
/// Adapter that wraps RetroFS DirectoryEntry/FileMetadata into IFileSystemEntry
/// </summary>
public class DriverAdapterFileSystemEntry : IFileSystemEntry
{
    private readonly IFileSystemDriver _driver;
    private readonly FileMetadata _metadata;
    private readonly IPropertyBag _properties;

    /// <summary>
    /// Wraps one file or directory reported by a file system driver, and builds its property bag right away.
    /// </summary>
    /// <remarks>
    /// The property bag holds the standard name, size, directory flag and dates, the driver's own
    /// metadata properties, and for files the extended properties of a driver that supports them.
    /// </remarks>
    /// <param name="driver">
    /// The driver used to read and write the file's content.
    /// </param>
    /// <param name="metadata">
    /// The driver's description of the entry.
    /// </param>
    public DriverAdapterFileSystemEntry(IFileSystemDriver driver, FileMetadata metadata)
    {
        _driver = driver;
        _metadata = metadata;
        _properties = ExtractProperties();
    }

    /// <inheritdoc/>
    public string Name => _metadata.Name;

    /// <inheritdoc/>
    public string FullPath => _metadata.Path;

    /// <inheritdoc/>
    public long Size => _metadata.Size;

    /// <inheritdoc/>
    public bool IsDirectory => _metadata.IsDirectory;

    /// <inheritdoc/>
    public DateTime? CreatedDate => _metadata.CreatedTime;

    /// <inheritdoc/>
    public DateTime? ModifiedDate => _metadata.ModifiedTime;

    /// <inheritdoc/>
    public DateTime? AccessedDate => _metadata.AccessedTime;

    /// <inheritdoc/>
    public IPropertyBag Properties => _properties;

    /// <summary>
    /// Reads the whole file through the driver and returns it as an in-memory stream.
    /// </summary>
    /// <param name="cancellationToken">
    /// Not used; the read runs to the end.
    /// </param>
    /// <returns>
    /// A memory stream holding the file's content.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entry is a directory.
    /// </exception>
    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (IsDirectory)
            throw new InvalidOperationException("Cannot open a directory for reading");

        var data = _driver.ReadFile(_metadata.Path);
        return Task.FromResult<Stream>(new MemoryStream(data));
    }

    /// <summary>
    /// Reads all of <paramref name="content"/> into memory and writes it to the file through the driver.
    /// </summary>
    /// <param name="content">
    /// The new file content, read from its current position to the end.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel reading <paramref name="content"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the driver has written the file.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entry is a directory.
    /// </exception>
    public async Task WriteAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (IsDirectory)
            throw new InvalidOperationException("Cannot write to a directory");

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var data = ms.ToArray();

        _driver.WriteFile(_metadata.Path, data);
    }

    private IPropertyBag ExtractProperties()
    {
        var bag = new PropertyBag();

        // Standard properties
        bag.SetProperty(StandardProperties.FileName, _metadata.Name);
        bag.SetProperty(StandardProperties.FileSize, _metadata.Size);
        bag.SetProperty(StandardProperties.IsDirectory, _metadata.IsDirectory);
        bag.SetProperty(StandardProperties.CreatedDate, _metadata.CreatedTime);
        bag.SetProperty(StandardProperties.ModifiedDate, _metadata.ModifiedTime);
        bag.SetProperty(StandardProperties.AccessedDate, _metadata.AccessedTime);

        // Copy filesystem-specific properties from metadata property bag
        foreach (var prop in _metadata.Properties)
        {
            bag.SetProperty(prop.Key, prop.Value);
        }

        // Drivers with richer, filesystem-native metadata (e.g. NDFS owner/access-bits/page
        // layout) expose it via IExtendedAttributeSupport.GetFileProperties rather than through
        // the plain directory-listing metadata above. Pull it in here so entries backed by such a
        // driver actually carry their real metadata — this is what makes DirectPropertyMapper
        // (used for same-type copies, e.g. NDFS->NDFS) preserve anything real, and what an
        // NDFS-to-Windows export sidecar (see SmartFileOperations.CopyAsync) has real data to write.
        if (!_metadata.IsDirectory && _driver is IExtendedAttributeSupport xa)
        {
            try
            {
                if (xa.GetCapabilities().SupportsExtendedAttributes)
                {
                    var extended = xa.GetFileProperties(_metadata.Path);
                    if (extended != null)
                    {
                        foreach (var prop in extended)
                        {
                            bag.SetProperty(prop.Key, prop.Value);
                        }
                    }
                }
            }
            catch
            {
                // Best-effort: a metadata read failure must not break directory listing.
            }
        }

        return bag;
    }
}
