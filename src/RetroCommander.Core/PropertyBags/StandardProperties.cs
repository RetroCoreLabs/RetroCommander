//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Standard properties supported by all filesystems
/// </summary>
public static class StandardProperties
{
    /// <summary>
    /// Key for the file name. The value is a string.
    /// </summary>
    public const string FileName = "FileName";

    /// <summary>
    /// Key for the file size in bytes. The value is a long.
    /// </summary>
    public const string FileSize = "FileSize";

    /// <summary>
    /// Key for whether the entry is a directory. The value is a bool.
    /// </summary>
    public const string IsDirectory = "IsDirectory";

    /// <summary>
    /// Key for when the entry was created. The value is a nullable DateTime.
    /// </summary>
    public const string CreatedDate = "CreatedDate";

    /// <summary>
    /// Key for when the entry was last changed. The value is a nullable DateTime.
    /// </summary>
    public const string ModifiedDate = "ModifiedDate";

    /// <summary>
    /// Key for when the entry was last read. The value is a nullable DateTime.
    /// </summary>
    public const string AccessedDate = "AccessedDate";

    /// <summary>
    /// Key for whether the entry is read-only. The value is a bool.
    /// </summary>
    public const string IsReadOnly = "IsReadOnly";

    /// <summary>
    /// Key for whether the entry is hidden. The value is a bool.
    /// </summary>
    public const string IsHidden = "IsHidden";
}
