//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Caching;

/// <summary>
/// Configuration for file caching
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Whether caching is switched on. Defaults to true.
    /// </summary>
    /// <remarks>
    /// <see cref="FileSystemCacheManager"/> does not read this value.
    /// </remarks>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Folder that holds the cached files and the cache index. Defaults to
    /// RetroCommander\Cache under the user's local application data folder.
    /// </summary>
    public string CacheDirectory { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroCommander",
            "Cache");

    /// <summary>
    /// Largest total size of the cache in gigabytes (1024 x 1024 x 1024 bytes). Defaults to 10.
    /// </summary>
    /// <remarks>
    /// When a new file would push the cache past this size, the least recently used files are
    /// removed first.
    /// </remarks>
    public long MaxCacheSizeGB { get; set; } = 10;

    /// <summary>
    /// Maximum age of a cached file in hours. Defaults to 24.
    /// </summary>
    /// <remarks>
    /// <see cref="FileSystemCacheManager"/> does not read this value.
    /// </remarks>
    public int MaxAgeHours { get; set; } = 24;

    /// <summary>
    /// Whether read-only files may be cached. Defaults to true.
    /// </summary>
    /// <remarks>
    /// <see cref="FileSystemCacheManager"/> does not read this value.
    /// </remarks>
    public bool CacheReadOnlyFiles { get; set; } = true;

    // Minimum file size to cache (avoid caching tiny files)
    /// <summary>
    /// Smallest file size, in bytes, worth caching. Defaults to 1024.
    /// </summary>
    /// <remarks>
    /// <see cref="FileSystemCacheManager"/> does not read this value.
    /// </remarks>
    public long MinFileSizeToCache { get; set; } = 1024; // 1 KB
}
