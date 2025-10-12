using EpisodeIdentifier.Core.Models.Configuration;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace EpisodeIdentifier.Tests.Unit.Models.Configuration;

/// <summary>
/// Unit tests for ConfigurationValidator bulk processing validation rules.
/// Tests validator behavior for BulkProcessingConfiguration properties.
/// </summary>
public class ConfigurationValidatorBulkProcessingTests
{
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorBulkProcessingTests()
    {
        _validator = new ConfigurationValidator();
    }

    [Fact]
    public void Configuration_WithoutBulkProcessing_ShouldPassValidation()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.BulkProcessing = null;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void BulkProcessing_DefaultBatchSize_WithValidValues_ShouldPassValidation(int batchSize)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultBatchSize = batchSize;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultBatchSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    [InlineData(50000)]
    public void BulkProcessing_DefaultBatchSize_WithInvalidValues_ShouldFailValidation(int batchSize)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultBatchSize = batchSize;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.DefaultBatchSize)
            .WithErrorMessage("BulkProcessing.DefaultBatchSize must be between 1 and 10000");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void BulkProcessing_DefaultMaxConcurrency_WithValidValues_ShouldPassValidation(int concurrency)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxConcurrency = concurrency;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultMaxConcurrency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public void BulkProcessing_DefaultMaxConcurrency_WithInvalidValues_ShouldFailValidation(int concurrency)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxConcurrency = concurrency;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.DefaultMaxConcurrency)
            .WithErrorMessage("BulkProcessing.DefaultMaxConcurrency must be between 1 and 100");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(30000)]
    [InlineData(60000)]
    public void BulkProcessing_DefaultProgressReportingInterval_WithValidValues_ShouldPassValidation(int interval)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultProgressReportingInterval = interval;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultProgressReportingInterval);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(60001)]
    [InlineData(100000)]
    public void BulkProcessing_DefaultProgressReportingInterval_WithInvalidValues_ShouldFailValidation(int interval)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultProgressReportingInterval = interval;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.DefaultProgressReportingInterval)
            .WithErrorMessage("BulkProcessing.DefaultProgressReportingInterval must be between 100 and 60000 milliseconds");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(50000)]
    [InlineData(100000)]
    public void BulkProcessing_DefaultMaxErrorsBeforeAbort_WithValidValues_ShouldPassValidation(int maxErrors)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxErrorsBeforeAbort = maxErrors;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultMaxErrorsBeforeAbort);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    [InlineData(500000)]
    public void BulkProcessing_DefaultMaxErrorsBeforeAbort_WithInvalidValues_ShouldFailValidation(int maxErrors)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxErrorsBeforeAbort = maxErrors;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.DefaultMaxErrorsBeforeAbort!.Value)
            .WithErrorMessage("BulkProcessing.DefaultMaxErrorsBeforeAbort must be between 1 and 100000");
    }

    [Fact]
    public void BulkProcessing_DefaultMaxErrorsBeforeAbort_WithNull_ShouldPassValidation()
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxErrorsBeforeAbort = null;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultMaxErrorsBeforeAbort);
    }

    [Fact]
    public void BulkProcessing_DefaultFileProcessingTimeout_WithNull_ShouldPassValidation()
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultFileProcessingTimeout = null;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultFileProcessingTimeout);
    }

    [Theory]
    [InlineData(1)] // 1 second
    [InlineData(300)] // 5 minutes
    [InlineData(1800)] // 30 minutes
    [InlineData(3600)] // 1 hour
    public void BulkProcessing_DefaultFileProcessingTimeout_WithValidTimeSpan_ShouldPassValidation(int seconds)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultFileProcessingTimeout = TimeSpan.FromSeconds(seconds);

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.DefaultFileProcessingTimeout);
    }

    [Theory]
    [InlineData(0)] // 0 seconds (less than 1 second)
    [InlineData(3601)] // More than 1 hour
    [InlineData(7200)] // 2 hours
    public void BulkProcessing_DefaultFileProcessingTimeout_WithInvalidTimeSpan_ShouldFailValidation(int seconds)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultFileProcessingTimeout = TimeSpan.FromSeconds(seconds);

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        if (seconds < 1)
        {
            result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.DefaultFileProcessingTimeout!.Value)
                .WithErrorMessage("BulkProcessing.DefaultFileProcessingTimeout must be at least 1 second");
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.DefaultFileProcessingTimeout!.Value)
                .WithErrorMessage("BulkProcessing.DefaultFileProcessingTimeout must be at most 1 hour");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10000)]
    [InlineData(25000)]
    [InlineData(50000)]
    public void BulkProcessing_MaxBatchSize_WithValidValues_ShouldPassValidation(int maxBatchSize)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.MaxBatchSize = maxBatchSize;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.MaxBatchSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50001)]
    [InlineData(100000)]
    public void BulkProcessing_MaxBatchSize_WithInvalidValues_ShouldFailValidation(int maxBatchSize)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.MaxBatchSize = maxBatchSize;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.MaxBatchSize)
            .WithErrorMessage("BulkProcessing.MaxBatchSize must be between 1 and 50000");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(500)]
    public void BulkProcessing_MaxConcurrency_WithValidValues_ShouldPassValidation(int maxConcurrency)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.MaxConcurrency = maxConcurrency;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BulkProcessing!.MaxConcurrency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(1000)]
    public void BulkProcessing_MaxConcurrency_WithInvalidValues_ShouldFailValidation(int maxConcurrency)
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.MaxConcurrency = maxConcurrency;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BulkProcessing!.MaxConcurrency)
            .WithErrorMessage("BulkProcessing.MaxConcurrency must be between 1 and 500");
    }

    [Fact]
    public void BulkProcessing_DefaultBatchSizeExceedsMaxBatchSize_ShouldFailValidation()
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultBatchSize = 1000;
        config.BulkProcessing!.MaxBatchSize = 500;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor("BulkProcessing")
            .WithErrorMessage("BulkProcessing.DefaultBatchSize must not exceed MaxBatchSize");
    }

    [Fact]
    public void BulkProcessing_DefaultBatchSizeEqualsMaxBatchSize_ShouldPassValidation()
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultBatchSize = 1000;
        config.BulkProcessing!.MaxBatchSize = 1000;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor("DefaultBatchSize");
    }

    [Fact]
    public void BulkProcessing_DefaultMaxConcurrencyExceedsMaxConcurrency_ShouldFailValidation()
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxConcurrency = 50;
        config.BulkProcessing!.MaxConcurrency = 25;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor("BulkProcessing")
            .WithErrorMessage("BulkProcessing.DefaultMaxConcurrency must not exceed MaxConcurrency");
    }

    [Fact]
    public void BulkProcessing_DefaultMaxConcurrencyEqualsMaxConcurrency_ShouldPassValidation()
    {
        // Arrange
        var config = CreateValidConfigurationWithBulkProcessing();
        config.BulkProcessing!.DefaultMaxConcurrency = 50;
        config.BulkProcessing!.MaxConcurrency = 50;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor("DefaultMaxConcurrency");
    }

    private static EpisodeIdentifier.Core.Models.Configuration.Configuration CreateValidConfiguration()
    {
        return new EpisodeIdentifier.Core.Models.Configuration.Configuration
        {
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
        };
    }

    private static EpisodeIdentifier.Core.Models.Configuration.Configuration CreateValidConfigurationWithBulkProcessing()
    {
        var config = CreateValidConfiguration();
        config.BulkProcessing = new BulkProcessingConfiguration();
        return config;
    }
}
