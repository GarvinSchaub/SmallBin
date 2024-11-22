using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using SmallBin.Core;
using SmallBin.Models;
using SmallBin.Exceptions;
using Xunit;

namespace SmallBin.UnitTests
{
    public class SecureFileDatabaseTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _testPassword;
        private readonly string _testFilesDir;
        private FileStream? _lockStream;

        public SecureFileDatabaseTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.sdb");
            _testPassword = "TestPassword123!";  // Ensure it meets minimum length requirement
            _testFilesDir = Path.Combine(Path.GetTempPath(), $"test_files_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testFilesDir);
        }

        public void Dispose()
        {
            try
            {
                _lockStream?.Dispose();
                _lockStream = null;

                if (File.Exists(_testDbPath))
                    File.Delete(_testDbPath);
                if (File.Exists(_testDbPath + ".tmp"))
                    File.Delete(_testDbPath + ".tmp");
                if (File.Exists(_testDbPath + ".bak"))
                    File.Delete(_testDbPath + ".bak");
                if (File.Exists(_testDbPath + ".bak.old"))
                    File.Delete(_testDbPath + ".bak.old");

                if (Directory.Exists(_testFilesDir))
                {
                    Directory.Delete(_testFilesDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void LockDirectory(string path)
        {
            // Create a file and keep it open to prevent modifications
            var lockFile = Path.Combine(path, ".lock");
            _lockStream = new FileStream(lockFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        }

        #region Builder Tests

        [Fact]
        public void Create_WithValidParameters_ShouldReturnDatabaseBuilder()
        {
            var builder = SecureFileDatabase.Create(_testDbPath, _testPassword);
            Assert.NotNull(builder);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_WithInvalidDbPath_ShouldThrowArgumentNullException(string dbPath)
        {
            Assert.Throws<ArgumentNullException>(() => SecureFileDatabase.Create(dbPath, _testPassword));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_WithInvalidPassword_ShouldThrowArgumentNullException(string password)
        {
            Assert.Throws<ArgumentNullException>(() => SecureFileDatabase.Create(_testDbPath, password));
        }

        [Theory]
        [InlineData("short")]
        [InlineData("1234567")]
        public void Create_WithShortPassword_ShouldThrowArgumentException(string password)
        {
            Assert.Throws<ArgumentException>(() => 
                SecureFileDatabase.Create(_testDbPath, password).Build());
        }

        [Fact]
        public void Build_WithDefaultOptions_ShouldCreateDatabase()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            Assert.True(File.Exists(_testDbPath));
        }

        [Fact]
        public void Build_WithCompressionDisabled_ShouldNotCompressFiles()
        {
            var testFilePath = CreateTestFile("test.txt", new string('a', 1000));
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword)
                .WithoutCompression()
                .Build();

            db.SaveFile(testFilePath);
            db.Save();

            var dbContent = GetDatabaseContent();
            var entry = dbContent.Files.First().Value;
            Assert.False(entry.IsCompressed);
        }

        [Fact]
        public void Build_WithAutoSaveEnabled_ShouldSaveAutomatically()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword)
                .WithAutoSave()
                .Build())
            {
                db.SaveFile(testFilePath);
                // No explicit Save() call
            }

            Assert.True(File.Exists(_testDbPath));
            var dbContent = GetDatabaseContent();
            Assert.Single(dbContent.Files);
        }

        #endregion

        #region SaveFile Tests

        [Fact]
        public void SaveFile_WithValidFile_ShouldStoreFileInDatabase()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

            db.SaveFile(testFilePath);
            db.Save();

            var dbContent = GetDatabaseContent();
            Assert.Single(dbContent.Files);
            Assert.Equal("test.txt", dbContent.Files.First().Value.FileName);
        }

        [Fact]
        public void SaveFile_WithEmptyFile_ShouldThrowFileValidationException()
        {
            var testFilePath = CreateTestFile("empty.txt", "");
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

            var ex = Assert.Throws<FileValidationException>(() => db.SaveFile(testFilePath));
            Assert.Contains("empty file", ex.Message);
        }

        [Fact]
        public void SaveFile_WithTags_ShouldStoreTags()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            var tags = new List<string> { "tag1", "tag2" };
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

            db.SaveFile(testFilePath, tags);
            db.Save();

            var dbContent = GetDatabaseContent();
            Assert.Equal(tags, dbContent.Files.First().Value.Tags);
        }

        [Fact]
        public void SaveFile_WithContentType_ShouldStoreContentType()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            const string contentType = "text/plain";
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

            db.SaveFile(testFilePath, contentType: contentType);
            db.Save();

            var dbContent = GetDatabaseContent();
            Assert.Equal(contentType, dbContent.Files.First().Value.ContentType);
        }

        [Fact]
        public void SaveFile_WithNonexistentFile_ShouldThrowFileNotFoundException()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            Assert.Throws<FileNotFoundException>(() => db.SaveFile("nonexistent.txt"));
        }

        [Fact]
        public void SaveFile_WithCompression_ShouldCompressContent()
        {
            var testFilePath = CreateTestFile("test.txt", new string('a', 1000));
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

            db.SaveFile(testFilePath);
            db.Save();

            var dbContent = GetDatabaseContent();
            var entry = dbContent.Files.First().Value;
            Assert.True(entry.IsCompressed);
        }

        #endregion

        #region GetFile Tests

        [Fact]
        public void GetFile_WithValidId_ShouldReturnOriginalContent()
        {
            var content = "Test content";
            var testFilePath = CreateTestFile("test.txt", content);
            string fileId;
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.SaveFile(testFilePath);
                fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
            }

            using var db2 = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            var retrievedContent = db2.GetFile(fileId);

            Assert.Equal(content, Encoding.UTF8.GetString(retrievedContent));
        }

        [Fact]
        public void GetFile_WithInvalidId_ShouldThrowKeyNotFoundException()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            Assert.Throws<KeyNotFoundException>(() => db.GetFile("invalid-id"));
        }

        [Fact]
        public void GetFile_WithCorruptCompressedData_ShouldThrowDatabaseCorruptException()
        {
            // First save a valid compressed file
            var content = new string('a', 1000);
            var testFilePath = CreateTestFile("test.txt", content);
            string fileId;
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.SaveFile(testFilePath);
                fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
                db.Save();
            }

            // Load the database and get the file entry
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                var entry = db.Search(new SearchCriteria { FileName = "test.txt" }).First();
                Assert.True(entry.IsCompressed); // Verify file is compressed

                // Corrupt the compressed data
                var corruptedContent = entry.EncryptedContent;
                corruptedContent[^10] = (byte)(corruptedContent[^10] ^ 0xFF); // Flip some bits

                // Save the corrupted data back
                db.UpdateMetadata(entry.Id, e => e.EncryptedContent = corruptedContent);
                db.Save();
            }

            // Try to get the file with corrupted compressed data
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                var ex = Assert.ThrowsAny<Exception>(() => db.GetFile(fileId));
                Assert.True(ex is DatabaseCorruptException || ex is DatabaseEncryptionException,
                          "Expected either DatabaseCorruptException or DatabaseEncryptionException");
            }
        }

        #endregion

        #region DeleteFile Tests

        [Fact]
        public void DeleteFile_WithValidId_ShouldRemoveFile()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            string fileId;
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.SaveFile(testFilePath);
                fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
            }

            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.DeleteFile(fileId);
            }

            var dbContent = GetDatabaseContent();
            Assert.Empty(dbContent.Files);
        }

        [Fact]
        public void DeleteFile_WithInvalidId_ShouldThrowKeyNotFoundException()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            Assert.Throws<KeyNotFoundException>(() => db.DeleteFile("invalid-id"));
        }

        #endregion

        #region UpdateMetadata Tests

        [Fact]
        public void UpdateMetadata_WithValidId_ShouldUpdateEntry()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            string fileId;
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.SaveFile(testFilePath);
                fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
            }

            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.UpdateMetadata(fileId, entry =>
                {
                    entry.Tags.Add("new-tag");
                    entry.CustomMetadata["key"] = "value";
                });
            }

            var dbContent = GetDatabaseContent();
            var updatedEntry = dbContent.Files[fileId];
            Assert.Contains("new-tag", updatedEntry.Tags);
            Assert.Equal("value", updatedEntry.CustomMetadata["key"]);
        }

        [Fact]
        public void UpdateMetadata_WithInvalidId_ShouldThrowKeyNotFoundException()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            Assert.Throws<KeyNotFoundException>(() => 
                db.UpdateMetadata("invalid-id", _ => { }));
        }

        [Fact]
        public void UpdateMetadata_WithNullAction_ShouldThrowArgumentNullException()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            Assert.Throws<ArgumentNullException>(() => 
                db.UpdateMetadata("any-id", null));
        }

        #endregion

        #region Search Tests

        [Fact]
        public void Search_WithFileName_ShouldReturnMatchingEntries()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            db.SaveFile(CreateTestFile("test1.txt", "content"));
            db.SaveFile(CreateTestFile("test2.txt", "content"));
            db.SaveFile(CreateTestFile("other.txt", "content"));

            var results = db.Search(new SearchCriteria { FileName = "test" });

            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void Search_WithNullCriteria_ShouldReturnAllEntries()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            db.SaveFile(CreateTestFile("test1.txt", "content"));
            db.SaveFile(CreateTestFile("test2.txt", "content"));

            var results = db.Search(null);

            Assert.Equal(2, results.Count());
        }

        #endregion

        #region Database Corruption Tests

        [Fact]
        public void LoadDatabase_WithCorruptFile_ShouldThrowDatabaseCorruptException()
        {
            File.WriteAllBytes(_testDbPath, new byte[8]); // Create a file that's too small

            var ex = Assert.ThrowsAny<Exception>(() => 
                SecureFileDatabase.Create(_testDbPath, _testPassword).Build());
            Assert.True(ex is DatabaseCorruptException || ex is DatabaseEncryptionException,
                      "Expected either DatabaseCorruptException or DatabaseEncryptionException");
        }

        [Fact]
        public void LoadDatabase_WithInvalidEncryption_ShouldThrowDatabaseEncryptionException()
        {
            // Create database with one password
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.SaveFile(CreateTestFile("test.txt", "content"));
            }

            // Try to open with different password
            var ex = Assert.ThrowsAny<Exception>(() => 
                SecureFileDatabase.Create(_testDbPath, "DifferentPassword123!").Build());
            Assert.True(ex is DatabaseEncryptionException,
                      "Expected DatabaseEncryptionException");
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_WithUnsavedChanges_ShouldSaveDatabase()
        {
            var testFilePath = CreateTestFile("test.txt", "Test content");
            
            using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
            {
                db.SaveFile(testFilePath);
                // Dispose will be called automatically
            }

            Assert.True(File.Exists(_testDbPath));
            var dbContent = GetDatabaseContent();
            Assert.Single(dbContent.Files);
        }

        [Fact]
        public void Operations_AfterDispose_ShouldThrowObjectDisposedException()
        {
            var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            db.Dispose();

            Assert.Throws<ObjectDisposedException>(() => db.SaveFile(CreateTestFile("test.txt", "content")));
            Assert.Throws<ObjectDisposedException>(() => db.GetFile("any-id"));
            Assert.Throws<ObjectDisposedException>(() => db.DeleteFile("any-id"));
            Assert.Throws<ObjectDisposedException>(() => db.Search(null));
            Assert.Throws<ObjectDisposedException>(() => db.UpdateMetadata("any-id", _ => { }));
        }

        #endregion

        #region Helper Methods

        private string CreateTestFile(string fileName, string content)
        {
            var filePath = Path.Combine(_testFilesDir, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private DatabaseContent GetDatabaseContent()
        {
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
            return new DatabaseContent
            {
                Files = db.Search(null).ToDictionary(f => f.Id)
            };
        }

        #endregion
    }
}
