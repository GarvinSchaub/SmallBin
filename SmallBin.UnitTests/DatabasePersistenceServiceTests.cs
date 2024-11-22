using System.Security.Cryptography;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;
using SmallBin.Services;

namespace SmallBin.UnitTests
{
    public class DatabasePersistenceServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _tempPath;
        private readonly string _backupPath;
        private readonly string _oldBackupPath;
        private readonly TestLogger _logger;
        private readonly EncryptionService _encryptionService;
        private readonly DatabasePersistenceService _persistenceService;
        private readonly byte[] _key;

        public DatabasePersistenceServiceTests()
        {
            _dbPath = Path.GetTempFileName();
            _tempPath = $"{_dbPath}.tmp";
            _backupPath = $"{_dbPath}.bak";
            _oldBackupPath = $"{_dbPath}.bak.old";
            
            _logger = new TestLogger();
            _key = new byte[32];
            RandomNumberGenerator.Fill(_key);
            _encryptionService = new EncryptionService(_key);
            _persistenceService = new DatabasePersistenceService(_dbPath, _encryptionService, _logger);
        }

        public void Dispose()
        {
            foreach (var path in new[] { _dbPath, _tempPath, _backupPath, _oldBackupPath })
            {
                if (File.Exists(path))
                {
                    // Reset any read-only attributes before deleting
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
        }

        private class TestLogger : ILogger
        {
            public List<string> LogMessages { get; } = new();
            public List<Exception> LoggedExceptions { get; } = new();

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

        private DatabaseContent CreateTestDatabase(string version = "1.0")
        {
            var entry1 = new FileEntry
            {
                FileName = "test1.txt",
                ContentType = "text/plain",
                FileSize = 100,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };

            var entry2 = new FileEntry
            {
                FileName = "test2.pdf",
                ContentType = "application/pdf",
                FileSize = 200,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };

            return new DatabaseContent
            {
                Version = version,
                Files = new Dictionary<string, FileEntry>
                {
                    { entry1.Id, entry1 },
                    { entry2.Id, entry2 }
                }
            };
        }

        private bool WaitForFile(string path, bool shouldExist, int timeoutMs = 1000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (File.Exists(path) == shouldExist)
                    return true;
                Thread.Sleep(100);
            }
            return false;
        }

        private bool VerifyFileContent(string path, string expectedVersion)
        {
            try
            {
                var service = new DatabasePersistenceService(path, _encryptionService);
                var content = service.Load();
                return content.Version == expectedVersion;
            }
            catch
            {
                return false;
            }
        }

        [Fact]
        public void Constructor_WithNullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new DatabasePersistenceService(null, _encryptionService));
        }

        [Fact]
        public void Constructor_WithNullEncryptionService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new DatabasePersistenceService(_dbPath, null));
        }

        [Fact]
        public void SaveAndLoad_WithValidDatabase_WorksCorrectly()
        {
            // Arrange
            var database = CreateTestDatabase();
            var fileIds = new List<string>(database.Files.Keys);

            // Act
            _persistenceService.Save(database);
            var loaded = _persistenceService.Load();

            // Assert
            Assert.Equal(database.Files.Count, loaded.Files.Count);
            Assert.Equal(database.Files[fileIds[0]].FileName, loaded.Files[fileIds[0]].FileName);
            Assert.Equal(database.Files[fileIds[1]].FileName, loaded.Files[fileIds[1]].FileName);
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("INFO: Database saved successfully"));
            Assert.Contains(_logger.LogMessages, m => m.StartsWith($"INFO: Database loaded successfully"));
        }

        [Fact]
        public void Load_WithCorruptFile_ThrowsDatabaseCorruptException()
        {
            // Arrange
            File.WriteAllText(_dbPath, "corrupt data");

            // Act & Assert
            Assert.Throws<DatabaseCorruptException>(() => _persistenceService.Load());
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("ERROR: Database file is corrupt"));
        }

        [Fact]
        public void Load_WithNonexistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            File.Delete(_dbPath);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => _persistenceService.Load());
        }

        [Fact]
        public void Save_CreatesBackupOfExistingFile()
        {
            // Arrange
            var database = CreateTestDatabase("1.0");
            _persistenceService.Save(database);
            Assert.True(WaitForFile(_dbPath, true), "Initial file should be created");
            Assert.True(VerifyFileContent(_dbPath, "1.0"), "Initial file should have correct content");

            // Act
            database.Version = "1.1";
            _persistenceService.Save(database);

            // Assert
            Assert.True(WaitForFile(_backupPath, true), "Backup file should be created");
            Assert.True(VerifyFileContent(_backupPath, "1.0"), "Backup should contain original content");
            Assert.True(VerifyFileContent(_dbPath, "1.1"), "Main file should have updated content");
            Assert.Contains(_logger.LogMessages, m => m.StartsWith("DEBUG: Creating backup"));
        }

        [Fact]
        public void Save_CleansUpTemporaryFiles()
        {
            // Arrange
            var database = CreateTestDatabase();

            // Act
            _persistenceService.Save(database);

            // Assert
            Assert.True(WaitForFile(_tempPath, false), "Temporary file should be cleaned up");
        }
    }
}
