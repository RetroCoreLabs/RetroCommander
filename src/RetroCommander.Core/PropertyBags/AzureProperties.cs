//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Azure Blob Storage specific properties
/// </summary>
public static class AzureProperties
{
    /// <summary>
    /// Key for the blob's content type. The value is a string.
    /// </summary>
    public const string ContentType = "Azure.ContentType";           // string

    /// <summary>
    /// Key for the blob's content encoding. The value is a string.
    /// </summary>
    public const string ContentEncoding = "Azure.ContentEncoding";   // string

    /// <summary>
    /// Key for the blob's ETag. The value is a string.
    /// </summary>
    public const string ETag = "Azure.ETag";                         // string

    /// <summary>
    /// Key for the kind of blob. The value is a string.
    /// </summary>
    public const string BlobType = "Azure.BlobType";                 // string

    /// <summary>
    /// Key for the blob's access tier. The value is a string.
    /// </summary>
    public const string Tier = "Azure.Tier";                         // string

    /// <summary>
    /// Key for the blob's name and value metadata pairs. The value is a Dictionary of string to string.
    /// </summary>
    public const string Metadata = "Azure.Metadata";                 // Dictionary<string, string>

    /// <summary>
    /// Key for the blob's lease state. The value is a string.
    /// </summary>
    public const string LeaseState = "Azure.LeaseState";             // string
}
