using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Tests.Unit.Services;

/// <summary>
/// Unit tests for ProgressTracker service.
/// Tests progress calculation, thread safety, and event handling.
/// </summary>
public class ProgressTrackerTests
{
    private readonly ILogger<ProgressTracker> _logger;
    private readonly ProgressTracker _progressTracker;

    public ProgressTrackerTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ProgressTracker>();
        _progressTracker = new ProgressTracker(_logger);
    }

    [Fact]
    public void Initialize_WithValidParameters_ShouldCreateProgress()
    {
        // Arrange
        var requestId = "test-request";
        var totalFiles = 100;
        var options = new BulkProcessingOptions();

        // Act
        _progressTracker.Initialize(requestId, totalFiles, options);

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress.Should().NotBeNull();
        progress!.RequestId.Should().Be(requestId);
        progress.TotalFiles.Should().Be(totalFiles);
        progress.ProcessedFiles.Should().Be(0);
        progress.FailedFiles.Should().Be(0);
        progress.SkippedFiles.Should().Be(0);
        progress.CurrentPhase.Should().Be(BulkProcessingPhase.Initializing);
    }

    [Fact]
    public void Initialize_WithNullRequestId_ShouldThrowException()
    {
        // Arrange & Act & Assert
        var act = () => _progressTracker.Initialize(null!, 100, new BulkProcessingOptions());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Initialize_WithNegativeTotalFiles_ShouldThrowException()
    {
        // Arrange & Act & Assert
        var act = () => _progressTracker.Initialize("test", -1, new BulkProcessingOptions());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdatePhase_WithValidParameters_ShouldUpdatePhase()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act
        _progressTracker.UpdatePhase(requestId, BulkProcessingPhase.Processing, "/test/file.txt");

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.CurrentPhase.Should().Be(BulkProcessingPhase.Processing);
        progress.CurrentFile.Should().Be("/test/file.txt");
    }

    [Fact]
    public void ReportFileSuccess_WithValidParameters_ShouldIncrementProcessedFiles()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());
        var processingTime = TimeSpan.FromSeconds(2);

        // Act
        _progressTracker.ReportFileSuccess(requestId, "/test/file.txt", processingTime);

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.ProcessedFiles.Should().Be(1);
        progress.CurrentFile.Should().Be("/test/file.txt");
    }

    [Fact]
    public void ReportFileFailure_WithValidParameters_ShouldIncrementFailedFiles()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());
        var error = new BulkProcessingError(BulkProcessingErrorType.FileNotFound, "File not found", "/test/file.txt");
        var processingTime = TimeSpan.FromSeconds(1);

        // Act
        _progressTracker.ReportFileFailure(requestId, "/test/file.txt", error, processingTime);

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.FailedFiles.Should().Be(1);
        progress.CurrentErrors.Should().Contain(error);
    }

    [Fact]
    public void ReportFileSkipped_WithValidParameters_ShouldIncrementSkippedFiles()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act
        _progressTracker.ReportFileSkipped(requestId, "/test/file.txt", "Unsupported format");

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.SkippedFiles.Should().Be(1);
        progress.AdditionalMetrics.Should().ContainKey("SkipReasons");
        
        var skipReasons = (Dictionary<string, int>)progress.AdditionalMetrics["SkipReasons"];
        skipReasons.Should().ContainKey("Unsupported format");
        skipReasons["Unsupported format"].Should().Be(1);
    }

    [Fact]
    public void GetProgress_WithNonExistentRequestId_ShouldReturnNull()
    {
        // Act
        var progress = _progressTracker.GetProgress("nonexistent");

        // Assert
        progress.Should().BeNull();
    }

    [Fact]
    public async Task GetProgressAsync_WithValidRequestId_ShouldReturnProgress()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act
        var progress = await _progressTracker.GetProgressAsync(requestId);

        // Assert
        progress.Should().NotBeNull();
        progress!.RequestId.Should().Be(requestId);
    }

    [Fact]
    public void MarkCompleted_WithValidStatus_ShouldUpdatePhase()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act
        _progressTracker.MarkCompleted(requestId, BulkProcessingStatus.Completed);

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.CurrentPhase.Should().Be(BulkProcessingPhase.Completed);
    }

    [Fact]
    public void AddError_WithValidError_ShouldAddToCurrentErrors()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());
        var error = new BulkProcessingError(BulkProcessingErrorType.ConfigurationError, "Config error");

        // Act
        _progressTracker.AddError(requestId, error);

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.CurrentErrors.Should().Contain(error);
    }

    [Fact]
    public void HasExceededErrorLimit_WithNoLimit_ShouldReturnFalse()
    {
        // Arrange
        var requestId = "test-request";
        var options = new BulkProcessingOptions { MaxErrors = 0 }; // No limit
        _progressTracker.Initialize(requestId, 10, options);

        // Add many errors
        for (int i = 0; i < 100; i++)
        {
            var error = new BulkProcessingError(BulkProcessingErrorType.Unknown, $"Error {i}");
            _progressTracker.ReportFileFailure(requestId, $"/file{i}.txt", error, TimeSpan.Zero);
        }

        // Act
        var exceeded = _progressTracker.HasExceededErrorLimit(requestId);

        // Assert
        exceeded.Should().BeFalse();
    }

    [Fact]
    public void HasExceededErrorLimit_WithLimit_ShouldReturnTrueWhenExceeded()
    {
        // Arrange
        var requestId = "test-request";
        var options = new BulkProcessingOptions { MaxErrors = 5 };
        _progressTracker.Initialize(requestId, 10, options);

        // Add errors up to the limit
        for (int i = 0; i < 6; i++)
        {
            var error = new BulkProcessingError(BulkProcessingErrorType.Unknown, $"Error {i}");
            _progressTracker.ReportFileFailure(requestId, $"/file{i}.txt", error, TimeSpan.Zero);
        }

        // Act
        var exceeded = _progressTracker.HasExceededErrorLimit(requestId);

        // Assert
        exceeded.Should().BeTrue();
    }

    [Fact]
    public void ClearProgress_WithValidRequestId_ShouldRemoveProgress()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act
        var cleared = _progressTracker.ClearProgress(requestId);

        // Assert
        cleared.Should().BeTrue();
        _progressTracker.GetProgress(requestId).Should().BeNull();
    }

    [Fact]
    public void GetActiveOperations_WithMultipleOperations_ShouldReturnActiveOnes()
    {
        // Arrange
        var completedRequest = "completed-request";
        var activeRequest = "active-request";
        var cancelledRequest = "cancelled-request";

        _progressTracker.Initialize(completedRequest, 10, new BulkProcessingOptions());
        _progressTracker.Initialize(activeRequest, 10, new BulkProcessingOptions());
        _progressTracker.Initialize(cancelledRequest, 10, new BulkProcessingOptions());

        _progressTracker.MarkCompleted(completedRequest, BulkProcessingStatus.Completed);
        _progressTracker.MarkCompleted(cancelledRequest, BulkProcessingStatus.Cancelled);
        _progressTracker.UpdatePhase(activeRequest, BulkProcessingPhase.Processing);

        // Act
        var activeOperations = _progressTracker.GetActiveOperations();

        // Assert
        activeOperations.Should().ContainSingle();
        activeOperations.Should().Contain(activeRequest);
        activeOperations.Should().NotContain(completedRequest);
        activeOperations.Should().NotContain(cancelledRequest);
    }

    [Fact]
    public void UpdateMetrics_WithValidMetrics_ShouldUpdateAdditionalMetrics()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());
        var metrics = new Dictionary<string, object>
        {
            ["CustomMetric1"] = "value1",
            ["CustomMetric2"] = 42
        };

        // Act
        _progressTracker.UpdateMetrics(requestId, metrics);

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.AdditionalMetrics.Should().ContainKey("CustomMetric1");
        progress.AdditionalMetrics.Should().ContainKey("CustomMetric2");
        progress.AdditionalMetrics["CustomMetric1"].Should().Be("value1");
        progress.AdditionalMetrics["CustomMetric2"].Should().Be(42);
    }

    [Fact]
    public void ProgressUpdated_Event_ShouldFireWhenProgressChanges()
    {
        // Arrange
        var requestId = "test-request";
        var eventsFired = new List<ProgressUpdatedEventArgs>();
        
        _progressTracker.ProgressUpdated += (sender, args) => eventsFired.Add(args);
        
        // Act
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());
        _progressTracker.ReportFileSuccess(requestId, "/test/file.txt", TimeSpan.FromSeconds(1));
        _progressTracker.UpdatePhase(requestId, BulkProcessingPhase.Processing);

        // Assert
        eventsFired.Should().HaveCount(3); // Initialize, ReportFileSuccess, UpdatePhase
        eventsFired.All(e => e.RequestId == requestId).Should().BeTrue();
        eventsFired.All(e => e.Progress != null).Should().BeTrue();
    }

    [Fact]
    public void PercentComplete_Calculation_ShouldBeAccurate()
    {
        // Arrange
        var requestId = "test-request";
        var totalFiles = 100;
        _progressTracker.Initialize(requestId, totalFiles, new BulkProcessingOptions());

        // Act - Process 25 files
        for (int i = 0; i < 25; i++)
        {
            _progressTracker.ReportFileSuccess(requestId, $"/file{i}.txt", TimeSpan.FromSeconds(1));
        }

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.PercentComplete.Should().BeApproximately(25.0, 0.1);
    }

    [Fact]
    public void ProcessingRate_Calculation_ShouldBeReasonable()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act - Process some files with a small delay to ensure time passes
        _progressTracker.ReportFileSuccess(requestId, "/file1.txt", TimeSpan.FromSeconds(1));
        Thread.Sleep(100); // Small delay to ensure time difference
        _progressTracker.ReportFileSuccess(requestId, "/file2.txt", TimeSpan.FromSeconds(1));

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.ProcessingRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimatedTimeRemaining_WithValidRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var requestId = "test-request";
        var totalFiles = 10;
        _progressTracker.Initialize(requestId, totalFiles, new BulkProcessingOptions());

        // Act - Process half the files
        for (int i = 0; i < 5; i++)
        {
            _progressTracker.ReportFileSuccess(requestId, $"/file{i}.txt", TimeSpan.FromSeconds(1));
        }
        Thread.Sleep(100); // Ensure some time passes

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        if (progress!.ProcessingRate > 0) // Only test if we have a meaningful rate
        {
            progress.EstimatedTimeRemaining.Should().NotBeNull();
            progress.EstimatedTimeRemaining.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    [Fact]
    public void ThreadSafety_MultipleThreads_ShouldHandleConcurrentUpdates()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 1000, new BulkProcessingOptions());
        var tasks = new List<Task>();

        // Act - Create multiple tasks that update progress concurrently
        for (int threadId = 0; threadId < 10; threadId++)
        {
            var localThreadId = threadId;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _progressTracker.ReportFileSuccess(requestId, $"/thread{localThreadId}/file{i}.txt", TimeSpan.FromMilliseconds(100));
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.ProcessedFiles.Should().Be(100); // 10 threads * 10 files each
    }

    [Fact]
    public void ProcessingMetrics_ShouldTrackAverageAndMinMaxTimes()
    {
        // Arrange
        var requestId = "test-request";
        _progressTracker.Initialize(requestId, 10, new BulkProcessingOptions());

        // Act - Report files with different processing times
        _progressTracker.ReportFileSuccess(requestId, "/file1.txt", TimeSpan.FromSeconds(1));
        _progressTracker.ReportFileSuccess(requestId, "/file2.txt", TimeSpan.FromSeconds(3));
        _progressTracker.ReportFileSuccess(requestId, "/file3.txt", TimeSpan.FromSeconds(2));

        // Assert
        var progress = _progressTracker.GetProgress(requestId);
        progress!.AdditionalMetrics.Should().ContainKey("AverageProcessingTime");
        progress.AdditionalMetrics.Should().ContainKey("MinProcessingTime");
        progress.AdditionalMetrics.Should().ContainKey("MaxProcessingTime");

        var avgTime = (TimeSpan)progress.AdditionalMetrics["AverageProcessingTime"];
        var minTime = (TimeSpan)progress.AdditionalMetrics["MinProcessingTime"];
        var maxTime = (TimeSpan)progress.AdditionalMetrics["MaxProcessingTime"];

        avgTime.Should().Be(TimeSpan.FromSeconds(2)); // (1+3+2)/3 = 2
        minTime.Should().Be(TimeSpan.FromSeconds(1));
        maxTime.Should().Be(TimeSpan.FromSeconds(3));
    }
}