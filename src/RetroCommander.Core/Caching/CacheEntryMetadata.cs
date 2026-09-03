//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Caching;

/// <summary>
/// Metadata for a single cache entry
/// </summary>
public class CacheEntryMetadata
{
    public string Urn { get; set; } = string.Empty;
    public string CacheFilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime RemoteModifiedDate { get; set; }
    public string Md5Hash { get; set; } = string.Empty;
    public DateTime CachedDate { get; set; }
    public DateTime LastAccessedDate { get; set; }
    public int AccessCount { get; set; }
}

/// <summary>
/// Index of all cached files
/// </summary>
public class CacheIndex
{
    public List<CacheEntryMetadata> Entries { get; set; } = new();
    public long TotalSizeBytes { get; set; }
    public DateTime LastUpdated { get; set; }
}
