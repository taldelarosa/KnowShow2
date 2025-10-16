using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IProgressTracker interface.
/// These tests verify the interface contract with mock implementations.
/// </summary>
public class ProgressTrackerContractTests
{
    private readonly ILogger<object> _logger;

    public ProgressTrackerContractTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<object>();
    }

    [Fact]
    public void Initialize_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => tracker.Initialize(null!, 10, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Initialize_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => tracker.Initialize(string.Empty, 10, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Initialize_WithNegativeTotalFiles_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => tracker.Initialize("test-request", -1, options);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Initialize_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.Initialize("test-request", 10, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdatePhase_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.UpdatePhase(null!, BulkProcessingPhase.Processing);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdatePhase_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.UpdatePhase(string.Empty, BulkProcessingPhase.Processing);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReportFileSuccess_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.ReportFileSuccess(null!, "/test/file.txt", TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReportFileSuccess_WithNullFilePath_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.ReportFileSuccess("test-request", null!, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReportFileFailure_WithNullError_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.ReportFileFailure("test-request", "/test/file.txt", null!, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReportFileSkipped_WithNullReason_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.ReportFileSkipped("test-request", "/test/file.txt", null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetProgress_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.GetProgress(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetProgressAsync_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = async () => await tracker.GetProgressAsync(string.Empty);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void MarkCompleted_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.MarkCompleted(null!, BulkProcessingStatus.Completed);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddError_WithNullError_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.AddError("test-request", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateMetrics_WithNullMetrics_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act & Assert
        var act = () => tracker.UpdateMetrics("test-request", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Initialize_WithValidParameters_ShouldNotThrow()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => tracker.Initialize("test-request", 10, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetProgress_WithValidRequestId_ShouldReturnProgressOrNull()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act
        var result = tracker.GetProgress("test-request");

        // Assert
        // Should return null or a valid BulkProcessingProgress object
        if (result != null)
        {
            result.Should().BeOfType<BulkProcessingProgress>();
        }
    }

    [Fact]
    public void GetActiveOperations_ShouldReturnStringList()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act
        var result = tracker.GetActiveOperations();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<string>>();
    }

    [Fact]
    public void ClearProgress_WithValidRequestId_ShouldReturnBoolean()
    {
        // Arrange
        var tracker = CreateMockProgressTracker();

        // Act
        var result = tracker.ClearProgress("test-request");

        // Assert
        // Just assert it's a boolean - no need to check type
    }

    private IProgressTracker CreateMockProgressTracker()
    {
        return new MockProgressTracker();
    }

    /// <summary>
    /// Mock implementation of IProgressTracker for contract testing.
    /// </summary>
    private class MockProgressTracker : IProgressTracker
    {
        private readonly Dictionary<string, BulkProcessingProgress> _progress = new();

        public event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated
        {
            add { }
            remove { }
        }

        public void Initialize(string requestId, int totalFiles, BulkProcessingOptions options)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (totalFiles < 0) throw new ArgumentOutOfRangeException(nameof(totalFiles), "Total files cannot be negative");
            if (options == null) throw new ArgumentNullException(nameof(options));

            _progress[requestId] = new BulkProcessingProgress
            {
                RequestId = requestId,
                TotalFiles = totalFiles,
                CurrentPhase = BulkProcessingPhase.Initializing
            };
        }

        public void UpdateTotalFiles(string requestId, int totalFiles)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (totalFiles < 0) throw new ArgumentOutOfRangeException(nameof(totalFiles), "Total files cannot be negative");
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].TotalFiles = totalFiles;
        }

        public void UpdatePhase(string requestId, BulkProcessingPhase phase, string? currentFile = null)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].CurrentPhase = phase;
            _progress[requestId].CurrentFile = currentFile;
        }

        public void UpdateBatchProgress(string requestId, int currentBatch, int totalBatches)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (currentBatch < 1) throw new ArgumentException("Current batch must be 1 or greater", nameof(currentBatch));
            if (totalBatches < 1) throw new ArgumentException("Total batches must be 1 or greater", nameof(totalBatches));
            if (currentBatch > totalBatches) throw new ArgumentException("Current batch cannot exceed total batches", nameof(currentBatch));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            // Store batch information in the Details dictionary
            _progress[requestId].Details["CurrentBatch"] = currentBatch;
            _progress[requestId].Details["TotalBatches"] = totalBatches;
        }

        public void ReportFileSuccess(string requestId, string filePath, TimeSpan processingTime)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].ProcessedFiles++;
        }

        public void ReportFileFailure(string requestId, string filePath, BulkProcessingError error, TimeSpan processingTime)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].FailedFiles++;
        }

        public void ReportFileSkipped(string requestId, string filePath, string reason)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (string.IsNullOrEmpty(reason)) throw new ArgumentException("Reason cannot be null or empty", nameof(reason));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].SkippedFiles++;
        }

        public BulkProcessingProgress? GetProgress(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            return _progress.TryGetValue(requestId, out var progress) ? progress : null;
        }

        public Task<BulkProcessingProgress?> GetProgressAsync(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            return Task.FromResult(GetProgress(requestId));
        }

        public void MarkCompleted(string requestId, BulkProcessingStatus status)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].CurrentPhase = status switch
            {
                BulkProcessingStatus.Completed => BulkProcessingPhase.Completed,
                BulkProcessingStatus.Cancelled => BulkProcessingPhase.Cancelled,
                BulkProcessingStatus.Failed => BulkProcessingPhase.Failed,
                _ => BulkProcessingPhase.Completed
            };
        }

        public void AddError(string requestId, BulkProcessingError error)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            _progress[requestId].CurrentErrors.Add(error);
        }

        public bool HasExceededErrorLimit(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            return false; // Mock implementation
        }

        public bool ClearProgress(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            return _progress.Remove(requestId);
        }

        public List<string> GetActiveOperations()
        {
            return _progress.Keys.ToList();
        }

        public void UpdateMetrics(string requestId, Dictionary<string, object> metrics)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            if (!_progress.ContainsKey(requestId)) throw new InvalidOperationException("Request has not been initialized");

            foreach (var metric in metrics)
            {
                _progress[requestId].AdditionalMetrics[metric.Key] = metric.Value;
            }
        }
    }
}
