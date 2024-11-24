using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SmallBin.Logging;
using SmallBin.Models;

namespace SmallBin.Services
{
    /// <summary>
    ///     Provides caching functionality for frequently accessed files
    /// </summary>
    internal class CacheService
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly int _maxCacheSize;
        private readonly TimeSpan _cacheExpiration;
        private readonly ILogger? _logger;
        private long _cacheHits;
        private long _cacheMisses;

        private class CacheEntry
        {
            public byte[] Content { get; set; }
            public DateTime LastAccessed { get; set; }
            public DateTime ExpiresAt { get; set; }
            public long Size { get; set; }
        }

        /// <summary>
        ///     Initializes a new instance of the CacheService class
        /// </summary>
        /// <param name="maxCacheSize">Maximum cache size in bytes (default 100MB)</param>
        /// <param name="cacheExpiration">Cache entry expiration time (default 1 hour)</param>
        /// <param name="logger">Optional logger for tracking cache operations</param>
        public CacheService(
            long maxCacheSize = 100 * 1024 * 1024, // 100MB default
            TimeSpan? cacheExpiration = null,
            ILogger? logger = null)
        {
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _maxCacheSize = (int)maxCacheSize;
            _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(1);
            _logger = logger;
            _cacheHits = 0;
            _cacheMisses = 0;
        }

        /// <summary>
        ///     Gets the current cache size in bytes
        /// </summary>
        public long CurrentCacheSize => _cache.Values.Sum(entry => entry.Size);

        /// <summary>
        ///     Gets the current number of items in the cache
        /// </summary>
        public int CacheCount => _cache.Count;

        /// <summary>
        ///     Gets the cache hit count
        /// </summary>
        public long CacheHits => _cacheHits;

        /// <summary>
        ///     Gets the cache miss count
        /// </summary>
        public long CacheMisses => _cacheMisses;

        /// <summary>
        ///     Attempts to get a file from the cache
        /// </summary>
        /// <param name="fileId">The ID of the file to retrieve</param>
        /// <returns>The cached file content if found and not expired, null otherwise</returns>
        public byte[]? TryGetFromCache(string fileId)
        {
            if (_cache.TryGetValue(fileId, out var entry))
            {
                if (DateTime.UtcNow > entry.ExpiresAt)
                {
                    _logger?.Debug($"Cache entry expired for file: {fileId}");
                    RemoveFromCache(fileId);
                    System.Threading.Interlocked.Increment(ref _cacheMisses);
                    return null;
                }

                entry.LastAccessed = DateTime.UtcNow;
                System.Threading.Interlocked.Increment(ref _cacheHits);
                _logger?.Debug($"Cache hit for file: {fileId}");
                return entry.Content;
            }

            System.Threading.Interlocked.Increment(ref _cacheMisses);
            _logger?.Debug($"Cache miss for file: {fileId}");
            return null;
        }

        /// <summary>
        ///     Adds or updates a file in the cache
        /// </summary>
        /// <param name="fileId">The ID of the file to cache</param>
        /// <param name="content">The file content to cache</param>
        public void AddToCache(string fileId, byte[] content)
        {
            if (content == null || content.Length == 0)
                return;

            var entry = new CacheEntry
            {
                Content = content,
                LastAccessed = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_cacheExpiration),
                Size = content.Length
            };

            // Ensure we have space for the new entry
            while (CurrentCacheSize + content.Length > _maxCacheSize && _cache.Count > 0)
            {
                RemoveLeastRecentlyUsed();
            }

            // Only cache if it fits within size limits
            if (content.Length <= _maxCacheSize)
            {
                _cache.AddOrUpdate(fileId, entry, (key, oldValue) => entry);
                _logger?.Debug($"Added/Updated cache entry for file: {fileId}");
            }
            else
            {
                _logger?.Debug($"File too large to cache: {fileId}");
            }
        }

        /// <summary>
        ///     Removes a file from the cache
        /// </summary>
        /// <param name="fileId">The ID of the file to remove</param>
        public void RemoveFromCache(string fileId)
        {
            if (_cache.TryRemove(fileId, out _))
            {
                _logger?.Debug($"Removed cache entry for file: {fileId}");
            }
        }

        /// <summary>
        ///     Clears all entries from the cache
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _logger?.Info("Cache cleared");
        }

        /// <summary>
        ///     Removes expired entries from the cache
        /// </summary>
        public void RemoveExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                RemoveFromCache(key);
            }

            _logger?.Debug($"Removed {expiredKeys.Count} expired cache entries");
        }

        private void RemoveLeastRecentlyUsed()
        {
            var lru = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(lru.Key))
            {
                RemoveFromCache(lru.Key);
            }
        }
    }
}
