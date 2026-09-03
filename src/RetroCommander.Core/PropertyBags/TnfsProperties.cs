//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// TNFS (Trivial Network File System) specific properties
/// </summary>
public static class TnfsProperties
{
    public const string Permissions = "TNFS.Permissions";     // uint (Unix-style)
    public const string Owner = "TNFS.Owner";                 // string
    public const string Group = "TNFS.Group";                 // string
    public const string LinkTarget = "TNFS.LinkTarget";       // string (for symlinks)
    public const string DeviceType = "TNFS.DeviceType";       // byte
}
