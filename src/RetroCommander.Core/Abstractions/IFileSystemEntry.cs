//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Abstractions;

/// <summary>
/// Represents a file or directory entry in any filesystem
/// </summary>
public interface IFileSystemEntry
{
    /// <summary>
    /// File or directory name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Full path within the filesystem
    /// </summary>
    string FullPath { get; }

    /// <summary>
    /// Size in bytes (0 for directories)
    /// </summary>
    long Size { get; }

    /// <summary>
    /// True if this is a directory
    /// </summary>
    bool IsDirectory { get; }

    /// <summary>
    /// Creation date (if supported by filesystem)
    /// </summary>
    DateTime? CreatedDate { get; }

    /// <summary>
    /// Last modified date (if supported by filesystem)
    /// </summary>
    DateTime? ModifiedDate { get; }

    /// <summary>
    /// Last accessed date (if supported by filesystem)
    /// </summary>
    DateTime? AccessedDate { get; }

    /// <summary>
    /// Property bag containing all filesystem-specific metadata
    /// </summary>
    IPropertyBag Properties { get; }

    /// <summary>
    /// Open file for reading
    /// </summary>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write content to file (if filesystem supports writing)
    /// </summary>
    Task WriteAsync(Stream content, CancellationToken cancellationToken = default);
}
