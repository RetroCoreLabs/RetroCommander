//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RetroCommander.Core.Caching;

/// <summary>
/// File-based cache manager with LRU eviction
/// </summary>
public class FileSystemCacheManager : ICacheManager
{
    private readonly string _cacheDirectory;
    private readonly long _maxCacheSizeBytes;
    private readonly CacheIndex _index;
    private readonly string _indexFilePath;
    private readonly object _lock = new();
    private long _hitCount;
    private long _missCount;

    public FileSystemCacheManager(CacheConfiguration config)
    {
        _cacheDirectory = config.CacheDirectory;
        _maxCacheSizeBytes = config.MaxCacheSizeGB * 1024L * 1024L * 1024L;
        _indexFilePath = Path.Combine(_cacheDirectory, "cache-index.json");

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);

        _index = LoadIndex();
    }

    public async Task<Stream> GetOrFetchAsync(
        string urn,
        Func<Task<Stream>> fetchFunc,
        RemoteFileMetadata remoteMetadata,
        CancellationToken cancellationToken = default)
    {
        // Check if cached
        var entry = GetCacheEntry(urn);

        if (entry != null)
        {
            // Validate cache is still fresh
            if (IsCacheFresh(entry, remoteMetadata))
            {
                // Cache hit
                Interlocked.Increment(ref _hitCount);
                UpdateAccessStats(entry);
                SaveIndex();
                return File.OpenRead(entry.CacheFilePath);
            }
            else
            {
                // Stale cache - invalidate
                await InvalidateAsync(urn);
            }
        }

        // Cache miss or stale - fetch from remote
        Interlocked.Increment(ref _missCount);
        var remoteStream = await fetchFunc();

        // Save to cache
        var cachedPath = await SaveToCacheAsync(urn, remoteStream, remoteMetadata, cancellationToken);

        // Return cached file stream
        return File.OpenRead(cachedPath);
    }

    public Task<bool> IsCachedAsync(string urn)
    {
        return Task.FromResult(GetCacheEntry(urn) != null);
    }

    public Task InvalidateAsync(string urn)
    {
        lock (_lock)
        {
            var entry = _index.Entries.FirstOrDefault(e => e.Urn == urn);
            if (entry != null)
            {
                if (File.Exists(entry.CacheFilePath))
                {
                    File.Delete(entry.CacheFilePath);
                }

                _index.Entries.Remove(entry);
                _index.TotalSizeBytes -= entry.FileSizeBytes;
            }
        }

        SaveIndex();
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        lock (_lock)
        {
            // Delete all cache files
            foreach (var entry in _index.Entries)
            {
                if (File.Exists(entry.CacheFilePath))
                {
                    File.Delete(entry.CacheFilePath);
                }
            }

            _index.Entries.Clear();
            _index.TotalSizeBytes = 0;
        }

        SaveIndex();
        return Task.CompletedTask;
    }

    public Task<CacheStatistics> GetStatisticsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(new CacheStatistics
            {
                TotalSizeBytes = _index.TotalSizeBytes,
                MaxSizeBytes = _maxCacheSizeBytes,
                EntryCount = _index.Entries.Count,
                HitCount = _hitCount,
                MissCount = _missCount
            });
        }
    }

    private bool IsCacheFresh(CacheEntryMetadata entry, RemoteFileMetadata remote)
    {
        // Check if remote file has changed
        return entry.FileSizeBytes == remote.FileSizeBytes &&
               entry.RemoteModifiedDate == remote.ModifiedDate;
    }

    private async Task<string> SaveToCacheAsync(
        string urn,
        Stream sourceStream,
        RemoteFileMetadata metadata,
        CancellationToken cancellationToken)
    {
        // Ensure space available
        await EnsureCacheSpaceAsync(metadata.FileSizeBytes);

        // Generate cache file path
        var cacheFileName = GenerateCacheFileName(urn);
        var cacheFilePath = Path.Combine(_cacheDirectory, cacheFileName);

        // Write to cache with MD5 calculation
        string md5Hash;
        using (var md5 = MD5.Create())
        using (var fileStream = File.Create(cacheFilePath))
        {
            var buffer = new byte[81920]; // 80KB buffer
            int bytesRead;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            md5.TransformFinalBlock(buffer, 0, 0);
            md5Hash = BitConverter.ToString(md5.Hash!).Replace("-", "");
        }

        // Add to index
        var entry = new CacheEntryMetadata
        {
            Urn = urn,
            CacheFilePath = cacheFilePath,
            FileSizeBytes = metadata.FileSizeBytes,
            RemoteModifiedDate = metadata.ModifiedDate,
            Md5Hash = md5Hash,
            CachedDate = DateTime.UtcNow,
            LastAccessedDate = DateTime.UtcNow,
            AccessCount = 1
        };

        lock (_lock)
        {
            _index.Entries.Add(entry);
            _index.TotalSizeBytes += metadata.FileSizeBytes;
        }

        SaveIndex();

        return cacheFilePath;
    }

    private Task EnsureCacheSpaceAsync(long requiredBytes)
    {
        lock (_lock)
        {
            // Check if we need to free space (LRU eviction)
            while (_index.TotalSizeBytes + requiredBytes > _maxCacheSizeBytes && _index.Entries.Count > 0)
            {
                // Remove least recently accessed entry
                var lruEntry = _index.Entries
                    .OrderBy(e => e.LastAccessedDate)
                    .First();

                // Delete cache file
                if (File.Exists(lruEntry.CacheFilePath))
                {
                    File.Delete(lruEntry.CacheFilePath);
                }

                // Remove from index
                _index.Entries.Remove(lruEntry);
                _index.TotalSizeBytes -= lruEntry.FileSizeBytes;
            }
        }

        SaveIndex();
        return Task.CompletedTask;
    }

    private CacheEntryMetadata? GetCacheEntry(string urn)
    {
        lock (_lock)
        {
            return _index.Entries.FirstOrDefault(e => e.Urn == urn);
        }
    }

    private void UpdateAccessStats(CacheEntryMetadata entry)
    {
        lock (_lock)
        {
            entry.LastAccessedDate = DateTime.UtcNow;
            entry.AccessCount++;
        }
    }

    private string GenerateCacheFileName(string urn)
    {
        // Generate safe filename from URN using SHA256 hash
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(urn));
        return BitConverter.ToString(hash).Replace("-", "");
    }

    private CacheIndex LoadIndex()
    {
        lock (_lock)
        {
            if (File.Exists(_indexFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_indexFilePath);
                    return JsonSerializer.Deserialize<CacheIndex>(json) ?? new CacheIndex();
                }
                catch
                {
                    // If index is corrupted, start fresh
                    return new CacheIndex();
                }
            }

            return new CacheIndex();
        }
    }

    private void SaveIndex()
    {
        lock (_lock)
        {
            _index.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(_index, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_indexFilePath, json);
        }
    }
}
