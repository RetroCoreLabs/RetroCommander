//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
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

    public NativeFileSystemEntry(FileInfo fileInfo)
    {
        _fileInfo = fileInfo;
        _properties = ExtractPropertiesFromFile(fileInfo);
    }

    public NativeFileSystemEntry(DirectoryInfo dirInfo)
    {
        _dirInfo = dirInfo;
        _properties = ExtractPropertiesFromDirectory(dirInfo);
    }

    public string Name => IsDirectory ? _dirInfo!.Name : _fileInfo!.Name;

    public string FullPath => IsDirectory ? _dirInfo!.FullName : _fileInfo!.FullName;

    public long Size => IsDirectory ? 0 : _fileInfo!.Length;

    public bool IsDirectory => _dirInfo != null;

    public DateTime? CreatedDate => IsDirectory ? _dirInfo!.CreationTimeUtc : _fileInfo!.CreationTimeUtc;

    public DateTime? ModifiedDate => IsDirectory ? _dirInfo!.LastWriteTimeUtc : _fileInfo!.LastWriteTimeUtc;

    public DateTime? AccessedDate => IsDirectory ? _dirInfo!.LastAccessTimeUtc : _fileInfo!.LastAccessTimeUtc;

    public IPropertyBag Properties => _properties;

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (IsDirectory)
            throw new InvalidOperationException("Cannot open a directory for reading");

        return Task.FromResult<Stream>(File.OpenRead(_fileInfo!.FullName));
    }

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
