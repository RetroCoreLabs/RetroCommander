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
    /// <summary>
    /// Key for the Windows file attributes. The value is a <see cref="System.IO.FileAttributes"/>.
    /// </summary>
    public const string Attributes = "Windows.Attributes";           // FileAttributes

    /// <summary>
    /// Key for the file's alternate data streams.
    /// </summary>
    /// <remarks>
    /// The comment on the line below names the value as a list of ADSInfo, but no ADSInfo type exists in the code
    /// and nothing sets this key at present.
    /// </remarks>
    public const string AlternateDataStreams = "Windows.ADS";        // List<ADSInfo>

    /// <summary>
    /// Key for the short (8.3) file name. The value is a string.
    /// </summary>
    public const string ShortFileName = "Windows.ShortFileName";     // string

    /// <summary>
    /// Key for the owner of the file. The value is a string.
    /// </summary>
    public const string Owner = "Windows.Owner";                     // string

    /// <summary>
    /// Key for the file's security descriptor. The value is a byte array.
    /// </summary>
    public const string SecurityDescriptor = "Windows.Security";     // byte[]
}
