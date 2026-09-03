//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Commodore (CBM DOS) specific properties
/// </summary>
public static class CbmProperties
{
    public const string FileType = "CBM.FileType";           // string (PRG, SEQ, USR, REL)
    public const string LoadAddress = "CBM.LoadAddress";     // ushort?
    public const string TrackSector = "CBM.TrackSector";     // (byte Track, byte Sector)?
    public const string RecordLength = "CBM.RecordLength";   // byte? (for REL files)
    public const string Closed = "CBM.Closed";               // bool
    public const string Locked = "CBM.Locked";               // bool
}
