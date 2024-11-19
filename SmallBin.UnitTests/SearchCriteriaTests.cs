namespace SmallBin.UnitTests;

public class SearchCriteriaTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithNullValues()
    {
        // Act
        var criteria = new SearchCriteria();

        // Assert
        Assert.Null(criteria.FileName);
        Assert.Null(criteria.Tags);
        Assert.Null(criteria.StartDate);
        Assert.Null(criteria.EndDate);
        Assert.Null(criteria.ContentType);
        Assert.Null(criteria.CustomMetadata);
    }

    #endregion

    #region FileName Tests

    [Theory]
    [InlineData("test.txt")]
    [InlineData("document.pdf")]
    [InlineData("image.jpg")]
    [InlineData("very-long-file-name-with-many-characters-and-symbols_123456789.extension")]
    [InlineData("файл.doc")]
    [InlineData("文件.pdf")]
    public void FileName_SettingValidValues_ShouldUpdateCorrectly(string fileName)
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.FileName = fileName;

        // Assert
        Assert.Equal(fileName, criteria.FileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void FileName_SettingNullOrEmptyValues_ShouldBeAllowed(string? fileName)
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.FileName = fileName;

        // Assert
        Assert.Equal(fileName, criteria.FileName);
    }

    #endregion

    #region Tags Tests

    [Fact]
    public void Tags_SettingValidList_ShouldUpdateCorrectly()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var tags = new List<string> { "tag1", "tag2", "tag3" };

        // Act
        criteria.Tags = tags;

        // Assert
        Assert.Equal(tags, criteria.Tags);
    }

    [Fact]
    public void Tags_SettingEmptyList_ShouldBeAllowed()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var tags = new List<string>();

        // Act
        criteria.Tags = tags;

        // Assert
        Assert.Empty(criteria.Tags);
    }

    [Fact]
    public void Tags_SettingNull_ShouldBeAllowed()
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.Tags = null;

        // Assert
        Assert.Null(criteria.Tags);
    }

    [Fact]
    public void Tags_ModifyingList_ShouldReflectChanges()
    {
        // Arrange
        var criteria = new SearchCriteria();
        criteria.Tags = new List<string> { "tag1" };

        // Act
        criteria.Tags.Add("tag2");
        criteria.Tags.Remove("tag1");

        // Assert
        Assert.Single(criteria.Tags);
        Assert.Equal("tag2", criteria.Tags[0]);
    }

    #endregion

    #region Date Range Tests

    [Fact]
    public void StartDate_SettingValidDate_ShouldUpdateCorrectly()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var date = DateTime.UtcNow;

        // Act
        criteria.StartDate = date;

        // Assert
        Assert.Equal(date, criteria.StartDate);
    }

    [Fact]
    public void EndDate_SettingValidDate_ShouldUpdateCorrectly()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var date = DateTime.UtcNow;

        // Act
        criteria.EndDate = date;

        // Assert
        Assert.Equal(date, criteria.EndDate);
    }

    [Fact]
    public void DateRange_SettingNullDates_ShouldBeAllowed()
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.StartDate = null;
        criteria.EndDate = null;

        // Assert
        Assert.Null(criteria.StartDate);
        Assert.Null(criteria.EndDate);
    }

    [Fact]
    public void DateRange_StartDateAfterEndDate_ShouldBeAllowed()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var startDate = DateTime.UtcNow.AddDays(1);
        var endDate = DateTime.UtcNow;

        // Act
        criteria.StartDate = startDate;
        criteria.EndDate = endDate;

        // Assert
        Assert.True(criteria.StartDate > criteria.EndDate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(365)]
    public void DateRange_VariousRanges_ShouldBeAllowed(int daysDifference)
    {
        // Arrange
        var criteria = new SearchCriteria();
        var baseDate = DateTime.UtcNow;

        // Act
        criteria.StartDate = baseDate;
        criteria.EndDate = baseDate.AddDays(daysDifference);

        // Assert
        Assert.Equal(baseDate, criteria.StartDate);
        Assert.Equal(baseDate.AddDays(daysDifference), criteria.EndDate);
    }

    #endregion

    #region ContentType Tests

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("application/octet-stream")]
    [InlineData("application/x-custom-type")]
    public void ContentType_SettingValidValues_ShouldUpdateCorrectly(string contentType)
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.ContentType = contentType;

        // Assert
        Assert.Equal(contentType, criteria.ContentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ContentType_SettingNullOrEmptyValues_ShouldBeAllowed(string? contentType)
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.ContentType = contentType;

        // Assert
        Assert.Equal(contentType, criteria.ContentType);
    }

    #endregion

    #region CustomMetadata Tests

    [Fact]
    public void CustomMetadata_SettingValidDictionary_ShouldUpdateCorrectly()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        criteria.CustomMetadata = metadata;

        // Assert
        Assert.Equal(metadata, criteria.CustomMetadata);
    }

    [Fact]
    public void CustomMetadata_SettingEmptyDictionary_ShouldBeAllowed()
    {
        // Arrange
        var criteria = new SearchCriteria();
        var metadata = new Dictionary<string, string>();

        // Act
        criteria.CustomMetadata = metadata;

        // Assert
        Assert.Empty(criteria.CustomMetadata);
    }

    [Fact]
    public void CustomMetadata_SettingNull_ShouldBeAllowed()
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Act
        criteria.CustomMetadata = null;

        // Assert
        Assert.Null(criteria.CustomMetadata);
    }

    [Fact]
    public void CustomMetadata_ModifyingDictionary_ShouldReflectChanges()
    {
        // Arrange
        var criteria = new SearchCriteria();
        criteria.CustomMetadata = new Dictionary<string, string> { { "key1", "value1" } };

        // Act
        criteria.CustomMetadata["key2"] = "value2";
        criteria.CustomMetadata.Remove("key1");

        // Assert
        Assert.Single(criteria.CustomMetadata);
        Assert.Equal("value2", criteria.CustomMetadata["key2"]);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void SearchCriteria_SettingAllProperties_ShouldMaintainConsistency()
    {
        // Arrange
        var fileName = "test.txt";
        var tags = new List<string> { "tag1", "tag2" };
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(1);
        var contentType = "text/plain";
        var metadata = new Dictionary<string, string> { { "key", "value" } };

        // Act
        var criteria = new SearchCriteria
        {
            FileName = fileName,
            Tags = tags,
            StartDate = startDate,
            EndDate = endDate,
            ContentType = contentType,
            CustomMetadata = metadata
        };

        // Assert
        Assert.Equal(fileName, criteria.FileName);
        Assert.Equal(tags, criteria.Tags);
        Assert.Equal(startDate, criteria.StartDate);
        Assert.Equal(endDate, criteria.EndDate);
        Assert.Equal(contentType, criteria.ContentType);
        Assert.Equal(metadata, criteria.CustomMetadata);
    }

    [Fact]
    public void SearchCriteria_PartialCriteria_ShouldMaintainConsistency()
    {
        // Arrange & Act
        var criteria = new SearchCriteria
        {
            FileName = "test.txt",
            StartDate = DateTime.UtcNow
        };

        // Assert
        Assert.NotNull(criteria.FileName);
        Assert.NotNull(criteria.StartDate);
        Assert.Null(criteria.Tags);
        Assert.Null(criteria.EndDate);
        Assert.Null(criteria.ContentType);
        Assert.Null(criteria.CustomMetadata);
    }

    [Fact]
    public void SearchCriteria_MixedNullAndNonNullValues_ShouldMaintainConsistency()
    {
        // Arrange & Act
        var criteria = new SearchCriteria
        {
            FileName = null,
            Tags = new List<string> { "tag1" },
            StartDate = null,
            EndDate = DateTime.UtcNow,
            ContentType = "",
            CustomMetadata = null
        };

        // Assert
        Assert.Null(criteria.FileName);
        Assert.NotNull(criteria.Tags);
        Assert.Null(criteria.StartDate);
        Assert.NotNull(criteria.EndDate);
        Assert.Empty(criteria.ContentType);
        Assert.Null(criteria.CustomMetadata);
    }

    #endregion
}