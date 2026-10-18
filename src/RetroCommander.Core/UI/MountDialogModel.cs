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
    /// <summary>
    /// Gets or sets the file system type name, for example <c>NDFS</c> or <c>D64</c>.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name shown for this option in the dialog.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short text that explains the option.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this type is mounted from a local path.
    /// </summary>
    public bool RequiresPath { get; set; }

    /// <summary>
    /// Gets or sets whether this type needs connection details, such as a host or container.
    /// </summary>
    public bool RequiresConnection { get; set; }

    /// <summary>
    /// Gets or sets the URN pattern with placeholders in curly brackets, for example <c>ndfs://{path}</c>. See <see cref="BuildUrn"/>.
    /// </summary>
    public string UrnTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon text shown for this option, usually an emoji.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the URN of the volume.
    /// </summary>
    public string Urn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name shown for the volume.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file system type name.
    /// </summary>
    public string FileSystemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the volume was last mounted.
    /// </summary>
    public DateTime LastAccessed { get; set; }

    /// <summary>
    /// Gets or sets the icon text shown for the entry, usually an emoji.
    /// </summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Bookmark entry
/// </summary>
public class Bookmark
{
    /// <summary>
    /// Gets or sets the bookmark ID, a new GUID string by default. Bookmarks are removed by this ID.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the name shown for the bookmark.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URN the bookmark points to.
    /// </summary>
    public string Urn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file system type name.
    /// </summary>
    public string FileSystemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional free-text description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the icon text shown for the bookmark, usually an emoji.
    /// </summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Result of mount operation
/// </summary>
public class MountDialogResult
{
    /// <summary>
    /// Gets or sets whether the dialog ended with a volume to mount. False for both cancel and failure.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the URN to mount, or null when not successful.
    /// </summary>
    public string? Urn { get; set; }

    /// <summary>
    /// Gets or sets the name to show for the volume, or null when not successful.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets why the dialog failed, or null on success or cancel.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="urn">
    /// The URN to mount.
    /// </param>
    /// <param name="displayName">
    /// The name to show for the volume.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> set to true.
    /// </returns>
    public static MountDialogResult SuccessResult(string urn, string displayName) =>
        new() { Success = true, Urn = urn, DisplayName = displayName };

    /// <summary>
    /// Creates a result for a dialog the user closed without mounting.
    /// </summary>
    /// <returns>
    /// A result with <see cref="Success"/> false and no error message.
    /// </returns>
    public static MountDialogResult Cancelled() =>
        new() { Success = false };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">
    /// Why the dialog failed.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> false and <see cref="ErrorMessage"/> set to <paramref name="error"/>.
    /// </returns>
    public static MountDialogResult Failure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
