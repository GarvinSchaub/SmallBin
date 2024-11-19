namespace SmallBin.UnitTests;

using Xunit;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;

public class SecureFileDatabaseTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testPassword;
    private readonly string _testFilesDir;

    public SecureFileDatabaseTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.sdb");
        _testPassword = "TestPassword123!";
        _testFilesDir = Path.Combine(Path.GetTempPath(), $"test_files_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testFilesDir);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
        if (File.Exists(_testDbPath + ".tmp"))
            File.Delete(_testDbPath + ".tmp");
        if (File.Exists(_testDbPath + ".bak"))
            File.Delete(_testDbPath + ".bak");
        if (Directory.Exists(_testFilesDir))
            Directory.Delete(_testFilesDir, true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateNewDatabase()
    {
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        Assert.True(File.Exists(_testDbPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidDbPath_ShouldThrowArgumentNullException(string dbPath)
    {
        Assert.Throws<ArgumentNullException>(() => new SecureFileDatabase(dbPath, _testPassword));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidPassword_ShouldThrowArgumentNullException(string password)
    {
        Assert.Throws<ArgumentNullException>(() => new SecureFileDatabase(_testDbPath, password));
    }

    [Fact]
    public void Constructor_WithNonexistentDirectory_ShouldCreateDirectory()
    {
        var deepPath = Path.Combine(_testFilesDir, "deep", "deeper", "database.sdb");
        using var db = new SecureFileDatabase(deepPath, _testPassword);
        Assert.True(Directory.Exists(Path.GetDirectoryName(deepPath)));
    }

    #endregion

    #region SaveFile Tests

    [Fact]
    public void SaveFile_WithValidFile_ShouldStoreFileInDatabase()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);

        // Act
        db.SaveFile(testFilePath);
        db.Save();

        // Assert
        var dbContent = GetDatabaseContent();
        Assert.Single(dbContent.Files);
        Assert.Equal("test.txt", dbContent.Files.First().Value.FileName);
    }

    [Fact]
    public void SaveFile_WithTags_ShouldStoreTags()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        var tags = new List<string> { "tag1", "tag2" };
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);

        // Act
        db.SaveFile(testFilePath, tags);
        db.Save();

        // Assert
        var dbContent = GetDatabaseContent();
        Assert.Equal(tags, dbContent.Files.First().Value.Tags);
    }

    [Fact]
    public void SaveFile_WithContentType_ShouldStoreContentType()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        const string contentType = "text/plain";
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);

        // Act
        db.SaveFile(testFilePath, contentType: contentType);
        db.Save();

        // Assert
        var dbContent = GetDatabaseContent();
        Assert.Equal(contentType, dbContent.Files.First().Value.ContentType);
    }

    [Fact]
    public void SaveFile_WithNonexistentFile_ShouldThrowFileNotFoundException()
    {
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        Assert.Throws<FileNotFoundException>(() => db.SaveFile("nonexistent.txt"));
    }

    [Fact]
    public void SaveFile_WithCompression_ShouldCompressContent()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", new string('a', 1000));
        using var db = new SecureFileDatabase(_testDbPath, _testPassword, useCompression: true);

        // Act
        db.SaveFile(testFilePath);
        db.Save();

        // Assert
        var dbContent = GetDatabaseContent();
        var entry = dbContent.Files.First().Value;
        Assert.True(entry.IsCompressed);
    }

    #endregion

    #region GetFile Tests

    [Fact]
    public void GetFile_WithValidId_ShouldReturnOriginalContent()
    {
        // Arrange
        var content = "Test content";
        var testFilePath = CreateTestFile("test.txt", content);
        string fileId;
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using var db2 = new SecureFileDatabase(_testDbPath, _testPassword);
        var retrievedContent = db2.GetFile(fileId);

        // Assert
        Assert.Equal(content, Encoding.UTF8.GetString(retrievedContent));
    }

    [Fact]
    public void GetFile_WithInvalidId_ShouldThrowKeyNotFoundException()
    {
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        Assert.Throws<KeyNotFoundException>(() => db.GetFile("invalid-id"));
    }

    [Fact]
    public void GetFile_WithCompressedContent_ShouldDecompressCorrectly()
    {
        // Arrange
        var content = new string('a', 1000);
        var testFilePath = CreateTestFile("test.txt", content);
        string fileId;
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword, useCompression: true))
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using var db2 = new SecureFileDatabase(_testDbPath, _testPassword);
        var retrievedContent = db2.GetFile(fileId);

        // Assert
        Assert.Equal(content, Encoding.UTF8.GetString(retrievedContent));
    }

    #endregion

    #region DeleteFile Tests

    [Fact]
    public void DeleteFile_WithValidId_ShouldRemoveFile()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        string fileId;
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.DeleteFile(fileId);
        }

        // Assert
        var dbContent = GetDatabaseContent();
        Assert.Empty(dbContent.Files);
    }

    [Fact]
    public void DeleteFile_WithInvalidId_ShouldThrowKeyNotFoundException()
    {
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        Assert.Throws<KeyNotFoundException>(() => db.DeleteFile("invalid-id"));
    }

    #endregion

    #region UpdateMetadata Tests

    [Fact]
    public void UpdateMetadata_WithValidId_ShouldUpdateEntry()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        string fileId;
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.UpdateMetadata(fileId, entry =>
            {
                entry.Tags.Add("new-tag");
                entry.CustomMetadata["key"] = "value";
            });
        }

        // Assert
        var dbContent = GetDatabaseContent();
        var updatedEntry = dbContent.Files[fileId];
        Assert.Contains("new-tag", updatedEntry.Tags);
        Assert.Equal("value", updatedEntry.CustomMetadata["key"]);
    }

    [Fact]
    public void UpdateMetadata_WithInvalidId_ShouldThrowKeyNotFoundException()
    {
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        Assert.Throws<KeyNotFoundException>(() => 
            db.UpdateMetadata("invalid-id", _ => { }));
    }

    #endregion

    #region Search Tests

    [Fact]
    public void Search_WithFileName_ShouldReturnMatchingEntries()
    {
        // Arrange
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        db.SaveFile(CreateTestFile("test1.txt", "content"));
        db.SaveFile(CreateTestFile("test2.txt", "content"));
        db.SaveFile(CreateTestFile("other.txt", "content"));

        // Act
        var results = db.Search(new SearchCriteria { FileName = "test" });

        // Assert
        Assert.Equal(2, results.Count());
    }

    [Fact]
    public void Search_WithNullCriteria_ShouldReturnAllEntries()
    {
        // Arrange
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        db.SaveFile(CreateTestFile("test1.txt", "content"));
        db.SaveFile(CreateTestFile("test2.txt", "content"));

        // Act
        var results = db.Search(null!);

        // Assert
        Assert.Equal(2, results.Count());
    }

    #endregion

    #region Save and Load Tests

    [Fact]
    public void Save_WithModifications_ShouldPersistChanges()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.SaveFile(testFilePath);
        }

        // Act & Assert
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            var results = db.Search(new SearchCriteria { FileName = "test.txt" });
            Assert.Single(results);
        }
    }

    [Fact]
    public void Save_WithNoModifications_ShouldNotCreateBackup()
    {
        // Arrange
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            // Do nothing - no modifications
        }

        // Assert
        Assert.False(File.Exists(_testDbPath + ".bak"));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WithUnsavedChanges_ShouldSaveDatabase()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        
        // Act
        using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
        {
            db.SaveFile(testFilePath);
            // Dispose will be called automatically
        }

        // Assert
        Assert.True(File.Exists(_testDbPath));
        var dbContent = GetDatabaseContent();
        Assert.Single(dbContent.Files);
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
        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        return new DatabaseContent
        {
            Files = db.Search(null).ToDictionary(f => f.Id)
        };
    }

    #endregion
    
        #region Database Corruption Tests

    [Fact]
    public void LoadDatabase_WithCorruptFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        File.WriteAllBytes(_testDbPath, new byte[8]); // Create a file that's too small

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new SecureFileDatabase(_testDbPath, _testPassword));
    }

    [Fact]
    public void LoadDatabase_WithEmptyFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        File.WriteAllBytes(_testDbPath, Array.Empty<byte>());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new SecureFileDatabase(_testDbPath, _testPassword));
    }

    #endregion

    #region Save Error Handling Tests

    // [Fact]
    // public void Save_WhenSaveOperationFails_ShouldRestoreFromBackup()
    // {
    //     // Arrange
    //     var testFilePath = CreateTestFile("test.txt", "Test content");
    //     byte[] originalContent;
    //
    //     // Setup initial database state
    //     using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
    //     {
    //         db.SaveFile(testFilePath);
    //         db.Save();
    //         originalContent = File.ReadAllBytes(_testDbPath);
    //     }
    //
    //     // Create a new instance for testing save failure
    //     using (var db = new SecureFileDatabase(_testDbPath, _testPassword))
    //     {
    //         // Add a new file to make the database dirty
    //         db.SaveFile(CreateTestFile("another.txt", "content"));
    //
    //         // Force a save operation that will create a backup
    //         db.Save();
    //
    //         // Verify backup was created
    //         Assert.True(File.Exists(_testDbPath + ".bak"));
    //
    //         // Delete original database file to simulate partial save failure
    //         File.Delete(_testDbPath);
    //
    //         // Verify backup is restored
    //         Assert.True(File.Exists(_testDbPath + ".bak"));
    //         
    //         // The next save operation should restore from backup
    //         db.Save();
    //
    //         // Assert
    //         Assert.True(File.Exists(_testDbPath), "Database file should exist");
    //         Assert.False(File.Exists(_testDbPath + ".tmp"), "Temporary file should be cleaned up");
    //         Assert.False(File.Exists(_testDbPath + ".bak"), "Backup file should be cleaned up");
    //         
    //         // Verify content length matches original
    //         var restoredContent = File.ReadAllBytes(_testDbPath);
    //         Assert.Equal(originalContent.Length, restoredContent.Length);
    //     }
    // }

    [Fact]
    public void Save_WithSuccessfulOperation_ShouldCleanupTemporaryFiles()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");

        using var db = new SecureFileDatabase(_testDbPath, _testPassword);
        db.SaveFile(testFilePath);

        // Act
        db.Save();

        // Assert
        Assert.True(File.Exists(_testDbPath), "Database file should exist");
        Assert.False(File.Exists(_testDbPath + ".tmp"), "Temporary file should not exist");
        Assert.False(File.Exists(_testDbPath + ".bak"), "Backup file should not exist");
    }

    #endregion
}