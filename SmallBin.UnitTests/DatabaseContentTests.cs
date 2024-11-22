namespace SmallBin.UnitTests;
using SmallBin.Logging;
using SmallBin.Core;
using SmallBin.Models;
public class DatabaseContentTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithEmptyFilesCollection()
    {
        // Arrange & Act
        var dbContent = new DatabaseContent();

        // Assert
        Assert.NotNull(dbContent.Files);
        Assert.Empty(dbContent.Files);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultVersion()
    {
        // Arrange & Act
        var dbContent = new DatabaseContent();

        // Assert
        Assert.Equal("1.0", dbContent.Version);
    }

    [Fact]
    public void Files_AddingNewEntry_ShouldStoreCorrectly()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        var fileEntry = new FileEntry();
        var fileId = "test-file-1";

        // Act
        dbContent.Files.Add(fileId, fileEntry);

        // Assert
        Assert.Single(dbContent.Files);
        Assert.Contains(fileId, dbContent.Files.Keys);
        Assert.Same(fileEntry, dbContent.Files[fileId]);
    }

    [Fact]
    public void Files_SettingNewDictionary_ShouldReplaceExisting()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        var newFiles = new Dictionary<string, FileEntry>
        {
            { "file1", new FileEntry() },
            { "file2", new FileEntry() }
        };

        // Act
        dbContent.Files = newFiles;

        // Assert
        Assert.Equal(2, dbContent.Files.Count);
        Assert.Same(newFiles, dbContent.Files);
    }

    [Fact]
    public void Version_SettingNewVersion_ShouldUpdateCorrectly()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        var newVersion = "2.0";

        // Act
        dbContent.Version = newVersion;

        // Assert
        Assert.Equal(newVersion, dbContent.Version);
    }

    [Fact]
    public void Files_RemovingEntry_ShouldRemoveCorrectly()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        var fileId = "test-file";
        dbContent.Files.Add(fileId, new FileEntry());

        // Act
        var removed = dbContent.Files.Remove(fileId);

        // Assert
        Assert.True(removed);
        Assert.Empty(dbContent.Files);
    }

    [Fact]
    public void Files_ClearingDictionary_ShouldRemoveAllEntries()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        dbContent.Files.Add("file1", new FileEntry());
        dbContent.Files.Add("file2", new FileEntry());

        // Act
        dbContent.Files.Clear();

        // Assert
        Assert.Empty(dbContent.Files);
    }

    [Fact]
    public void Files_AddingDuplicateKey_ShouldThrowException()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        var fileId = "duplicate-file";
        dbContent.Files.Add(fileId, new FileEntry());

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            dbContent.Files.Add(fileId, new FileEntry()));
    }

    [Fact]
    public void Files_AddingNullKey_ShouldThrowException()
    {
        // Arrange
        var dbContent = new DatabaseContent();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            dbContent.Files.Add(null, new FileEntry()));
    }

    [Fact]
    public void Files_KeyCaseSensitivity_ShouldTreatDifferentCasesAsDistinct()
    {
        // Arrange
        var dbContent = new DatabaseContent();
        var lowerCaseKey = "testfile";
        var upperCaseKey = "TESTFILE";

        // Act
        dbContent.Files.Add(lowerCaseKey, new FileEntry());
        dbContent.Files.Add(upperCaseKey, new FileEntry());

        // Assert
        Assert.Equal(2, dbContent.Files.Count);
        Assert.Contains(lowerCaseKey, dbContent.Files.Keys);
        Assert.Contains(upperCaseKey, dbContent.Files.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Files_AddingEmptyOrWhitespaceKey_ShouldBeAllowed(string key)
    {
        // Arrange
        var dbContent = new DatabaseContent();

        // Act
        dbContent.Files.Add(key, new FileEntry());

        // Assert
        Assert.Single(dbContent.Files);
        Assert.Contains(key, dbContent.Files.Keys);
    }

    [Theory]
    [InlineData("0.0.1")]
    [InlineData("999.999.999")]
    [InlineData("v1.0")]
    [InlineData("")]
    [InlineData(" ")]
    public void Version_SettingDifferentVersionFormats_ShouldBeAllowed(string version)
    {
        // Arrange
        var dbContent = new DatabaseContent();

        // Act
        dbContent.Version = version;

        // Assert
        Assert.Equal(version, dbContent.Version);
    }

    [Fact]
    public void Version_SettingNullVersion_ShouldBeAllowed()
    {
        // Arrange
        var dbContent = new DatabaseContent();

        // Act
        dbContent.Version = null;

        // Assert
        Assert.Null(dbContent.Version);
    }

    [Fact]
    public void Files_SettingNullDictionary_ShouldBeAllowed()
    {
        // Arrange
        var dbContent = new DatabaseContent();

        // Act
        dbContent.Files = null;

        // Assert
        Assert.Null(dbContent.Files);
    }
}