using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;
using SmallBin.Services;
using Xunit;

namespace SmallBin.UnitTests
{
    public class FileOperationServiceTests : IDisposable
    {
        private readonly string _testFilePath;
        private readonly TestLogger _logger;
        private readonly EncryptionService _encryptionService;
        private readonly CompressionService _compressionService;
        private readonly FileOperationService _fileOperationService;
        private readonly byte[] _testKey;

        public FileOperationServiceTests()
        {
            // Create a temporary test file
            _testFilePath = Path.GetTempFileName();
            File.WriteAllText(_testFilePath, "Test file content");

            // Setup services
            _logger = new TestLogger();
            _testKey = new byte[32];
            RandomNumberGenerator.Fill(_testKey);
            _encryptionService = new EncryptionService(_testKey);
            _compressionService = new CompressionService();
            _fileOperationService = new FileOperationService(_encryptionService, _compressionService, true, _logger);
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

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
            public void Dispose() { }
        }

        [Fact]
        public void Constructor_WithNullEncryptionService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new FileOperationService(null, _compressionService));
        }

        [Fact]
        public void Constructor_WithNullCompressionService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new FileOperationService(_encryptionService, null));
        }

        [Fact]
        public void SaveFile_WithValidFile_CreatesFileEntry()
        {
            // Act
            var entry = _fileOperationService.SaveFile(_testFilePath, new List<string> { "test" }, "text/plain");

            // Assert
            Assert.NotNull(entry);
            Assert.Equal(Path.GetFileName(_testFilePath), entry.FileName);
            Assert.Contains("test", entry.Tags);
            Assert.Equal("text/plain", entry.ContentType);
            Assert.True(entry.IsCompressed);
            Assert.NotNull(entry.EncryptedContent);
            Assert.NotNull(entry.IV);
            Assert.True(entry.FileSize > 0);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("INFO: Saving file:"));
        }

        [Fact]
        public void SaveFile_WithoutCompression_CreatesUncompressedFileEntry()
        {
            // Arrange
            var service = new FileOperationService(_encryptionService, _compressionService, false, _logger);

            // Act
            var entry = service.SaveFile(_testFilePath);

            // Assert
            Assert.False(entry.IsCompressed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SaveFile_WithInvalidFilePath_ThrowsArgumentNullException(string invalidPath)
        {
            Assert.Throws<ArgumentNullException>(() => 
                _fileOperationService.SaveFile(invalidPath));
        }

        [Fact]
        public void SaveFile_WithNonexistentFile_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => 
                _fileOperationService.SaveFile("nonexistent.txt"));
        }

        [Fact]
        public void GetFile_WithValidEntry_ReturnsOriginalContent()
        {
            // Arrange
            var originalContent = "Test file content";
            File.WriteAllText(_testFilePath, originalContent);
            var entry = _fileOperationService.SaveFile(_testFilePath);

            // Act
            var retrievedContent = _fileOperationService.GetFile(entry);

            // Assert
            Assert.Equal(originalContent, Encoding.UTF8.GetString(retrievedContent));
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("DEBUG: Retrieving file:"));
        }

        [Fact]
        public void GetFile_WithNullEntry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                _fileOperationService.GetFile(null));
        }

        [Fact]
        public void GetFile_WithCorruptedContent_ThrowsDatabaseCorruptException()
        {
            // Arrange
            var entry = _fileOperationService.SaveFile(_testFilePath);
            entry.EncryptedContent = new byte[] { 1, 2, 3 }; // Corrupt the content

            // Act & Assert
            Assert.Throws<DatabaseEncryptionException>(() => 
                _fileOperationService.GetFile(entry));
        }

        [Fact]
        public void UpdateMetadata_WithValidAction_UpdatesEntry()
        {
            // Arrange
            var entry = _fileOperationService.SaveFile(_testFilePath);
            var originalUpdateTime = entry.UpdatedOn;

            // Act
            _fileOperationService.UpdateMetadata(entry, e => 
            {
                e.Tags.Add("newTag");
                e.ContentType = "application/test";
            });

            // Assert
            Assert.Contains("newTag", entry.Tags);
            Assert.Equal("application/test", entry.ContentType);
            Assert.True(entry.UpdatedOn > originalUpdateTime);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("INFO: Metadata updated successfully"));
        }

        [Fact]
        public void UpdateMetadata_WithNullEntry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                _fileOperationService.UpdateMetadata(null, _ => { }));
        }

        [Fact]
        public void UpdateMetadata_WithNullAction_ThrowsArgumentNullException()
        {
            // Arrange
            var entry = _fileOperationService.SaveFile(_testFilePath);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _fileOperationService.UpdateMetadata(entry, null));
        }

        [Fact]
        public void UpdateMetadata_WithThrowingAction_ThrowsDatabaseOperationException()
        {
            // Arrange
            var entry = _fileOperationService.SaveFile(_testFilePath);

            // Act & Assert
            var exception = Assert.Throws<DatabaseOperationException>(() => 
                _fileOperationService.UpdateMetadata(entry, _ => throw new Exception("Test exception")));

            Assert.Contains(_logger.LogMessages, m => m.StartsWith("ERROR: Failed to update metadata"));
            Assert.Single(_logger.LoggedExceptions);
        }

        [Fact]
        public void SaveFile_WithEmptyFile_ThrowsFileValidationException()
        {
            // Arrange
            File.WriteAllText(_testFilePath, ""); // Create empty file

            // Act & Assert
            Assert.Throws<FileValidationException>(() => 
                _fileOperationService.SaveFile(_testFilePath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SaveFile_WithInvalidContentType_ThrowsArgumentNullException(string invalidContentType)
        {
            Assert.Throws<ArgumentNullException>(() => 
                _fileOperationService.SaveFile(_testFilePath, contentType: invalidContentType));
        }
    }
}
