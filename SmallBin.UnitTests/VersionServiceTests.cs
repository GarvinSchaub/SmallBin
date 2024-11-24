using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using SmallBin.Models;
using SmallBin.Services;
using SmallBin.Logging;
using Xunit;

namespace SmallBin.UnitTests
{
    public class VersionServiceTests : IDisposable
    {
        private readonly VersionService _versionService;
        private readonly FileOperationService _fileOperationService;
        private readonly string _testFilePath;
        private readonly string _testFileV2Path;
        private readonly TestLogger _logger;

        public VersionServiceTests()
        {
            // Setup encryption key
            var testKey = new byte[32];
            RandomNumberGenerator.Fill(testKey);

            // Setup services
            _logger = new TestLogger();
            var encryptionService = new EncryptionService(testKey);
            var compressionService = new CompressionService();
            var checksumService = new ChecksumService();
            _fileOperationService = new FileOperationService(
                encryptionService,
                compressionService,
                checksumService,
                useCompression: true,
                _logger);
            _versionService = new VersionService(_fileOperationService, _logger);

            // Create test files
            _testFilePath = Path.GetTempFileName();
            _testFileV2Path = Path.GetTempFileName();
            File.WriteAllText(_testFilePath, "Original content");
            File.WriteAllText(_testFileV2Path, "Updated content");
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
        public void CreateVersion_WithValidInput_CreatesNewVersion()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);

            // Act
            var versionEntry = _versionService.CreateVersion(baseEntry, _testFileV2Path, "Updated version");

            // Assert
            Assert.NotNull(versionEntry);
            Assert.Equal(2, versionEntry.Version);
            Assert.Equal(baseEntry.Id, versionEntry.BaseFileId);
            Assert.Equal("Updated version", versionEntry.VersionComment);
            Assert.Contains(versionEntry.Id, baseEntry.VersionIds);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("INFO: Creating new version"));
        }

        [Fact]
        public void CreateVersion_WithNullBaseEntry_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _versionService.CreateVersion(null!, _testFileV2Path));
        }

        [Fact]
        public void CreateVersion_WithNullFilePath_ThrowsArgumentNullException()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _versionService.CreateVersion(baseEntry, null!));
        }

        [Fact]
        public void CreateVersion_FromVersion_ThrowsInvalidOperationException()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);
            var version2 = _versionService.CreateVersion(baseEntry, _testFileV2Path);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                _versionService.CreateVersion(version2, _testFileV2Path));
            Assert.Contains(_logger.LogMessages, m => m.Contains("Cannot create a version from another version"));
        }

        [Fact]
        public void GetVersionHistory_ReturnsAllVersionsInOrder()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);
            var version2 = _versionService.CreateVersion(baseEntry, _testFileV2Path, "Version 2");
            var version3 = _versionService.CreateVersion(baseEntry, _testFileV2Path, "Version 3");

            // Act
            var history = _versionService.GetVersionHistory(baseEntry);

            // Assert
            Assert.Equal(3, history.Count);
            Assert.Equal(1, history[0].Version);
            Assert.Equal(2, history[1].Version);
            Assert.Equal(3, history[2].Version);
        }

        [Fact]
        public void GetVersion_WithValidVersion_ReturnsCorrectVersion()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);
            var version2 = _versionService.CreateVersion(baseEntry, _testFileV2Path, "Version 2");

            // Act
            var retrievedVersion = _versionService.GetVersion(baseEntry, 2);

            // Assert
            Assert.Equal(version2.Id, retrievedVersion.Id);
            Assert.Equal(2, retrievedVersion.Version);
            Assert.Equal("Version 2", retrievedVersion.VersionComment);
        }

        [Fact]
        public void GetVersion_WithInvalidVersion_ThrowsFileNotFoundException()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => 
                _versionService.GetVersion(baseEntry, 999));
        }

        [Fact]
        public void GetLatestVersion_ReturnsNewestVersion()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);
            _versionService.CreateVersion(baseEntry, _testFileV2Path, "Version 2");
            var version3 = _versionService.CreateVersion(baseEntry, _testFileV2Path, "Version 3");

            // Act
            var latestVersion = _versionService.GetLatestVersion(baseEntry);

            // Assert
            Assert.Equal(version3.Id, latestVersion.Id);
            Assert.Equal(3, latestVersion.Version);
            Assert.Equal("Version 3", latestVersion.VersionComment);
        }

        [Fact]
        public void GetVersionContent_ReturnsCorrectContent()
        {
            // Arrange
            var baseEntry = _fileOperationService.SaveFile(_testFilePath);
            _versionService.CreateVersion(baseEntry, _testFileV2Path, "Version 2");

            // Act
            var content = _versionService.GetVersionContent(baseEntry, 2);

            // Assert
            Assert.NotNull(content);
            Assert.Equal("Updated content", Encoding.UTF8.GetString(content));
        }

        public void Dispose()
        {
            // Cleanup test files
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
            if (File.Exists(_testFileV2Path))
                File.Delete(_testFileV2Path);
        }
    }
}
