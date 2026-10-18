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
    /// <summary>
    /// Key for the NDFS user id. The value is a ushort.
    /// </summary>
    public const string UserId = "NDFS.UserId";                    // ushort

    /// <summary>
    /// Key for the NDFS object type. The value is a byte.
    /// </summary>
    public const string ObjectType = "NDFS.ObjectType";            // byte

    /// <summary>
    /// Key for the NDFS access mode. The value is a byte.
    /// </summary>
    public const string AccessMode = "NDFS.AccessMode";            // byte

    /// <summary>
    /// Key for the NDFS parity bit. The value is a bool.
    /// </summary>
    public const string ParityBit = "NDFS.ParityBit";              // bool

    /// <summary>
    /// Key for the number of the file's first page. The value is a uint.
    /// </summary>
    public const string FirstPageNumber = "NDFS.FirstPageNumber";  // uint

    /// <summary>
    /// Key for the number of pages in the file. The value is a uint.
    /// </summary>
    public const string PageCount = "NDFS.PageCount";              // uint

    /// <summary>
    /// Key for the NDFS user name. The value is a string.
    /// </summary>
    public const string UserName = "NDFS.UserName";                // string

    /// <summary>
    /// Key for a text description. The value is a string.
    /// </summary>
    public const string Description = "NDFS.Description";          // string

    /// <summary>
    /// Key for the NDFS effective date. The value is a nullable DateTime.
    /// </summary>
    public const string EffectiveDate = "NDFS.EffectiveDate";      // DateTime?
}
