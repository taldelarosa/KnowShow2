using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Models.Configuration;
using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for configuration validation functionality.
/// These tests verify the configuration validation contract with range validation for MaxConcurrency.
/// </summary>
public class ConfigurationValidationContractTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(25, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(-1, false)]
    [InlineData(500, false)]
    public void MaxConcurrency_ValidationAttribute_ValidatesRange(int maxConcurrency, bool shouldBeValid)
    {
        // Arrange
        var config = new Configuration
        {
            MaxConcurrency = maxConcurrency,
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        var context = new ValidationContext(config);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(config, context, results, validateAllProperties: true);

        // Assert
        if (shouldBeValid)
        {
            isValid.Should().BeTrue();
            results.Should().BeEmpty();
        }
        else
        {
            isValid.Should().BeFalse();
            results.Should().Contain(r => r.MemberNames.Contains(nameof(Configuration.MaxConcurrency)));
        }
    }

    [Fact]
    public void MaxConcurrency_RangeAttribute_HasCorrectRange()
    {
        // Arrange
        var property = typeof(Configuration).GetProperty(nameof(Configuration.MaxConcurrency));

        // Act
        var rangeAttribute = property!.GetCustomAttributes(typeof(RangeAttribute), false)
            .Cast<RangeAttribute>()
            .FirstOrDefault();

        // Assert
        rangeAttribute.Should().NotBeNull();
        rangeAttribute!.Minimum.Should().Be(1);
        rangeAttribute.Maximum.Should().Be(100);
    }

    [Fact]
    public void Configuration_DefaultMaxConcurrency_IsOne()
    {
        // Arrange & Act
        var config = new Configuration();

        // Assert
        config.MaxConcurrency.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void Configuration_WithValidMaxConcurrency_PassesValidation(int validConcurrency)
    {
        // Arrange
        var config = new Configuration
        {
            MaxConcurrency = validConcurrency,
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        var context = new ValidationContext(config);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(config, context, results, validateAllProperties: true);

        // Assert
        isValid.Should().BeTrue();
        results.Where(r => r.MemberNames.Contains(nameof(Configuration.MaxConcurrency)))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "MaxConcurrency")]
    [InlineData(-5, "MaxConcurrency")]
    [InlineData(101, "MaxConcurrency")]
    [InlineData(1000, "MaxConcurrency")]
    public void Configuration_WithInvalidMaxConcurrency_FailsValidation(int invalidConcurrency, string expectedMember)
    {
        // Arrange
        var config = new Configuration
        {
            MaxConcurrency = invalidConcurrency,
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        var context = new ValidationContext(config);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(config, context, results, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(expectedMember));
    }
}
