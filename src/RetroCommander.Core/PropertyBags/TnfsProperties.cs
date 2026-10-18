//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// TNFS (Trivial Network File System) specific properties
/// </summary>
public static class TnfsProperties
{
    /// <summary>
    /// Key for the Unix-style permission bits. The value is a uint.
    /// </summary>
    public const string Permissions = "TNFS.Permissions";     // uint (Unix-style)

    /// <summary>
    /// Key for the owner of the file. The value is a string.
    /// </summary>
    public const string Owner = "TNFS.Owner";                 // string

    /// <summary>
    /// Key for the group of the file. The value is a string.
    /// </summary>
    public const string Group = "TNFS.Group";                 // string

    /// <summary>
    /// Key for the path a symbolic link points to. The value is a string.
    /// </summary>
    public const string LinkTarget = "TNFS.LinkTarget";       // string (for symlinks)

    /// <summary>
    /// Key for the device type. The value is a byte.
    /// </summary>
    public const string DeviceType = "TNFS.DeviceType";       // byte
}
