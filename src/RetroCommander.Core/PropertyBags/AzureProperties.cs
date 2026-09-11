//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Azure Blob Storage specific properties
/// </summary>
public static class AzureProperties
{
    public const string ContentType = "Azure.ContentType";           // string
    public const string ContentEncoding = "Azure.ContentEncoding";   // string
    public const string ETag = "Azure.ETag";                         // string
    public const string BlobType = "Azure.BlobType";                 // string
    public const string Tier = "Azure.Tier";                         // string
    public const string Metadata = "Azure.Metadata";                 // Dictionary<string, string>
    public const string LeaseState = "Azure.LeaseState";             // string
}
