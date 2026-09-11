//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
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

    public DriverAdapterFileSystemEntry(IFileSystemDriver driver, FileMetadata metadata)
    {
        _driver = driver;
        _metadata = metadata;
        _properties = ExtractProperties();
    }

    public string Name => _metadata.Name;

    public string FullPath => _metadata.Path;

    public long Size => _metadata.Size;

    public bool IsDirectory => _metadata.IsDirectory;

    public DateTime? CreatedDate => _metadata.CreatedTime;

    public DateTime? ModifiedDate => _metadata.ModifiedTime;

    public DateTime? AccessedDate => _metadata.AccessedTime;

    public IPropertyBag Properties => _properties;

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (IsDirectory)
            throw new InvalidOperationException("Cannot open a directory for reading");

        var data = _driver.ReadFile(_metadata.Path);
        return Task.FromResult<Stream>(new MemoryStream(data));
    }

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
