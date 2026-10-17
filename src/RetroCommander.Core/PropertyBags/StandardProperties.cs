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
    public const string FileName = "FileName";
    public const string FileSize = "FileSize";
    public const string IsDirectory = "IsDirectory";
    public const string CreatedDate = "CreatedDate";
    public const string ModifiedDate = "ModifiedDate";
    public const string AccessedDate = "AccessedDate";
    public const string IsReadOnly = "IsReadOnly";
    public const string IsHidden = "IsHidden";
}
