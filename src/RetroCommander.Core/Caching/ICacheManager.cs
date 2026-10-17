//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Caching;

/// <summary>
/// Metadata about a remote file for cache validation
/// </summary>
public class RemoteFileMetadata
{
    public long FileSizeBytes { get; set; }
    public DateTime ModifiedDate { get; set; }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public long TotalSizeBytes { get; set; }
    public long MaxSizeBytes { get; set; }
    public int EntryCount { get; set; }
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public double HitRate => (HitCount + MissCount) > 0 ? HitCount / (double)(HitCount + MissCount) : 0;
}

/// <summary>
/// Cache manager for remote filesystem files
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Get file from cache or fetch from remote
    /// </summary>
    Task<Stream> GetOrFetchAsync(
        string urn,
        Func<Task<Stream>> fetchFunc,
        RemoteFileMetadata remoteMetadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if file is cached
    /// </summary>
    Task<bool> IsCachedAsync(string urn);

    /// <summary>
    /// Invalidate cache entry
    /// </summary>
    Task InvalidateAsync(string urn);

    /// <summary>
    /// Clear all cache entries
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();
}
