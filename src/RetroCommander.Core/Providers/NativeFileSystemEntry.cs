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
/// File system entry for native Windows filesystem
/// </summary>
public class NativeFileSystemEntry : IFileSystemEntry
{
    private readonly FileInfo? _fileInfo;
    private readonly DirectoryInfo? _dirInfo;
    private readonly IPropertyBag _properties;

    /// <summary>
    /// Wraps a host file and builds its property bag right away.
    /// </summary>
    /// <remarks>
    /// The property bag holds the name, size, dates in UTC, read-only and hidden flags, and the Windows attributes.
    /// </remarks>
    /// <param name="fileInfo">
    /// The host file.
    /// </param>
    public NativeFileSystemEntry(FileInfo fileInfo)
    {
        _fileInfo = fileInfo;
        _properties = ExtractPropertiesFromFile(fileInfo);
    }

    /// <summary>
    /// Wraps a host directory and builds its property bag right away.
    /// </summary>
    /// <remarks>
    /// The property bag holds the name, a size of 0, dates in UTC, the hidden flag, and the Windows attributes.
    /// </remarks>
    /// <param name="dirInfo">
    /// The host directory.
    /// </param>
    public NativeFileSystemEntry(DirectoryInfo dirInfo)
    {
        _dirInfo = dirInfo;
        _properties = ExtractPropertiesFromDirectory(dirInfo);
    }

    /// <inheritdoc/>
    public string Name => IsDirectory ? _dirInfo!.Name : _fileInfo!.Name;

    /// <summary>
    /// Gets the full host path of the file or directory.
    /// </summary>
    public string FullPath => IsDirectory ? _dirInfo!.FullName : _fileInfo!.FullName;

    /// <inheritdoc/>
    public long Size => IsDirectory ? 0 : _fileInfo!.Length;

    /// <summary>
    /// Gets whether this entry was created from a directory.
    /// </summary>
    public bool IsDirectory => _dirInfo != null;

    /// <summary>
    /// Gets the creation time in UTC.
    /// </summary>
    public DateTime? CreatedDate => IsDirectory ? _dirInfo!.CreationTimeUtc : _fileInfo!.CreationTimeUtc;

    /// <summary>
    /// Gets the last write time in UTC.
    /// </summary>
    public DateTime? ModifiedDate => IsDirectory ? _dirInfo!.LastWriteTimeUtc : _fileInfo!.LastWriteTimeUtc;

    /// <summary>
    /// Gets the last access time in UTC.
    /// </summary>
    public DateTime? AccessedDate => IsDirectory ? _dirInfo!.LastAccessTimeUtc : _fileInfo!.LastAccessTimeUtc;

    /// <inheritdoc/>
    public IPropertyBag Properties => _properties;

    /// <summary>
    /// Opens the host file for reading.
    /// </summary>
    /// <param name="cancellationToken">
    /// Not used.
    /// </param>
    /// <returns>
    /// A read-only file stream; the caller disposes it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entry is a directory.
    /// </exception>
    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (IsDirectory)
            throw new InvalidOperationException("Cannot open a directory for reading");

        return Task.FromResult<Stream>(File.OpenRead(_fileInfo!.FullName));
    }

    /// <summary>
    /// Replaces the host file's content with <paramref name="content"/>, creating the file if needed.
    /// </summary>
    /// <param name="content">
    /// The new content, read from its current position to the end.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the copy.
    /// </param>
    /// <returns>
    /// A task that completes when the content is written.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entry is a directory.
    /// </exception>
    public async Task WriteAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (IsDirectory)
            throw new InvalidOperationException("Cannot write to a directory");

        using var fileStream = File.Create(_fileInfo!.FullName);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    private IPropertyBag ExtractPropertiesFromFile(FileInfo fileInfo)
    {
        var bag = new PropertyBag();

        bag.SetProperty(StandardProperties.FileName, fileInfo.Name);
        bag.SetProperty(StandardProperties.FileSize, fileInfo.Length);
        bag.SetProperty(StandardProperties.IsDirectory, false);
        bag.SetProperty(StandardProperties.CreatedDate, (DateTime?)fileInfo.CreationTimeUtc);
        bag.SetProperty(StandardProperties.ModifiedDate, (DateTime?)fileInfo.LastWriteTimeUtc);
        bag.SetProperty(StandardProperties.AccessedDate, (DateTime?)fileInfo.LastAccessTimeUtc);
        bag.SetProperty(StandardProperties.IsReadOnly, fileInfo.IsReadOnly);
        bag.SetProperty(StandardProperties.IsHidden, (fileInfo.Attributes & FileAttributes.Hidden) != 0);

        // Windows-specific properties
        bag.SetProperty(WindowsProperties.Attributes, fileInfo.Attributes);

        return bag;
    }

    private IPropertyBag ExtractPropertiesFromDirectory(DirectoryInfo dirInfo)
    {
        var bag = new PropertyBag();

        bag.SetProperty(StandardProperties.FileName, dirInfo.Name);
        bag.SetProperty(StandardProperties.FileSize, 0L);
        bag.SetProperty(StandardProperties.IsDirectory, true);
        bag.SetProperty(StandardProperties.CreatedDate, (DateTime?)dirInfo.CreationTimeUtc);
        bag.SetProperty(StandardProperties.ModifiedDate, (DateTime?)dirInfo.LastWriteTimeUtc);
        bag.SetProperty(StandardProperties.AccessedDate, (DateTime?)dirInfo.LastAccessTimeUtc);
        bag.SetProperty(StandardProperties.IsHidden, (dirInfo.Attributes & FileAttributes.Hidden) != 0);

        // Windows-specific properties
        bag.SetProperty(WindowsProperties.Attributes, dirInfo.Attributes);

        return bag;
    }
}
