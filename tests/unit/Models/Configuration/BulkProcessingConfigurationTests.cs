using System.ComponentModel.DataAnnotations;
using EpisodeIdentifier.Core.Models.Configuration;
using FluentAssertions;
using Xunit;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace EpisodeIdentifier.Tests.Unit.Models.Configuration;

/// <summary>
/// Unit tests for BulkProcessingConfiguration validation and behavior.
/// Tests all property constraints, validation rules, and default values.
/// </summary>
public class BulkProcessingConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new BulkProcessingConfiguration();

        // Assert
        config.DefaultBatchSize.Should().Be(100);
        config.DefaultMaxConcurrency.Should().Be(Environment.ProcessorCount);
        config.DefaultProgressReportingInterval.Should().Be(1000);
        config.DefaultForceGarbageCollection.Should().BeTrue();
        config.DefaultCreateBackups.Should().BeFalse();
        config.DefaultContinueOnError.Should().BeTrue();
        config.DefaultMaxErrorsBeforeAbort.Should().BeNull();
        config.DefaultFileProcessingTimeout.Should().Be(TimeSpan.FromMinutes(5));
        config.DefaultFileExtensions.Should().BeEmpty();
        config.MaxBatchSize.Should().Be(10000);
        config.MaxConcurrency.Should().Be(Environment.ProcessorCount * 8);
        config.EnableBatchStatistics.Should().BeFalse();
        config.EnableMemoryMonitoring.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void DefaultBatchSize_WithValidValues_ShouldPassValidation(int batchSize)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultBatchSize = batchSize
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultBatchSize));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    [InlineData(50000)]
    public void DefaultBatchSize_WithInvalidValues_ShouldFailValidation(int batchSize)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultBatchSize = batchSize
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultBatchSize));

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.First().ErrorMessage.Should().Contain("field DefaultBatchSize must be between 1 and 10000");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void DefaultMaxConcurrency_WithValidValues_ShouldPassValidation(int concurrency)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultMaxConcurrency = concurrency
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultMaxConcurrency));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public void DefaultMaxConcurrency_WithInvalidValues_ShouldFailValidation(int concurrency)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultMaxConcurrency = concurrency
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultMaxConcurrency));

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.First().ErrorMessage.Should().Contain("field DefaultMaxConcurrency must be between 1 and 100");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(30000)]
    [InlineData(60000)]
    public void DefaultProgressReportingInterval_WithValidValues_ShouldPassValidation(int interval)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultProgressReportingInterval = interval
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultProgressReportingInterval));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(60001)]
    [InlineData(100000)]
    public void DefaultProgressReportingInterval_WithInvalidValues_ShouldFailValidation(int interval)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultProgressReportingInterval = interval
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultProgressReportingInterval));

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.First().ErrorMessage.Should().Contain("field DefaultProgressReportingInterval must be between 100 and 60000");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(50000)]
    [InlineData(100000)]
    public void DefaultMaxErrorsBeforeAbort_WithValidValues_ShouldPassValidation(int maxErrors)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultMaxErrorsBeforeAbort = maxErrors
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultMaxErrorsBeforeAbort));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    [InlineData(500000)]
    public void DefaultMaxErrorsBeforeAbort_WithInvalidValues_ShouldFailValidation(int maxErrors)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultMaxErrorsBeforeAbort = maxErrors
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultMaxErrorsBeforeAbort));

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.First().ErrorMessage.Should().Contain("field DefaultMaxErrorsBeforeAbort must be between 1 and 100000");
    }

    [Fact]
    public void DefaultMaxErrorsBeforeAbort_WithNull_ShouldPassValidation()
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultMaxErrorsBeforeAbort = null
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultMaxErrorsBeforeAbort));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10000)]
    [InlineData(25000)]
    [InlineData(50000)]
    public void MaxBatchSize_WithValidValues_ShouldPassValidation(int maxBatchSize)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            MaxBatchSize = maxBatchSize
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.MaxBatchSize));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50001)]
    [InlineData(100000)]
    public void MaxBatchSize_WithInvalidValues_ShouldFailValidation(int maxBatchSize)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            MaxBatchSize = maxBatchSize
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.MaxBatchSize));

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.First().ErrorMessage.Should().Contain("field MaxBatchSize must be between 1 and 50000");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(500)]
    public void MaxConcurrency_WithValidValues_ShouldPassValidation(int maxConcurrency)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            MaxConcurrency = maxConcurrency
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.MaxConcurrency));

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(1000)]
    public void MaxConcurrency_WithInvalidValues_ShouldFailValidation(int maxConcurrency)
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            MaxConcurrency = maxConcurrency
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.MaxConcurrency));

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.First().ErrorMessage.Should().Contain("field MaxConcurrency must be between 1 and 500");
    }

    [Fact]
    public void DefaultFileExtensions_ShouldBeModifiable()
    {
        // Arrange
        var config = new BulkProcessingConfiguration();

        // Act
        config.DefaultFileExtensions.Add(".mkv");
        config.DefaultFileExtensions.Add(".mp4");
        config.DefaultFileExtensions.Add(".avi");

        // Assert
        config.DefaultFileExtensions.Should().HaveCount(3);
        config.DefaultFileExtensions.Should().Contain(new[] { ".mkv", ".mp4", ".avi" });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BooleanProperties_ShouldAcceptValidValues(bool value)
    {
        // Arrange & Act
        var config = new BulkProcessingConfiguration
        {
            DefaultForceGarbageCollection = value,
            DefaultCreateBackups = value,
            DefaultContinueOnError = value,
            EnableBatchStatistics = value,
            EnableMemoryMonitoring = value
        };

        // Assert
        config.DefaultForceGarbageCollection.Should().Be(value);
        config.DefaultCreateBackups.Should().Be(value);
        config.DefaultContinueOnError.Should().Be(value);
        config.EnableBatchStatistics.Should().Be(value);
        config.EnableMemoryMonitoring.Should().Be(value);
    }

    [Fact]
    public void DefaultFileProcessingTimeout_WithNull_ShouldBeValid()
    {
        // Arrange
        var config = new BulkProcessingConfiguration
        {
            DefaultFileProcessingTimeout = null
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultFileProcessingTimeout));

        // Assert
        validationResults.Should().BeEmpty();
        config.DefaultFileProcessingTimeout.Should().BeNull();
    }

    [Theory]
    [InlineData(1)] // 1 second
    [InlineData(300)] // 5 minutes
    [InlineData(1800)] // 30 minutes
    [InlineData(3600)] // 1 hour
    public void DefaultFileProcessingTimeout_WithValidTimeSpan_ShouldBeValid(int seconds)
    {
        // Arrange
        var timeSpan = TimeSpan.FromSeconds(seconds);
        var config = new BulkProcessingConfiguration
        {
            DefaultFileProcessingTimeout = timeSpan
        };

        // Act
        var validationResults = ValidateProperty(config, nameof(config.DefaultFileProcessingTimeout));

        // Assert
        validationResults.Should().BeEmpty();
        config.DefaultFileProcessingTimeout.Should().Be(timeSpan);
    }

    /// <summary>
    /// Validates a specific property of an object using data annotations.
    /// </summary>
    private static List<ValidationResult> ValidateProperty<T>(T obj, string propertyName)
    {
        var context = new ValidationContext(obj, serviceProvider: null, items: null)
        {
            MemberName = propertyName
        };

        var property = typeof(T).GetProperty(propertyName);
        var value = property?.GetValue(obj);

        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(value, context, results);

        return results;
    }
}