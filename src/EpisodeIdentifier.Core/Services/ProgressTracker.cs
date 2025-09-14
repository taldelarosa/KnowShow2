using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for tracking progress of bulk processing operations.
/// Provides thread-safe progress reporting with statistics and time estimation.
/// </summary>
public class ProgressTracker : IProgressTracker
{
    private readonly ILogger<ProgressTracker> _logger;
    private readonly ConcurrentDictionary<string, BulkProcessingProgress> _progressData = new();
    private readonly ConcurrentDictionary<string, BulkProcessingOptions> _options = new();
    private readonly object _eventLock = new object();

    /// <summary>
    /// Initializes a new instance of the ProgressTracker class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    public ProgressTracker(ILogger<ProgressTracker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;

    /// <inheritdoc />
    public void Initialize(string requestId, int totalFiles, BulkProcessingOptions options)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (totalFiles < 0) throw new ArgumentOutOfRangeException(nameof(totalFiles), "Total files cannot be negative");
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Initializing progress tracker for request {RequestId} with {TotalFiles} files", requestId, totalFiles);

        var progress = new BulkProcessingProgress
        {
            RequestId = requestId,
            TotalFiles = totalFiles,
            ProcessedFiles = 0,
            FailedFiles = 0,
            SkippedFiles = 0,
            CurrentPhase = BulkProcessingPhase.Initializing,
            StartTime = DateTime.UtcNow,
            LastUpdateTime = DateTime.UtcNow
        };

        _progressData[requestId] = progress;
        _options[requestId] = options;

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public void UpdatePhase(string requestId, BulkProcessingPhase phase, string? currentFile = null)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogDebug("Updating phase for request {RequestId} to {Phase}", requestId, phase);

        progress.CurrentPhase = phase;
        progress.CurrentFile = currentFile;
        progress.LastUpdateTime = DateTime.UtcNow;

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public void UpdateBatchProgress(string requestId, int currentBatch, int totalBatches)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (currentBatch < 1) throw new ArgumentException("Current batch must be 1 or greater", nameof(currentBatch));
        if (totalBatches < 1) throw new ArgumentException("Total batches must be 1 or greater", nameof(totalBatches));
        if (currentBatch > totalBatches) throw new ArgumentException("Current batch cannot exceed total batches", nameof(currentBatch));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogDebug("Updating batch progress for request {RequestId}: batch {CurrentBatch}/{TotalBatches}",
            requestId, currentBatch, totalBatches);

        lock (progress)
        {
            // Store batch information in the Details dictionary
            progress.Details["CurrentBatch"] = currentBatch;
            progress.Details["TotalBatches"] = totalBatches;
            progress.Details["BatchProgress"] = (double)currentBatch / totalBatches * 100.0;
            progress.LastUpdateTime = DateTime.UtcNow;
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public void ReportFileSuccess(string requestId, string filePath, TimeSpan processingTime)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogDebug("Reporting file success for request {RequestId}: {FilePath}", requestId, filePath);

        lock (progress)
        {
            progress.ProcessedFiles++;
            progress.CurrentFile = filePath;
            progress.LastUpdateTime = DateTime.UtcNow;

            // Update additional metrics
            UpdateProcessingMetrics(progress, processingTime, true);
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public void ReportFileFailure(string requestId, string filePath, BulkProcessingError error, TimeSpan processingTime)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        if (error == null) throw new ArgumentNullException(nameof(error));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogDebug("Reporting file failure for request {RequestId}: {FilePath} - {ErrorMessage}", requestId, filePath, error.Message);

        lock (progress)
        {
            progress.FailedFiles++;
            progress.CurrentFile = filePath;
            progress.LastUpdateTime = DateTime.UtcNow;
            progress.CurrentErrors.Add(error);

            // Update additional metrics
            UpdateProcessingMetrics(progress, processingTime, false);
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public void ReportFileSkipped(string requestId, string filePath, string reason)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        if (string.IsNullOrEmpty(reason)) throw new ArgumentException("Reason cannot be null or empty", nameof(reason));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogDebug("Reporting file skipped for request {RequestId}: {FilePath} - {Reason}", requestId, filePath, reason);

        lock (progress)
        {
            progress.SkippedFiles++;
            progress.CurrentFile = filePath;
            progress.LastUpdateTime = DateTime.UtcNow;

            // Add skip reason to metrics
            if (!progress.AdditionalMetrics.ContainsKey("SkipReasons"))
            {
                progress.AdditionalMetrics["SkipReasons"] = new Dictionary<string, int>();
            }

            var skipReasons = (Dictionary<string, int>)progress.AdditionalMetrics["SkipReasons"];
            skipReasons[reason] = skipReasons.GetValueOrDefault(reason, 0) + 1;
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public BulkProcessingProgress? GetProgress(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

        return _progressData.TryGetValue(requestId, out var progress) ? CloneProgress(progress) : null;
    }

    /// <inheritdoc />
    public Task<BulkProcessingProgress?> GetProgressAsync(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

        return Task.FromResult(GetProgress(requestId));
    }

    /// <inheritdoc />
    public void MarkCompleted(string requestId, BulkProcessingStatus status)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogInformation("Marking request {RequestId} as completed with status {Status}", requestId, status);

        lock (progress)
        {
            progress.CurrentPhase = status switch
            {
                BulkProcessingStatus.Completed => BulkProcessingPhase.Completed,
                BulkProcessingStatus.CompletedWithWarnings => BulkProcessingPhase.Completed,
                BulkProcessingStatus.Cancelled => BulkProcessingPhase.Cancelled,
                BulkProcessingStatus.Failed => BulkProcessingPhase.Failed,
                _ => BulkProcessingPhase.Completed
            };

            progress.LastUpdateTime = DateTime.UtcNow;

            // Calculate final statistics
            UpdateFinalStatistics(progress);
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public void AddError(string requestId, BulkProcessingError error)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (error == null) throw new ArgumentNullException(nameof(error));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogWarning("Adding error to request {RequestId}: {ErrorMessage}", requestId, error.Message);

        lock (progress)
        {
            progress.CurrentErrors.Add(error);
            progress.LastUpdateTime = DateTime.UtcNow;
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <inheritdoc />
    public bool HasExceededErrorLimit(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");
        if (!_options.TryGetValue(requestId, out var options)) return false;

        if (options.MaxErrors == 0) return false; // No limit

        var totalErrors = progress.FailedFiles + progress.CurrentErrors.Count;
        return totalErrors >= options.MaxErrors;
    }

    /// <inheritdoc />
    public bool ClearProgress(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

        _logger.LogInformation("Clearing progress data for request {RequestId}", requestId);

        var removed = _progressData.TryRemove(requestId, out _);
        _options.TryRemove(requestId, out _);

        return removed;
    }

    /// <inheritdoc />
    public List<string> GetActiveOperations()
    {
        return _progressData.Keys.Where(requestId =>
        {
            if (_progressData.TryGetValue(requestId, out var progress))
            {
                return progress.CurrentPhase != BulkProcessingPhase.Completed &&
                       progress.CurrentPhase != BulkProcessingPhase.Cancelled &&
                       progress.CurrentPhase != BulkProcessingPhase.Failed;
            }
            return false;
        }).ToList();
    }

    /// <inheritdoc />
    public void UpdateMetrics(string requestId, Dictionary<string, object> metrics)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));
        if (!_progressData.TryGetValue(requestId, out var progress)) throw new InvalidOperationException("Request has not been initialized");

        _logger.LogDebug("Updating metrics for request {RequestId} with {MetricCount} metrics", requestId, metrics.Count);

        lock (progress)
        {
            foreach (var metric in metrics)
            {
                progress.AdditionalMetrics[metric.Key] = metric.Value;
            }
            progress.LastUpdateTime = DateTime.UtcNow;
        }

        OnProgressUpdated(requestId, progress);
    }

    /// <summary>
    /// Raises the ProgressUpdated event in a thread-safe manner.
    /// </summary>
    private void OnProgressUpdated(string requestId, BulkProcessingProgress progress)
    {
        EventHandler<ProgressUpdatedEventArgs>? handler;
        lock (_eventLock)
        {
            handler = ProgressUpdated;
        }

        handler?.Invoke(this, new ProgressUpdatedEventArgs(requestId, CloneProgress(progress)));
    }

    /// <summary>
    /// Creates a deep clone of the progress to ensure thread safety.
    /// </summary>
    private static BulkProcessingProgress CloneProgress(BulkProcessingProgress original)
    {
        return new BulkProcessingProgress
        {
            RequestId = original.RequestId,
            CurrentPhase = original.CurrentPhase,
            TotalFiles = original.TotalFiles,
            ProcessedFiles = original.ProcessedFiles,
            FailedFiles = original.FailedFiles,
            SkippedFiles = original.SkippedFiles,
            CurrentFile = original.CurrentFile,
            StartTime = original.StartTime,
            LastUpdateTime = original.LastUpdateTime,
            CurrentErrors = new List<BulkProcessingError>(original.CurrentErrors),
            AdditionalMetrics = new Dictionary<string, object>(original.AdditionalMetrics)
        };
    }

    /// <summary>
    /// Updates processing metrics for time estimation.
    /// </summary>
    private static void UpdateProcessingMetrics(BulkProcessingProgress progress, TimeSpan processingTime, bool success)
    {
        // Update average processing time
        var currentAverage = progress.AdditionalMetrics.TryGetValue("AverageProcessingTime", out var avgObj) ?
            (TimeSpan)avgObj : TimeSpan.Zero;
        var processedCount = success ? progress.ProcessedFiles : progress.FailedFiles;

        if (processedCount == 1)
        {
            progress.AdditionalMetrics["AverageProcessingTime"] = processingTime;
        }
        else if (processedCount > 1)
        {
            // Calculate rolling average
            var totalTime = currentAverage.Multiply(processedCount - 1).Add(processingTime);
            progress.AdditionalMetrics["AverageProcessingTime"] = TimeSpan.FromTicks(totalTime.Ticks / processedCount);
        }

        // Track min/max processing times
        var minTime = progress.AdditionalMetrics.TryGetValue("MinProcessingTime", out var minObj) ?
            (TimeSpan)minObj : TimeSpan.MaxValue;
        var maxTime = progress.AdditionalMetrics.TryGetValue("MaxProcessingTime", out var maxObj) ?
            (TimeSpan)maxObj : TimeSpan.Zero;

        if (processingTime < minTime)
        {
            progress.AdditionalMetrics["MinProcessingTime"] = processingTime;
        }
        if (processingTime > maxTime)
        {
            progress.AdditionalMetrics["MaxProcessingTime"] = processingTime;
        }
    }

    /// <summary>
    /// Updates final statistics when processing is completed.
    /// </summary>
    private static void UpdateFinalStatistics(BulkProcessingProgress progress)
    {
        var totalProcessed = progress.ProcessedFiles + progress.FailedFiles + progress.SkippedFiles;
        var successRate = progress.TotalFiles > 0 ? (double)progress.ProcessedFiles / progress.TotalFiles * 100 : 0;
        var errorRate = progress.TotalFiles > 0 ? (double)progress.FailedFiles / progress.TotalFiles * 100 : 0;

        progress.AdditionalMetrics["TotalProcessed"] = totalProcessed;
        progress.AdditionalMetrics["SuccessRate"] = successRate;
        progress.AdditionalMetrics["ErrorRate"] = errorRate;
        progress.AdditionalMetrics["CompletionTime"] = DateTime.UtcNow;
    }
}