using System;
using System.Text;
using SmallBin.Logging;
using SmallBin.Services;
using Xunit;

namespace SmallBin.UnitTests
{
    public class CacheServiceTests
    {
        private readonly TestLogger _logger;
        private readonly CacheService _cacheService;

        public CacheServiceTests()
        {
            _logger = new TestLogger();
            _cacheService = new CacheService(
                maxCacheSize: 1024 * 1024, // 1MB for testing
                cacheExpiration: TimeSpan.FromSeconds(1),
                logger: _logger);
        }

        private class TestLogger : ILogger
        {
            public List<string> LogMessages { get; } = new List<string>();

            public void Debug(string message) => LogMessages.Add($"DEBUG: {message}");
            public void Info(string message) => LogMessages.Add($"INFO: {message}");
            public void Warning(string message) => LogMessages.Add($"WARNING: {message}");
            public void Error(string message, Exception? ex = null) => LogMessages.Add($"ERROR: {message}");
            public void Dispose() { }
        }

        [Fact]
        public void AddToCache_WithValidContent_CachesContent()
        {
            // Arrange
            var fileId = "test1";
            var content = Encoding.UTF8.GetBytes("Test content");

            // Act
            _cacheService.AddToCache(fileId, content);
            var cachedContent = _cacheService.TryGetFromCache(fileId);

            // Assert
            Assert.NotNull(cachedContent);
            Assert.Equal(content, cachedContent);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("DEBUG: Added/Updated cache entry"));
        }

        [Fact]
        public void AddToCache_WithNullOrEmptyContent_DoesNotCache()
        {
            // Arrange
            var fileId = "test2";

            // Act
            _cacheService.AddToCache(fileId, null);
            var cachedContent = _cacheService.TryGetFromCache(fileId);

            // Assert
            Assert.Null(cachedContent);
        }

        [Fact]
        public void TryGetFromCache_WithExpiredContent_ReturnsNull()
        {
            // Arrange
            var fileId = "test3";
            var content = Encoding.UTF8.GetBytes("Test content");
            _cacheService.AddToCache(fileId, content);

            // Act
            // Wait for cache to expire
            System.Threading.Thread.Sleep(1100);
            var cachedContent = _cacheService.TryGetFromCache(fileId);

            // Assert
            Assert.Null(cachedContent);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("DEBUG: Cache entry expired"));
        }

        [Fact]
        public void AddToCache_ExceedingMaxSize_RemovesOldestEntries()
        {
            // Arrange
            var content1 = new byte[512 * 1024]; // 512KB
            var content2 = new byte[512 * 1024]; // 512KB
            var content3 = new byte[512 * 1024]; // 512KB

            // Act
            _cacheService.AddToCache("file1", content1);
            _cacheService.AddToCache("file2", content2);
            _cacheService.AddToCache("file3", content3);

            // Assert
            Assert.Null(_cacheService.TryGetFromCache("file1")); // Should be removed
            Assert.NotNull(_cacheService.TryGetFromCache("file2"));
            Assert.NotNull(_cacheService.TryGetFromCache("file3"));
        }

        [Fact]
        public void AddToCache_WithContentLargerThanMaxSize_DoesNotCache()
        {
            // Arrange
            var largeContent = new byte[2 * 1024 * 1024]; // 2MB, larger than cache size

            // Act
            _cacheService.AddToCache("large-file", largeContent);

            // Assert
            Assert.Null(_cacheService.TryGetFromCache("large-file"));
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("DEBUG: File too large to cache"));
        }

        [Fact]
        public void ClearCache_RemovesAllEntries()
        {
            // Arrange
            var content = Encoding.UTF8.GetBytes("Test content");
            _cacheService.AddToCache("file1", content);
            _cacheService.AddToCache("file2", content);

            // Act
            _cacheService.ClearCache();

            // Assert
            Assert.Null(_cacheService.TryGetFromCache("file1"));
            Assert.Null(_cacheService.TryGetFromCache("file2"));
            Assert.Equal(0, _cacheService.CacheCount);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("INFO: Cache cleared"));
        }

        [Fact]
        public void RemoveExpiredEntries_RemovesOnlyExpiredEntries()
        {
            // Arrange
            var content = Encoding.UTF8.GetBytes("Test content");
            _cacheService.AddToCache("expired", content);
            System.Threading.Thread.Sleep(1100); // Wait for first entry to expire
            _cacheService.AddToCache("fresh", content);

            // Act
            _cacheService.RemoveExpiredEntries();

            // Assert
            Assert.Null(_cacheService.TryGetFromCache("expired"));
            Assert.NotNull(_cacheService.TryGetFromCache("fresh"));
        }

        [Fact]
        public void CacheMetrics_TrackHitsAndMisses()
        {
            // Arrange
            var content = Encoding.UTF8.GetBytes("Test content");
            _cacheService.AddToCache("test", content);

            // Act - Generate some hits and misses
            _cacheService.TryGetFromCache("test"); // Hit
            _cacheService.TryGetFromCache("test"); // Hit
            _cacheService.TryGetFromCache("nonexistent"); // Miss
            _cacheService.TryGetFromCache("nonexistent"); // Miss
            _cacheService.TryGetFromCache("test"); // Hit

            // Assert
            Assert.Equal(3, _cacheService.CacheHits);
            Assert.Equal(2, _cacheService.CacheMisses);
        }

        [Fact]
        public void CurrentCacheSize_ReflectsActualSize()
        {
            // Arrange
            var content1 = new byte[100];
            var content2 = new byte[200];

            // Act
            _cacheService.AddToCache("file1", content1);
            _cacheService.AddToCache("file2", content2);

            // Assert
            Assert.Equal(300, _cacheService.CurrentCacheSize);
        }
    }
}
