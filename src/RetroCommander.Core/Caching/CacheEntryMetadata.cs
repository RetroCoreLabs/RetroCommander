//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Caching;

/// <summary>
/// Metadata for a single cache entry
/// </summary>
public class CacheEntryMetadata
{
    /// <summary>
    /// URN of the remote file this entry holds a copy of. Used as the lookup key.
    /// </summary>
    public string Urn { get; set; } = string.Empty;

    /// <summary>
    /// Full path of the local file that holds the cached copy.
    /// </summary>
    public string CacheFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Size of the remote file in bytes, as reported when it was cached.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Last-modified time of the remote file, as reported when it was cached.
    /// </summary>
    public DateTime RemoteModifiedDate { get; set; }

    /// <summary>
    /// MD5 hash of the cached content, as upper-case hex digits with no separators.
    /// </summary>
    public string Md5Hash { get; set; } = string.Empty;

    /// <summary>
    /// When the file was put in the cache (UTC).
    /// </summary>
    public DateTime CachedDate { get; set; }

    /// <summary>
    /// When the cached copy was last used (UTC). The entry with the oldest value is removed
    /// first when space is needed.
    /// </summary>
    public DateTime LastAccessedDate { get; set; }

    /// <summary>
    /// Number of times the cached copy has been used, counting the fetch that stored it.
    /// </summary>
    public int AccessCount { get; set; }
}

/// <summary>
/// Index of all cached files
/// </summary>
public class CacheIndex
{
    /// <summary>
    /// One entry per cached file.
    /// </summary>
    public List<CacheEntryMetadata> Entries { get; set; } = new();

    /// <summary>
    /// Sum of <see cref="CacheEntryMetadata.FileSizeBytes"/> over all entries.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// When the index was last saved (UTC).
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
