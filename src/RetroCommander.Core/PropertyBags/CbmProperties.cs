//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Commodore (CBM DOS) specific properties
/// </summary>
public static class CbmProperties
{
    /// <summary>
    /// Key for the CBM file type. The value is a string such as PRG, SEQ, USR or REL.
    /// </summary>
    public const string FileType = "CBM.FileType";           // string (PRG, SEQ, USR, REL)

    /// <summary>
    /// Key for the address the file loads to. The value is a nullable ushort.
    /// </summary>
    public const string LoadAddress = "CBM.LoadAddress";     // ushort?

    /// <summary>
    /// Key for the track and sector of the file. The value is a nullable tuple of byte Track and byte Sector.
    /// </summary>
    public const string TrackSector = "CBM.TrackSector";     // (byte Track, byte Sector)?

    /// <summary>
    /// Key for the record length of a REL file. The value is a nullable byte.
    /// </summary>
    public const string RecordLength = "CBM.RecordLength";   // byte? (for REL files)

    /// <summary>
    /// Key for the CBM "closed" flag of the file. The value is a bool.
    /// </summary>
    public const string Closed = "CBM.Closed";               // bool

    /// <summary>
    /// Key for the CBM "locked" flag of the file. The value is a bool.
    /// </summary>
    public const string Locked = "CBM.Locked";               // bool
}
