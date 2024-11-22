using System;
using System.Collections.Generic;
using System.Linq;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;
using SmallBin.Services;
using Xunit;

namespace SmallBin.UnitTests
{
    public class SearchServiceTests
    {
        private class TestLogger : ILogger
        {
            public List<string> LogMessages { get; } = new List<string>();
            public List<Exception> LoggedExceptions { get; } = new List<Exception>();

            public void Debug(string message) => LogMessages.Add($"DEBUG: {message}");
            public void Info(string message) => LogMessages.Add($"INFO: {message}");
            public void Warning(string message) => LogMessages.Add($"WARNING: {message}");
            public void Error(string message, Exception? ex = null)
            {
                LogMessages.Add($"ERROR: {message}");
                if (ex != null)
                    LoggedExceptions.Add(ex);
            }
            public void Dispose() { } // Implement IDisposable
        }

        private readonly SearchService _searchService;
        private readonly TestLogger _logger;
        private readonly List<FileEntry> _testFiles;

        public SearchServiceTests()
        {
            _logger = new TestLogger();
            _searchService = new SearchService(_logger);

            _testFiles = new List<FileEntry>
            {
                new FileEntry 
                { 
                    FileName = "document1.txt",
                    ContentType = "text/plain",
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    FileSize = 1024
                },
                new FileEntry 
                { 
                    FileName = "document2.pdf",
                    ContentType = "application/pdf",
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    FileSize = 2048
                },
                new FileEntry 
                { 
                    FileName = "image.jpg",
                    ContentType = "image/jpeg",
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    FileSize = 4096
                },
                new FileEntry 
                { 
                    FileName = "TEST.txt",
                    ContentType = "text/plain",
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    FileSize = 512
                }
            };
        }

        [Fact]
        public void Search_WithNullFiles_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _searchService.Search(null, new SearchCriteria()));
        }

        [Fact]
        public void Search_WithNullCriteria_ReturnsAllFiles()
        {
            // Act
            var results = _searchService.Search(_testFiles, null).ToList();

            // Assert
            Assert.Equal(_testFiles.Count, results.Count);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("DEBUG: Searching files with criteria: all"));
            Assert.Contains(_logger.LogMessages, m => m.StartsWith($"INFO: Search completed. Found {_testFiles.Count} matches"));
        }

        [Fact]
        public void Search_WithEmptyCriteria_ReturnsAllFiles()
        {
            // Arrange
            var criteria = new SearchCriteria { FileName = "" };

            // Act
            var results = _searchService.Search(_testFiles, criteria).ToList();

            // Assert
            Assert.Equal(_testFiles.Count, results.Count);
        }

        [Fact]
        public void Search_WithFileName_ReturnsCaseInsensitiveMatches()
        {
            // Arrange
            var criteria = new SearchCriteria { FileName = "txt" };

            // Act
            var results = _searchService.Search(_testFiles, criteria).ToList();

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(results, f => f.FileName.Equals("document1.txt"));
            Assert.Contains(results, f => f.FileName.Equals("TEST.txt"));
        }

        [Fact]
        public void Search_WithNoMatches_ReturnsEmptyList()
        {
            // Arrange
            var criteria = new SearchCriteria { FileName = "nonexistent" };

            // Act
            var results = _searchService.Search(_testFiles, criteria).ToList();

            // Assert
            Assert.Empty(results);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("INFO: Search completed. Found 0 matches"));
        }

        [Fact]
        public void Search_WithException_LogsErrorAndThrowsDatabaseOperationException()
        {
            // Arrange
            var badFiles = new List<FileEntry> { null }; // Will cause NullReferenceException
            var criteria = new SearchCriteria { FileName = "test" };

            // Act & Assert
            var exception = Assert.Throws<DatabaseOperationException>(() => 
                _searchService.Search(badFiles, criteria).ToList());

            Assert.Contains(_logger.LogMessages, m => m.StartsWith("ERROR: Search operation failed"));
            Assert.Single(_logger.LoggedExceptions);
            Assert.IsType<NullReferenceException>(_logger.LoggedExceptions[0]);
        }

        [Fact]
        public void Search_WithoutLogger_HandlesNullLoggerGracefully()
        {
            // Arrange
            var searchService = new SearchService(null);
            var criteria = new SearchCriteria { FileName = "txt" };

            // Act
            var results = searchService.Search(_testFiles, criteria).ToList();

            // Assert
            Assert.Equal(2, results.Count);
        }

        [Theory]
        [InlineData("doc", 2)] // Should match document1.txt and document2.pdf
        [InlineData("PDF", 1)] // Should match document2.pdf (case insensitive)
        [InlineData(".jpg", 1)] // Should match image.jpg
        [InlineData("test", 1)] // Should match TEST.txt (case insensitive)
        public void Search_WithVariousPatterns_ReturnsCorrectMatches(string searchPattern, int expectedMatches)
        {
            // Arrange
            var criteria = new SearchCriteria { FileName = searchPattern };

            // Act
            var results = _searchService.Search(_testFiles, criteria).ToList();

            // Assert
            Assert.Equal(expectedMatches, results.Count);
        }
    }
}
