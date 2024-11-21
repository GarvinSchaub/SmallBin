﻿namespace SmallBin.UnitTests;

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
        if (File.Exists(_testDbPath + ".bak.old"))
            File.Delete(_testDbPath + ".bak.old");
        if (Directory.Exists(_testFilesDir))
            Directory.Delete(_testFilesDir, true);
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

        // Verify file was saved
        Assert.True(File.Exists(_testDbPath));
        var dbContent = GetDatabaseContent();
        Assert.Single(dbContent.Files);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNonexistentDirectory_ShouldCreateDirectory()
    {
        var deepPath = Path.Combine(_testFilesDir, "deep", "deeper", "database.sdb");
        using var db = SecureFileDatabase.Create(deepPath, _testPassword).Build();
        Assert.True(Directory.Exists(Path.GetDirectoryName(deepPath)));
    }

    #endregion

    #region SaveFile Tests

    [Fact]
    public void SaveFile_WithValidFile_ShouldStoreFileInDatabase()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

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
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

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
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

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
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
        Assert.Throws<FileNotFoundException>(() => db.SaveFile("nonexistent.txt"));
    }

    [Fact]
    public void SaveFile_WithCompression_ShouldCompressContent()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", new string('a', 1000));
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();

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
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using var db2 = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
        var retrievedContent = db2.GetFile(fileId);

        // Assert
        Assert.Equal(content, Encoding.UTF8.GetString(retrievedContent));
    }

    [Fact]
    public void GetFile_WithInvalidId_ShouldThrowKeyNotFoundException()
    {
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
        Assert.Throws<KeyNotFoundException>(() => db.GetFile("invalid-id"));
    }

    [Fact]
    public void GetFile_WithCompressedContent_ShouldDecompressCorrectly()
    {
        // Arrange
        var content = new string('a', 1000);
        var testFilePath = CreateTestFile("test.txt", content);
        string fileId;
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using var db2 = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
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
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
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
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
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
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(testFilePath);
            fileId = db.Search(new SearchCriteria { FileName = "test.txt" }).First().Id;
        }

        // Act
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
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
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
        Assert.Throws<KeyNotFoundException>(() => 
            db.UpdateMetadata("invalid-id", _ => { }));
    }

    #endregion

    #region Search Tests

    [Fact]
    public void Search_WithFileName_ShouldReturnMatchingEntries()
    {
        // Arrange
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
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
        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
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
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(testFilePath);
        }

        // Act & Assert
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            var results = db.Search(new SearchCriteria { FileName = "test.txt" });
            Assert.Single(results);
        }
    }

    [Fact]
    public void Save_WithNoModifications_ShouldNotCreateBackup()
    {
        // Arrange
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            // Do nothing - no modifications
        }

        // Assert
        Assert.False(File.Exists(_testDbPath + ".bak"));
    }

    [Fact]
    public void Save_WithModifications_ShouldCreateBackup()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Initial content");
        
        // First save to create initial database
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(testFilePath);
            db.Save();
        }

        // Second save to test backup creation
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
        {
            db.SaveFile(CreateTestFile("test2.txt", "More content"));
            db.Save();
        }

        // Assert
        Assert.True(File.Exists(_testDbPath + ".bak"), "Backup file should exist after save with modifications");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WithUnsavedChanges_ShouldSaveDatabase()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");
        
        // Act
        using (var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build())
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

    #region Database Corruption Tests

    [Fact]
    public void LoadDatabase_WithCorruptFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        File.WriteAllBytes(_testDbPath, new byte[8]); // Create a file that's too small

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            SecureFileDatabase.Create(_testDbPath, _testPassword).Build());
    }

    [Fact]
    public void LoadDatabase_WithEmptyFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        File.WriteAllBytes(_testDbPath, Array.Empty<byte>());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            SecureFileDatabase.Create(_testDbPath, _testPassword).Build());
    }

    #endregion

    #region Save Error Handling Tests

    [Fact]
    public void Save_WithSuccessfulOperation_ShouldCleanupTemporaryFiles()
    {
        // Arrange
        var testFilePath = CreateTestFile("test.txt", "Test content");

        using var db = SecureFileDatabase.Create(_testDbPath, _testPassword).Build();
        db.SaveFile(testFilePath);

        // Act
        db.Save();

        // Assert
        Assert.True(File.Exists(_testDbPath), "Database file should exist");
        Assert.False(File.Exists(_testDbPath + ".tmp"), "Temporary file should not exist");
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
