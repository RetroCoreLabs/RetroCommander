//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.UI;

/// <summary>
/// Model for mount dialog - UI agnostic
/// </summary>
public class MountDialogModel
{
    /// <summary>
    /// Available filesystem types to mount
    /// </summary>
    public List<FileSystemTypeOption> AvailableTypes { get; set; } = new()
    {
        new FileSystemTypeOption
        {
            Type = "Windows",
            DisplayName = "Windows Folder",
            Description = "Mount a native Windows directory",
            RequiresPath = true,
            UrnTemplate = "file:///{path}",
            Icon = "📁"
        },
        new FileSystemTypeOption
        {
            Type = "NDFS",
            DisplayName = "NDFS Disk Image",
            Description = "Nord Disk File System image (.img, .image)",
            RequiresPath = true,
            UrnTemplate = "ndfs://{path}",
            Icon = "💾"
        },
        new FileSystemTypeOption
        {
            Type = "Azure",
            DisplayName = "Azure Blob Storage",
            Description = "Azure Blob Storage container",
            RequiresConnection = true,
            UrnTemplate = "azure://{container}",
            Icon = "☁️"
        },
        new FileSystemTypeOption
        {
            Type = "TNFS",
            DisplayName = "TNFS Server",
            Description = "Trivial Network File System server",
            RequiresConnection = true,
            UrnTemplate = "tnfs://{host}:{port}/{path}",
            Icon = "🌐"
        },
        new FileSystemTypeOption
        {
            Type = "D64",
            DisplayName = "Commodore D64 Disk",
            Description = "Commodore 1541 disk image (.d64)",
            RequiresPath = true,
            UrnTemplate = "d64://{path}",
            Icon = "💾"
        },
        new FileSystemTypeOption
        {
            Type = "D71",
            DisplayName = "Commodore D71 Disk",
            Description = "Commodore 1571 disk image (.d71)",
            RequiresPath = true,
            UrnTemplate = "d71://{path}",
            Icon = "💾"
        },
        new FileSystemTypeOption
        {
            Type = "D81",
            DisplayName = "Commodore D81 Disk",
            Description = "Commodore 1581 disk image (.d81)",
            RequiresPath = true,
            UrnTemplate = "d81://{path}",
            Icon = "💾"
        },
        new FileSystemTypeOption
        {
            Type = "BSD",
            DisplayName = "BSD Disk Image",
            Description = "BSD 2.11 V7 Unix filesystem (.dsk, .img, .bsd)",
            RequiresPath = true,
            UrnTemplate = "bsd://{path}",
            Icon = "🐡"
        },
        new FileSystemTypeOption
        {
            Type = "ZIP",
            DisplayName = "ZIP Archive",
            Description = "ZIP compressed archive (.zip)",
            RequiresPath = true,
            UrnTemplate = "zip://{path}",
            Icon = "📦"
        }
    };

    /// <summary>
    /// Recently used URNs (for quick access)
    /// </summary>
    public List<RecentMount> RecentMounts { get; set; } = new();

    /// <summary>
    /// Bookmarked locations
    /// </summary>
    public List<Bookmark> Bookmarks { get; set; } = new();
}

/// <summary>
/// Describes a filesystem type option
/// </summary>
public class FileSystemTypeOption
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresPath { get; set; }
    public bool RequiresConnection { get; set; }
    public string UrnTemplate { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Build URN from parameters
    /// </summary>
    public string BuildUrn(Dictionary<string, string> parameters)
    {
        var urn = UrnTemplate;
        foreach (var param in parameters)
        {
            urn = urn.Replace($"{{{param.Key}}}", param.Value);
        }
        return urn;
    }
}

/// <summary>
/// Recent mount entry
/// </summary>
public class RecentMount
{
    public string Urn { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileSystemType { get; set; } = string.Empty;
    public DateTime LastAccessed { get; set; }
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Bookmark entry
/// </summary>
public class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Urn { get; set; } = string.Empty;
    public string FileSystemType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Result of mount operation
/// </summary>
public class MountDialogResult
{
    public bool Success { get; set; }
    public string? Urn { get; set; }
    public string? DisplayName { get; set; }
    public string? ErrorMessage { get; set; }

    public static MountDialogResult SuccessResult(string urn, string displayName) =>
        new() { Success = true, Urn = urn, DisplayName = displayName };

    public static MountDialogResult Cancelled() =>
        new() { Success = false };

    public static MountDialogResult Failure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
