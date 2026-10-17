//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Windows filesystem specific properties
/// </summary>
public static class WindowsProperties
{
    public const string Attributes = "Windows.Attributes";           // FileAttributes
    public const string AlternateDataStreams = "Windows.ADS";        // List<ADSInfo>
    public const string ShortFileName = "Windows.ShortFileName";     // string
    public const string Owner = "Windows.Owner";                     // string
    public const string SecurityDescriptor = "Windows.Security";     // byte[]
}
