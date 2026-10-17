//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// NDFS (Nord Disk File System) specific properties
/// </summary>
public static class NdfsProperties
{
    public const string UserId = "NDFS.UserId";                    // ushort
    public const string ObjectType = "NDFS.ObjectType";            // byte
    public const string AccessMode = "NDFS.AccessMode";            // byte
    public const string ParityBit = "NDFS.ParityBit";              // bool
    public const string FirstPageNumber = "NDFS.FirstPageNumber";  // uint
    public const string PageCount = "NDFS.PageCount";              // uint
    public const string UserName = "NDFS.UserName";                // string
    public const string Description = "NDFS.Description";          // string
    public const string EffectiveDate = "NDFS.EffectiveDate";      // DateTime?
}
