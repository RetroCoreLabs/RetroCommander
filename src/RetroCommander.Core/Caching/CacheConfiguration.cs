//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Caching;

/// <summary>
/// Configuration for file caching
/// </summary>
public class CacheConfiguration
{
    public bool EnableCaching { get; set; } = true;

    public string CacheDirectory { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroCommander",
            "Cache");

    public long MaxCacheSizeGB { get; set; } = 10;
    public int MaxAgeHours { get; set; } = 24;
    public bool CacheReadOnlyFiles { get; set; } = true;

    // Minimum file size to cache (avoid caching tiny files)
    public long MinFileSizeToCache { get; set; } = 1024; // 1 KB
}
