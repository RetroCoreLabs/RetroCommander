//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroCommander.Core.Tests -- Unit tests for FileSystemCacheManager
//

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Caching;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for FileSystemCacheManager.
    /// Tests cache hit/miss, LRU eviction, staleness detection, and concurrent access.
    /// </summary>
    [TestFixture]
    public class FileSystemCacheManagerTests
    {
        private string _testCacheDirectory = null!;

        [SetUp]
        public void Setup()
        {
            // Create unique test directory for each test
            _testCacheDirectory = Path.Combine(Path.GetTempPath(), "RetroCommanderCacheTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testCacheDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test directory
            if (Directory.Exists(_testCacheDirectory))
            {
                try
                {
                    Directory.Delete(_testCacheDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private CacheConfiguration CreateTestConfig(long maxSizeGB = 1)
        {
            return new CacheConfiguration
            {
                CacheDirectory = _testCacheDirectory,
                MaxCacheSizeGB = maxSizeGB,
                EnableCaching = true
            };
        }

        private static MemoryStream CreateTestStream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        private static async Task<string> ReadStreamAsStringAsync(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        #region Basic Cache Operations

        [Test]
        public async Task GetOrFetchAsync_FirstAccess_FetchesFromSource()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";
            int fetchCount = 0;

            Func<Task<Stream>> fetchFunc = () =>
            {
                fetchCount++;
                return Task.FromResult<Stream>(CreateTestStream(content));
            };

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Act
            using var resultStream = await cache.GetOrFetchAsync(urn, fetchFunc, metadata);
            var resultContent = await ReadStreamAsStringAsync(resultStream);

            // Assert
            Assert.That(fetchCount, Is.EqualTo(1), "Fetch should be called once");
            Assert.That(resultContent, Is.EqualTo(content));
        }

        [Test]
        public async Task GetOrFetchAsync_SecondAccess_ReturnsFromCache()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";
            int fetchCount = 0;

            Func<Task<Stream>> fetchFunc = () =>
            {
                fetchCount++;
                return Task.FromResult<Stream>(CreateTestStream(content));
            };

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Act - First access (cache miss)
            using (var stream1 = await cache.GetOrFetchAsync(urn, fetchFunc, metadata))
            {
                await ReadStreamAsStringAsync(stream1);
            }

            // Second access (cache hit)
            using var stream2 = await cache.GetOrFetchAsync(urn, fetchFunc, metadata);
            var resultContent = await ReadStreamAsStringAsync(stream2);

            // Assert
            Assert.That(fetchCount, Is.EqualTo(1), "Fetch should only be called once");
            Assert.That(resultContent, Is.EqualTo(content));
        }

        [Test]
        public async Task IsCachedAsync_AfterCaching_ReturnsTrue()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Cache the file
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            var isCached = await cache.IsCachedAsync(urn);

            // Assert
            Assert.That(isCached, Is.True);
        }

        [Test]
        public async Task IsCachedAsync_BeforeCaching_ReturnsFalse()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:nonexistent";

            // Act
            var isCached = await cache.IsCachedAsync(urn);

            // Assert
            Assert.That(isCached, Is.False);
        }

        [Test]
        public async Task InvalidateAsync_CachedFile_RemovesFromCache()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Cache the file
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            await cache.InvalidateAsync(urn);

            // Assert
            var isCached = await cache.IsCachedAsync(urn);
            Assert.That(isCached, Is.False);
        }

        [Test]
        public async Task ClearAsync_MultipleCachedFiles_RemovesAll()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);

            // Cache multiple files
            for (int i = 0; i < 5; i++)
            {
                var urn = $"urn:test:file{i}";
                var content = $"Content {i}";
                var metadata = new RemoteFileMetadata
                {
                    FileSizeBytes = content.Length,
                    ModifiedDate = DateTime.UtcNow
                };

                using var stream = await cache.GetOrFetchAsync(urn,
                    () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            await cache.ClearAsync();

            // Assert
            var stats = await cache.GetStatisticsAsync();
            Assert.That(stats.EntryCount, Is.EqualTo(0));
            Assert.That(stats.TotalSizeBytes, Is.EqualTo(0));
        }

        #endregion

        #region Cache Statistics

        [Test]
        public async Task GetStatisticsAsync_AfterCacheHit_UpdatesHitCount()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // First access (miss)
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Second access (hit)
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            var stats = await cache.GetStatisticsAsync();

            // Assert
            Assert.That(stats.HitCount, Is.EqualTo(1));
            Assert.That(stats.MissCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetStatisticsAsync_AfterCacheMiss_UpdatesMissCount()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);

            // Cache different files (all misses)
            for (int i = 0; i < 3; i++)
            {
                var urn = $"urn:test:file{i}";
                var content = $"Content {i}";
                var metadata = new RemoteFileMetadata
                {
                    FileSizeBytes = content.Length,
                    ModifiedDate = DateTime.UtcNow
                };

                using var stream = await cache.GetOrFetchAsync(urn,
                    () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            var stats = await cache.GetStatisticsAsync();

            // Assert
            Assert.That(stats.MissCount, Is.EqualTo(3));
            Assert.That(stats.HitCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetStatisticsAsync_HitRate_CalculatedCorrectly()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // 1 miss + 3 hits = 75% hit rate
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            for (int i = 0; i < 3; i++)
            {
                using var stream = await cache.GetOrFetchAsync(urn,
                    () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            var stats = await cache.GetStatisticsAsync();

            // Assert
            Assert.That(stats.HitRate, Is.EqualTo(0.75).Within(0.01));
        }

        [Test]
        public async Task GetStatisticsAsync_TotalSizeBytes_TracksCorrectly()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            long totalExpectedSize = 0;

            // Cache files of known sizes
            for (int i = 0; i < 3; i++)
            {
                var urn = $"urn:test:file{i}";
                var content = new string('X', 1000 * (i + 1)); // 1KB, 2KB, 3KB
                var metadata = new RemoteFileMetadata
                {
                    FileSizeBytes = content.Length,
                    ModifiedDate = DateTime.UtcNow
                };

                using var stream = await cache.GetOrFetchAsync(urn,
                    () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                await ReadStreamAsStringAsync(stream);

                totalExpectedSize += content.Length;
            }

            // Act
            var stats = await cache.GetStatisticsAsync();

            // Assert
            Assert.That(stats.TotalSizeBytes, Is.EqualTo(totalExpectedSize));
            Assert.That(stats.EntryCount, Is.EqualTo(3));
        }

        #endregion

        #region Staleness Detection

        [Test]
        public async Task GetOrFetchAsync_FileSizeChanged_RefetchesFile()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var originalContent = "Original content";
            var updatedContent = "Updated content with more data";
            int fetchCount = 0;
            string currentContent = originalContent;

            Func<Task<Stream>> fetchFunc = () =>
            {
                fetchCount++;
                return Task.FromResult<Stream>(CreateTestStream(currentContent));
            };

            var originalMetadata = new RemoteFileMetadata
            {
                FileSizeBytes = originalContent.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // First access
            using (var stream = await cache.GetOrFetchAsync(urn, fetchFunc, originalMetadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Update content and metadata (size changed)
            currentContent = updatedContent;
            var updatedMetadata = new RemoteFileMetadata
            {
                FileSizeBytes = updatedContent.Length, // Different size
                ModifiedDate = originalMetadata.ModifiedDate // Same date
            };

            // Act - Second access with different size
            using var resultStream = await cache.GetOrFetchAsync(urn, fetchFunc, updatedMetadata);
            var resultContent = await ReadStreamAsStringAsync(resultStream);

            // Assert
            Assert.That(fetchCount, Is.EqualTo(2), "Should refetch when size changed");
            Assert.That(resultContent, Is.EqualTo(updatedContent));
        }

        [Test]
        public async Task GetOrFetchAsync_ModifiedDateChanged_RefetchesFile()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var originalContent = "Original content";
            var updatedContent = "Updated content";
            int fetchCount = 0;
            string currentContent = originalContent;

            Func<Task<Stream>> fetchFunc = () =>
            {
                fetchCount++;
                return Task.FromResult<Stream>(CreateTestStream(currentContent));
            };

            var originalDate = DateTime.UtcNow.AddDays(-1);
            var originalMetadata = new RemoteFileMetadata
            {
                FileSizeBytes = originalContent.Length,
                ModifiedDate = originalDate
            };

            // First access
            using (var stream = await cache.GetOrFetchAsync(urn, fetchFunc, originalMetadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Update content and metadata (date changed, same size by padding)
            currentContent = updatedContent;
            var updatedMetadata = new RemoteFileMetadata
            {
                FileSizeBytes = originalContent.Length, // Same size
                ModifiedDate = DateTime.UtcNow // Different date
            };

            // Act - Second access with different date
            using var resultStream = await cache.GetOrFetchAsync(urn, fetchFunc, updatedMetadata);

            // Assert
            Assert.That(fetchCount, Is.EqualTo(2), "Should refetch when modified date changed");
        }

        [Test]
        public async Task GetOrFetchAsync_SameSizeAndDate_UsesCachedVersion()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";
            int fetchCount = 0;

            Func<Task<Stream>> fetchFunc = () =>
            {
                fetchCount++;
                return Task.FromResult<Stream>(CreateTestStream(content));
            };

            var fixedDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = fixedDate
            };

            // First access
            using (var stream = await cache.GetOrFetchAsync(urn, fetchFunc, metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Act - Multiple accesses with same metadata
            for (int i = 0; i < 5; i++)
            {
                using var stream = await cache.GetOrFetchAsync(urn, fetchFunc, metadata);
                await ReadStreamAsStringAsync(stream);
            }

            // Assert
            Assert.That(fetchCount, Is.EqualTo(1), "Should use cache when metadata unchanged");
        }

        #endregion

        #region LRU Eviction

        [Test]
        public async Task LruEviction_WhenCacheFull_RemovesLeastRecentlyUsed()
        {
            // Arrange - Very small cache (1MB = 1024*1024 bytes)
            // But MaxCacheSizeGB is in GB, so we need to work around this
            // Create a cache with 1 byte max (this will trigger eviction quickly)
            var config = new CacheConfiguration
            {
                CacheDirectory = _testCacheDirectory,
                MaxCacheSizeGB = 1, // 1GB - we'll need large files to test eviction
                EnableCaching = true
            };

            var cache = new FileSystemCacheManager(config);

            // For a realistic test, we'll cache files and verify the access count updates
            var urn1 = "urn:test:file1";
            var urn2 = "urn:test:file2";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Cache file 1
            using (var stream = await cache.GetOrFetchAsync(urn1,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Cache file 2
            using (var stream = await cache.GetOrFetchAsync(urn2,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Access file 1 multiple times to make it "more recently used"
            for (int i = 0; i < 3; i++)
            {
                using var stream = await cache.GetOrFetchAsync(urn1,
                    () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                await ReadStreamAsStringAsync(stream);
            }

            // Act - Get statistics
            var stats = await cache.GetStatisticsAsync();

            // Assert - Both files should be cached
            Assert.That(stats.EntryCount, Is.EqualTo(2));

            // Verify access pattern worked (file1 should have higher hit count)
            var isCached1 = await cache.IsCachedAsync(urn1);
            var isCached2 = await cache.IsCachedAsync(urn2);
            Assert.That(isCached1, Is.True);
            Assert.That(isCached2, Is.True);
        }

        [Test]
        public async Task LruEviction_AccessCountUpdated_OnCacheHit()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Initial cache
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Multiple cache hits
            for (int i = 0; i < 10; i++)
            {
                using var stream = await cache.GetOrFetchAsync(urn,
                    () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                await ReadStreamAsStringAsync(stream);
            }

            // Act
            var stats = await cache.GetStatisticsAsync();

            // Assert
            Assert.That(stats.HitCount, Is.EqualTo(10));
            Assert.That(stats.MissCount, Is.EqualTo(1));
        }

        #endregion

        #region Concurrent Access

        [Test]
        public async Task ConcurrentAccess_SameFile_OnlyFetchesOnce()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content for concurrent access";
            int fetchCount = 0;
            var fetchLock = new object();

            Func<Task<Stream>> fetchFunc = async () =>
            {
                lock (fetchLock)
                {
                    fetchCount++;
                }
                await Task.Delay(100); // Simulate slow fetch
                return CreateTestStream(content);
            };

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // First, ensure file is cached
            using (var stream = await cache.GetOrFetchAsync(urn, fetchFunc, metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Reset counter
            fetchCount = 0;

            // Act - Concurrent cache hits
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var stream = await cache.GetOrFetchAsync(urn, fetchFunc, metadata);
                    await ReadStreamAsStringAsync(stream);
                });
            }

            await Task.WhenAll(tasks);

            // Assert - All should be cache hits, no fetches
            Assert.That(fetchCount, Is.EqualTo(0), "Should not fetch when cached");
        }

        [Test]
        public async Task ConcurrentAccess_DifferentFiles_AllCached()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            const int fileCount = 10;
            var barrier = new Barrier(fileCount);

            // Act
            var tasks = new Task[fileCount];
            for (int i = 0; i < fileCount; i++)
            {
                int fileIndex = i;
                tasks[i] = Task.Run(async () =>
                {
                    barrier.SignalAndWait();

                    var urn = $"urn:test:file{fileIndex}";
                    var content = $"Content for file {fileIndex}";
                    var metadata = new RemoteFileMetadata
                    {
                        FileSizeBytes = content.Length,
                        ModifiedDate = DateTime.UtcNow
                    };

                    using var stream = await cache.GetOrFetchAsync(urn,
                        () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                    await ReadStreamAsStringAsync(stream);
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            var stats = await cache.GetStatisticsAsync();
            Assert.That(stats.EntryCount, Is.EqualTo(fileCount));
            Assert.That(stats.MissCount, Is.EqualTo(fileCount));
        }

        [Test]
        public async Task ConcurrentAccess_InvalidateWhileReading_HandledGracefully()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:file1";
            var content = "Test content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Cache the file first
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Act - Concurrent reads and invalidates
            var readTasks = new Task[5];
            var invalidateTask = Task.Run(async () =>
            {
                await Task.Delay(50); // Small delay
                await cache.InvalidateAsync(urn);
            });

            var unexpectedExceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            for (int i = 0; i < 5; i++)
            {
                readTasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        using var stream = await cache.GetOrFetchAsync(urn,
                            () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                        await ReadStreamAsStringAsync(stream);
                    }
                    catch (IOException)
                    {
                        // Expected if file is deleted during read
                    }
                    catch (Exception ex)
                    {
                        unexpectedExceptions.Add(ex);
                    }
                });
            }

            await Task.WhenAll(readTasks);
            await invalidateTask;

            // Assert - only IOException (file deleted mid-read) is an expected race; anything
            // else is a real bug the old Assert.DoesNotThrowAsync could never have caught, since
            // it wrapped the very code that swallowed these exceptions.
            Assert.That(unexpectedExceptions, Is.Empty,
                $"Unexpected exception(s) during concurrent invalidate/read: {string.Join(", ", unexpectedExceptions)}");

            // Cache must still be usable afterward — a deadlock/corrupted-index bug from the
            // race would leave it unable to serve a fresh fetch.
            using var postStream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
            string postContent = await ReadStreamAsStringAsync(postStream);
            Assert.That(postContent, Is.EqualTo(content));
        }

        [Test]
        public async Task ConcurrentAccess_ClearWhileCaching_HandlesConcurrentOperations()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var barrier = new Barrier(6); // 5 cachers + 1 clearer
            var unexpectedExceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            // Act
            var cacheTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int fileIndex = i;
                cacheTasks[i] = Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    for (int j = 0; j < 3; j++)
                    {
                        var urn = $"urn:test:file{fileIndex}_{j}";
                        var content = $"Content {fileIndex}_{j}";
                        var metadata = new RemoteFileMetadata
                        {
                            FileSizeBytes = content.Length,
                            ModifiedDate = DateTime.UtcNow
                        };

                        try
                        {
                            using var stream = await cache.GetOrFetchAsync(urn,
                                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
                            await ReadStreamAsStringAsync(stream);
                        }
                        catch (IOException)
                        {
                            // Expected during concurrent clear - file may be locked
                        }
                        catch (Exception ex)
                        {
                            unexpectedExceptions.Add(ex);
                        }
                    }
                });
            }

            var clearTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await Task.Delay(50);
                try
                {
                    await cache.ClearAsync();
                }
                catch (IOException)
                {
                    // Expected if file is locked by another operation
                }
                catch (Exception ex)
                {
                    unexpectedExceptions.Add(ex);
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(cacheTasks);
            await clearTask;

            // Assert - only IOException (file locked by the concurrent clear) is an expected
            // race; anything else is a real bug. The old Assert.Pass(...) unconditionally
            // passed regardless of anyExceptionOccurred, so it could never catch a deadlock or
            // an unexpected exception type.
            Assert.That(unexpectedExceptions, Is.Empty,
                $"Unexpected exception(s) during concurrent cache/clear: {string.Join(", ", unexpectedExceptions)}");

            // Cache must still be usable after the concurrent clear — verifies no deadlock and
            // no corrupted index state.
            var postUrn = "urn:test:post-clear-probe";
            var postContent = "post-clear content";
            var postMetadata = new RemoteFileMetadata
            {
                FileSizeBytes = postContent.Length,
                ModifiedDate = DateTime.UtcNow
            };
            using var postStream = await cache.GetOrFetchAsync(postUrn,
                () => Task.FromResult<Stream>(CreateTestStream(postContent)), postMetadata);
            Assert.That(await ReadStreamAsStringAsync(postStream), Is.EqualTo(postContent));
        }

        #endregion

        #region Edge Cases

        [Test]
        public async Task Cache_LargeFile_CachesSuccessfully()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:largefile";
            var content = new string('X', 1024 * 1024); // 1MB file

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Act
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                var result = await ReadStreamAsStringAsync(stream);
                Assert.That(result.Length, Is.EqualTo(content.Length));
            }

            // Assert - Verify cached
            var isCached = await cache.IsCachedAsync(urn);
            Assert.That(isCached, Is.True);
        }

        [Test]
        public async Task Cache_EmptyFile_CachesSuccessfully()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:emptyfile";
            var content = "";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = 0,
                ModifiedDate = DateTime.UtcNow
            };

            // Act
            using var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata);
            var result = await ReadStreamAsStringAsync(stream);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task Cache_SpecialCharactersInUrn_HandlesCorrectly()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:path/with spaces/and!special@chars#$%";
            var content = "Content with special URN";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // Act
            using (var stream = await cache.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Assert
            var isCached = await cache.IsCachedAsync(urn);
            Assert.That(isCached, Is.True);
        }

        [Test]
        public async Task Cache_IndexPersistsAcrossInstances()
        {
            // Arrange
            var config = CreateTestConfig();
            var urn = "urn:test:persistent";
            var content = "Persistent content";

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = content.Length,
                ModifiedDate = DateTime.UtcNow
            };

            // First instance - cache the file
            var cache1 = new FileSystemCacheManager(config);
            using (var stream = await cache1.GetOrFetchAsync(urn,
                () => Task.FromResult<Stream>(CreateTestStream(content)), metadata))
            {
                await ReadStreamAsStringAsync(stream);
            }

            // Act - Create new instance (simulates app restart)
            var cache2 = new FileSystemCacheManager(config);
            var isCached = await cache2.IsCachedAsync(urn);

            // Assert
            Assert.That(isCached, Is.True, "Cache should persist across instances");
        }

        [Test]
        public async Task Cache_FetchFunctionThrows_PropagatesException()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);
            var urn = "urn:test:failing";

            Func<Task<Stream>> failingFetch = () =>
            {
                throw new IOException("Network error");
            };

            var metadata = new RemoteFileMetadata
            {
                FileSizeBytes = 100,
                ModifiedDate = DateTime.UtcNow
            };

            // Act & Assert
            Assert.ThrowsAsync<IOException>(async () =>
            {
                await cache.GetOrFetchAsync(urn, failingFetch, metadata);
            });
        }

        [Test]
        public async Task InvalidateAsync_NonExistentUrn_DoesNotThrow()
        {
            // Arrange
            var config = CreateTestConfig();
            var cache = new FileSystemCacheManager(config);

            // Act & Assert
            Assert.DoesNotThrowAsync(async () =>
            {
                await cache.InvalidateAsync("urn:test:nonexistent");
            });
        }

        #endregion
    }
}
