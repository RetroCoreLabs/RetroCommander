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
    /// <summary>
    /// Current size of the remote file in bytes. A cached copy is used only when its stored
    /// size matches this.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Current last-modified time of the remote file. A cached copy is used only when its
    /// stored time matches this exactly.
    /// </summary>
    public DateTime ModifiedDate { get; set; }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total size of all cached files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Largest size the cache may grow to, in bytes.
    /// </summary>
    public long MaxSizeBytes { get; set; }

    /// <summary>
    /// Number of files in the cache.
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// Number of requests served from a fresh cached copy.
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Number of requests that had to fetch the file from the remote side.
    /// </summary>
    public long MissCount { get; set; }

    /// <summary>
    /// <see cref="HitCount"/> divided by all requests, from 0 to 1. Returns 0 when there have
    /// been no requests.
    /// </summary>
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
