using EpisodeIdentifier.Core.Models.Configuration;
using FluentValidation;

namespace EpisodeIdentifier.Tests.Unit;

/// <summary>
/// Comprehensive unit tests for Configuration validation rules and edge cases.
/// Tests all validation scenarios, boundary conditions, and error handling.
/// </summary>
public class ConfigurationValidationTests
{
    private readonly ConfigurationValidator _validator = new();
    private readonly FilenamesPatternsValidator _filenameValidator = new();

    #region Version Validation Tests

    [Fact]
    public void Version_EmptyString_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Version = string.Empty;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.Version) &&
                                           e.ErrorMessage == "Version is required");
    }

    [Fact]
    public void Version_Null_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Version = null!;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.Version) &&
                                           e.ErrorMessage == "Version is required");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("2.1")]
    [InlineData("1.2.3")]
    [InlineData("10.20.30")]
    [InlineData("0.0")]
    [InlineData("0.1.0")]
    public void Version_ValidSemanticVersions_ShouldNotHaveValidationError(string version)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Version = version;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(Configuration.Version)));
    }

    [Theory]
    [InlineData("1")]                    // Single number
    [InlineData("1.2.3.4")]            // Too many parts
    [InlineData("v1.2.3")]             // Prefix not allowed
    [InlineData("1.a.3")]              // Non-numeric parts
    [InlineData("1.-1.3")]             // Negative numbers
    public void Version_InvalidSemanticVersions_ShouldHaveValidationError(string version)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Version = version;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.Version) &&
                                           e.ErrorMessage == "Version must be a valid semantic version (e.g., '2.0', '1.2.3')");
    }

    #endregion

    #region Confidence Threshold Tests

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void MatchConfidenceThreshold_OutsideValidRange_ShouldHaveValidationError(decimal threshold)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MatchConfidenceThreshold = threshold;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.MatchConfidenceThreshold));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void MatchConfidenceThreshold_WithinValidRange_ShouldNotHaveValidationError(decimal threshold)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MatchConfidenceThreshold = threshold;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(Configuration.MatchConfidenceThreshold)));
    }

    [Fact]
    public void RenameConfidenceThreshold_LowerThanMatchThreshold_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MatchConfidenceThreshold = 0.7m;
        config.RenameConfidenceThreshold = 0.5m; // Lower than match threshold

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("RenameConfidenceThreshold must be greater than or equal to MatchConfidenceThreshold"));
    }

    #endregion

    #region Fuzzy Hash Threshold Tests

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public void FuzzyHashThreshold_OutsideValidRange_ShouldHaveValidationError(int threshold)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.FuzzyHashThreshold = threshold;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.FuzzyHashThreshold));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void FuzzyHashThreshold_WithinValidRange_ShouldNotHaveValidationError(int threshold)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.FuzzyHashThreshold = threshold;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(Configuration.FuzzyHashThreshold) &&
                                                              e.ErrorMessage.Contains("must be between")));
    }

    [Fact]
    public void FuzzyHashThreshold_ZeroWithCTPhAlgorithm_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.HashingAlgorithm = HashingAlgorithm.CTPH;
        config.FuzzyHashThreshold = 0;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("FuzzyHashThreshold is required when HashingAlgorithm is CTPH"));
    }

    [Fact]
    public void FuzzyHashThreshold_ZeroWithNonCTPhAlgorithm_ShouldNotHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.HashingAlgorithm = HashingAlgorithm.MD5;
        config.FuzzyHashThreshold = 0;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.ErrorMessage.Contains("FuzzyHashThreshold is required when HashingAlgorithm is CTPH")));
    }

    #endregion

    #region Hashing Algorithm Tests

    [Theory]
    [InlineData(HashingAlgorithm.MD5)]
    [InlineData(HashingAlgorithm.SHA1)]
    [InlineData(HashingAlgorithm.CTPH)]
    public void HashingAlgorithm_ValidEnumValues_ShouldNotHaveValidationError(HashingAlgorithm algorithm)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.HashingAlgorithm = algorithm;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(Configuration.HashingAlgorithm)));
    }

    [Fact]
    public void HashingAlgorithm_InvalidEnumValue_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.HashingAlgorithm = (HashingAlgorithm)999; // Invalid enum value

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.HashingAlgorithm));
    }

    #endregion

    #region Filename Template Tests

    [Fact]
    public void FilenameTemplate_EmptyString_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.FilenameTemplate = string.Empty;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.FilenameTemplate) &&
                                           e.ErrorMessage == "FilenameTemplate is required");
    }

    [Theory]
    [InlineData("{SeriesName} S{Season}E{Episode}")]
    [InlineData("{SeriesName} - S{Season}E{Episode} - Title")]
    [InlineData("{SeriesName}.S{Season}.E{Episode}.mkv")]
    public void FilenameTemplate_WithAllRequiredPlaceholders_ShouldNotHaveValidationError(string template)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.FilenameTemplate = template;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(Configuration.FilenameTemplate)));
    }

    [Theory]
    [InlineData("S{Season}E{Episode}")]                          // Missing SeriesName
    [InlineData("{SeriesName} E{Episode}")]                      // Missing Season
    [InlineData("{SeriesName} S{Season}")]                       // Missing Episode
    [InlineData("No placeholders at all")]                       // Missing all placeholders
    public void FilenameTemplate_MissingRequiredPlaceholders_ShouldHaveValidationError(string template)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.FilenameTemplate = template;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.FilenameTemplate) &&
                                           e.ErrorMessage.Contains("must contain required placeholders"));
    }

    #endregion

    #region Filename Patterns Tests

    [Fact]
    public void FilenamePatterns_Null_ShouldHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.FilenamePatterns = null!;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Configuration.FilenamePatterns) &&
                                           e.ErrorMessage == "FilenamePatterns is required");
    }

    [Fact]
    public void PrimaryPattern_EmptyString_ShouldHaveValidationError()
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = string.Empty
        };

        // Act
        var result = _filenameValidator.Validate(patterns);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FilenamePatterns.PrimaryPattern) &&
                                           e.ErrorMessage == "PrimaryPattern is required");
    }

    [Theory]
    [InlineData("[invalid")]                                     // Unmatched bracket
    [InlineData("(invalid")]                                     // Unmatched parenthesis
    [InlineData("*invalid")]                                     // Invalid quantifier placement
    public void PrimaryPattern_InvalidRegex_ShouldHaveValidationError(string pattern)
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = pattern
        };

        // Act
        var result = _filenameValidator.Validate(patterns);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FilenamePatterns.PrimaryPattern) &&
                                           e.ErrorMessage.Contains("must be a valid regex pattern"));
    }

    [Theory]
    [InlineData(@"(?<SeriesName>.+)\.S(?<Season>\d+)")]         // Missing Episode
    [InlineData(@"(?<Season>\d+)E(?<Episode>\d+)")]             // Missing SeriesName
    [InlineData(@"(?<SeriesName>.+)\.E(?<Episode>\d+)")]        // Missing Season
    public void PrimaryPattern_MissingRequiredGroups_ShouldHaveValidationError(string pattern)
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = pattern
        };

        // Act
        var result = _filenameValidator.Validate(patterns);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("must contain named capture groups: SeriesName, Season, Episode"));
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Fact]
    public void Configuration_AllFieldsValid_ShouldNotHaveAnyValidationErrors()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Configuration_MultipleInvalidFields_ShouldHaveMultipleValidationErrors()
    {
        // Arrange
        var config = new Configuration
        {
            Version = "invalid-version",
            MatchConfidenceThreshold = -1.0m,
            RenameConfidenceThreshold = 2.0m,
            FuzzyHashThreshold = -10,
            HashingAlgorithm = (HashingAlgorithm)999,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = "[invalid"
            },
            FilenameTemplate = "No placeholders"
        };

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5); // Should have multiple errors

        // Check for specific error categories
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Version"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("MatchConfidenceThreshold"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("RenameConfidenceThreshold"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("FuzzyHashThreshold"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("HashingAlgorithm"));
    }

    [Fact]
    public void Configuration_CTPhWithValidThreshold_ShouldNotHaveValidationError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.HashingAlgorithm = HashingAlgorithm.CTPH;
        config.FuzzyHashThreshold = 50;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a valid configuration object for testing purposes.
    /// </summary>
    /// <returns>Valid Configuration instance.</returns>
    private static Configuration CreateValidConfiguration()
    {
        return new Configuration
        {
            Version = "2.0.0",
            MatchConfidenceThreshold = 0.7m,
            RenameConfidenceThreshold = 0.8m,
            FuzzyHashThreshold = 50,
            HashingAlgorithm = HashingAlgorithm.MD5,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)"
            },
            FilenameTemplate = "{SeriesName} S{Season}E{Episode}"
        };
    }

    #endregion
}

/// <summary>
/// Unit tests specifically focused on FilenamePatterns utility methods and edge cases.
/// </summary>
public class FilenamePatternsUtilityTests
{
    [Fact]
    public void AreAllPatternsValid_AllValidPatterns_ShouldReturnTrue()
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)",
            FallbackPatterns = new List<string>
            {
                @"(?<SeriesName>.+)\.(?<Season>\d+)x(?<Episode>\d+)",
                @"(?<SeriesName>.+)\.Episode\.(?<Episode>\d+)"
            },
            SeriesNamePattern = @"(?<SeriesName>[^\.]+)",
            SeasonEpisodePattern = @"S(?<Season>\d+)E(?<Episode>\d+)"
        };

        // Act
        var result = patterns.AreAllPatternsValid();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreAllPatternsValid_InvalidPrimaryPattern_ShouldReturnFalse()
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = "[invalid",
            FallbackPatterns = new List<string>(),
            SeriesNamePattern = null,
            SeasonEpisodePattern = null
        };

        // Act
        var result = patterns.AreAllPatternsValid();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasRequiredCaptureGroups_AllRequiredGroups_ShouldReturnTrue()
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)"
        };

        // Act
        var result = patterns.HasRequiredCaptureGroups();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasRequiredCaptureGroups_MissingSeriesName_ShouldReturnFalse()
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = @"S(?<Season>\d+)E(?<Episode>\d+)"
        };

        // Act
        var result = patterns.HasRequiredCaptureGroups();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetAllCaptureGroups_MultiplePatterns_ShouldReturnUniqueGroups()
    {
        // Arrange
        var patterns = new FilenamePatterns
        {
            PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)",
            FallbackPatterns = new List<string>
            {
                @"(?<SeriesName>.+)\.(?<Season>\d+)x(?<Episode>\d+)",
                @"(?<Title>.+)\.(?<Quality>\d+p)"
            },
            SeriesNamePattern = @"(?<SeriesName>[^\.]+)",
            SeasonEpisodePattern = @"S(?<Season>\d+)E(?<Episode>\d+)"
        };

        // Act
        var result = patterns.GetAllCaptureGroups().ToList();

        // Assert
        Assert.Contains("SeriesName", result);
        Assert.Contains("Season", result);
        Assert.Contains("Episode", result);
        Assert.Contains("Title", result);
        Assert.Contains("Quality", result);
        Assert.Equal(5, result.Count); // Should be unique
    }

    [Fact]
    public void Clone_ShouldCreateDeepCopy()
    {
        // Arrange
        var original = new FilenamePatterns
        {
            PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)",
            FallbackPatterns = new List<string> { "pattern1", "pattern2" },
            SeriesNamePattern = "series",
            SeasonEpisodePattern = "season"
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.PrimaryPattern, clone.PrimaryPattern);
        Assert.Equal(original.SeriesNamePattern, clone.SeriesNamePattern);
        Assert.Equal(original.SeasonEpisodePattern, clone.SeasonEpisodePattern);
        Assert.Equal(original.FallbackPatterns, clone.FallbackPatterns);
        Assert.NotSame(original.FallbackPatterns, clone.FallbackPatterns); // Should be different instances
    }
}

/// <summary>
/// Unit tests for ValidationResult and ConfigurationResult utility classes.
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void ValidationResult_Success_ShouldCreateValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_FailureWithArray_ShouldCreateInvalidResult()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var result = ValidationResult.Failure(errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("Error 1", result.Errors);
        Assert.Contains("Error 2", result.Errors);
    }

    [Fact]
    public void ConfigurationResult_Success_ShouldCreateValidResult()
    {
        // Arrange
        var config = new Configuration { Version = "1.0.0" };

        // Act
        var result = ConfigurationResult.Success(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(config, result.Configuration);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ConfigurationResult_Failure_ShouldCreateInvalidResult()
    {
        // Arrange
        var errors = new[] { "Configuration error" };

        // Act
        var result = ConfigurationResult.Failure(errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.Configuration);
        Assert.Single(result.Errors);
        Assert.Equal("Configuration error", result.Errors[0]);
    }
}