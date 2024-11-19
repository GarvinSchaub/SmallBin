namespace SmallBin.UnitTests;

using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

public class FileEntryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var entry = new FileEntry();

        // Assert
        Assert.NotNull(entry.Id);
        Assert.NotEqual(Guid.Empty.ToString(), entry.Id);
        Assert.Null(entry.FileName);
        Assert.NotNull(entry.Tags);
        Assert.Empty(entry.Tags);
        Assert.Equal(default, entry.CreatedOn);
        Assert.Equal(default, entry.UpdatedOn);
        Assert.Equal(0, entry.FileSize);
        Assert.Null(entry.ContentType);
        Assert.False(entry.IsCompressed);
        Assert.NotNull(entry.CustomMetadata);
        Assert.Empty(entry.CustomMetadata);
        Assert.Null(entry.EncryptedContent);
        Assert.Null(entry.IV);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        // Arrange
        var entries = new List<FileEntry>();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            entries.Add(new FileEntry());
        }

        // Assert
        var uniqueIds = entries.Select(e => e.Id).Distinct();
        Assert.Equal(entries.Count, uniqueIds.Count());
    }

    #endregion

    #region Id Property Tests

    [Fact]
    public void Id_SettingNewValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var newId = Guid.NewGuid().ToString();

        // Act
        entry.Id = newId;

        // Assert
        Assert.Equal(newId, entry.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Id_SettingNullOrEmptyValue_ShouldBeAllowed(string id)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.Id = id;

        // Assert
        Assert.Equal(id, entry.Id);
    }

    #endregion

    #region FileName Property Tests

    [Theory]
    [InlineData("test.txt")]
    [InlineData("документ.pdf")]
    [InlineData("文件.doc")]
    [InlineData("file with spaces.jpg")]
    [InlineData("very-long-file-name-with-many-characters-and-symbols_123456789.extension")]
    public void FileName_SettingValidNames_ShouldUpdateCorrectly(string fileName)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.FileName = fileName;

        // Assert
        Assert.Equal(fileName, entry.FileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FileName_SettingNullOrEmptyValue_ShouldBeAllowed(string fileName)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.FileName = fileName;

        // Assert
        Assert.Equal(fileName, entry.FileName);
    }

    #endregion

    #region Tags Property Tests

    [Fact]
    public void Tags_AddingNewTags_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var tags = new List<string> { "tag1", "tag2", "tag3" };

        // Act
        entry.Tags = tags;

        // Assert
        Assert.Equal(tags.Count, entry.Tags.Count);
        Assert.Equal(tags, entry.Tags);
    }

    [Fact]
    public void Tags_ModifyingExistingList_ShouldReflectChanges()
    {
        // Arrange
        var entry = new FileEntry();
        entry.Tags.Add("tag1");

        // Act
        entry.Tags.Add("tag2");
        entry.Tags.Remove("tag1");

        // Assert
        Assert.Single(entry.Tags);
        Assert.Equal("tag2", entry.Tags[0]);
    }

    [Fact]
    public void Tags_SettingNull_ShouldBeAllowed()
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.Tags = null;

        // Assert
        Assert.Null(entry.Tags);
    }

    #endregion

    #region DateTime Property Tests

    [Fact]
    public void CreatedOn_SettingValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var date = DateTime.UtcNow;

        // Act
        entry.CreatedOn = date;

        // Assert
        Assert.Equal(date, entry.CreatedOn);
    }

    [Fact]
    public void UpdatedOn_SettingValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var date = DateTime.UtcNow;

        // Act
        entry.UpdatedOn = date;

        // Assert
        Assert.Equal(date, entry.UpdatedOn);
    }

    #endregion

    #region FileSize Property Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void FileSize_SettingValidValues_ShouldUpdateCorrectly(long size)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.FileSize = size;

        // Assert
        Assert.Equal(size, entry.FileSize);
    }

    [Fact]
    public void FileSize_SettingNegativeValue_ShouldBeAllowed()
    {
        // Arrange
        var entry = new FileEntry();
        var size = -100L;

        // Act
        entry.FileSize = size;

        // Assert
        Assert.Equal(size, entry.FileSize);
    }

    #endregion

    #region ContentType Property Tests

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/jpeg")]
    [InlineData("application/pdf")]
    [InlineData("application/octet-stream")]
    public void ContentType_SettingValidMimeTypes_ShouldUpdateCorrectly(string contentType)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.ContentType = contentType;

        // Assert
        Assert.Equal(contentType, entry.ContentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ContentType_SettingNullOrEmptyValue_ShouldBeAllowed(string contentType)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.ContentType = contentType;

        // Assert
        Assert.Equal(contentType, entry.ContentType);
    }

    #endregion

    #region IsCompressed Property Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsCompressed_SettingValue_ShouldUpdateCorrectly(bool isCompressed)
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.IsCompressed = isCompressed;

        // Assert
        Assert.Equal(isCompressed, entry.IsCompressed);
    }

    #endregion

    #region CustomMetadata Property Tests

    [Fact]
    public void CustomMetadata_AddingNewEntries_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        entry.CustomMetadata = metadata;

        // Assert
        Assert.Equal(metadata.Count, entry.CustomMetadata.Count);
        Assert.Equal(metadata, entry.CustomMetadata);
    }

    [Fact]
    public void CustomMetadata_ModifyingExistingDictionary_ShouldReflectChanges()
    {
        // Arrange
        var entry = new FileEntry();
        entry.CustomMetadata.Add("key1", "value1");

        // Act
        entry.CustomMetadata.Add("key2", "value2");
        entry.CustomMetadata.Remove("key1");

        // Assert
        Assert.Single(entry.CustomMetadata);
        Assert.Equal("value2", entry.CustomMetadata["key2"]);
    }

    [Fact]
    public void CustomMetadata_SettingNull_ShouldBeAllowed()
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.CustomMetadata = null;

        // Assert
        Assert.Null(entry.CustomMetadata);
    }

    #endregion

    #region EncryptedContent Property Tests

    [Fact]
    public void EncryptedContent_SettingValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var content = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        entry.EncryptedContent = content;

        // Assert
        Assert.Equal(content, entry.EncryptedContent);
    }

    [Fact]
    public void EncryptedContent_SettingNull_ShouldBeAllowed()
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.EncryptedContent = null;

        // Assert
        Assert.Null(entry.EncryptedContent);
    }

    #endregion

    #region IV Property Tests

    [Fact]
    public void IV_SettingValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var entry = new FileEntry();
        var iv = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        entry.IV = iv;

        // Assert
        Assert.Equal(iv, entry.IV);
    }

    [Fact]
    public void IV_SettingNull_ShouldBeAllowed()
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.IV = null;

        // Assert
        Assert.Null(entry.IV);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FileEntry_SettingAllProperties_ShouldMaintainConsistency()
    {
        // Arrange
        var entry = new FileEntry();
        var id = Guid.NewGuid().ToString();
        var fileName = "test.txt";
        var tags = new List<string> { "tag1", "tag2" };
        var createdOn = DateTime.UtcNow;
        var updatedOn = DateTime.UtcNow.AddHours(1);
        var fileSize = 1024L;
        var contentType = "text/plain";
        var isCompressed = true;
        var metadata = new Dictionary<string, string> { { "key", "value" } };
        var content = new byte[] { 1, 2, 3 };
        var iv = new byte[] { 4, 5, 6 };

        // Act
        entry.Id = id;
        entry.FileName = fileName;
        entry.Tags = tags;
        entry.CreatedOn = createdOn;
        entry.UpdatedOn = updatedOn;
        entry.FileSize = fileSize;
        entry.ContentType = contentType;
        entry.IsCompressed = isCompressed;
        entry.CustomMetadata = metadata;
        entry.EncryptedContent = content;
        entry.IV = iv;

        // Assert
        Assert.Equal(id, entry.Id);
        Assert.Equal(fileName, entry.FileName);
        Assert.Equal(tags, entry.Tags);
        Assert.Equal(createdOn, entry.CreatedOn);
        Assert.Equal(updatedOn, entry.UpdatedOn);
        Assert.Equal(fileSize, entry.FileSize);
        Assert.Equal(contentType, entry.ContentType);
        Assert.Equal(isCompressed, entry.IsCompressed);
        Assert.Equal(metadata, entry.CustomMetadata);
        Assert.Equal(content, entry.EncryptedContent);
        Assert.Equal(iv, entry.IV);
    }

    #endregion
}