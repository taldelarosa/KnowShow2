using System.Collections.Concurrent;

namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of a bulk processing operation.
/// Designed to be thread-safe for concurrent operation result aggregation.
/// </summary>
public class BulkProcessingResult
{
    private int _processedFiles = 0;
    private int _failedFiles = 0;
    private int _skippedFiles = 0;

    /// <summary>
    /// Gets or sets the request ID that generated this result.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall status of the bulk processing operation.
    /// </summary>
    public BulkProcessingStatus Status { get; set; } = BulkProcessingStatus.NotStarted;

    /// <summary>
    /// Gets or sets when the processing started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the processing completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the processing operation.
    /// </summary>
    public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? DateTime.UtcNow.Subtract(StartedAt);

    /// <summary>
    /// Gets or sets the total number of files discovered for processing.
    /// </summary>
    public int TotalFiles { get; set; } = 0;

    /// <summary>
    /// Gets the number of files successfully processed.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public int ProcessedFiles => _processedFiles;

    /// <summary>
    /// Gets the number of files that failed processing.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public int FailedFiles => _failedFiles;

    /// <summary>
    /// Gets the number of files skipped during processing.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public int SkippedFiles => _skippedFiles;

    /// <summary>
    /// Gets or sets the processing rate in files per second.
    /// </summary>
    public double ProcessingRate => Duration.TotalSeconds > 0 ? ProcessedFiles / Duration.TotalSeconds : 0;

    /// <summary>
    /// Gets detailed results for individual files.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public ConcurrentBag<FileProcessingResult> FileResults { get; private set; } = new();

    /// <summary>
    /// Gets any errors that occurred during bulk processing.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public ConcurrentBag<BulkProcessingError> Errors { get; private set; } = new();

    /// <summary>
    /// Gets or sets the final progress information.
    /// </summary>
    public BulkProcessingProgress? FinalProgress { get; set; }

    /// <summary>
    /// Gets or sets summary statistics for the processing operation.
    /// </summary>
    public Dictionary<string, object> Statistics { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether the processing completed successfully.
    /// </summary>
    public bool IsSuccessful => Status == BulkProcessingStatus.Completed && FailedFiles == 0;

    /// <summary>
    /// Gets a value indicating whether the processing was cancelled.
    /// </summary>
    public bool WasCancelled => Status == BulkProcessingStatus.Cancelled;

    /// <summary>
    /// Thread-safe method to increment the processed files counter.
    /// </summary>
    public void IncrementProcessedFiles() => Interlocked.Increment(ref _processedFiles);

    /// <summary>
    /// Thread-safe method to increment the failed files counter.
    /// </summary>
    public void IncrementFailedFiles() => Interlocked.Increment(ref _failedFiles);

    /// <summary>
    /// Thread-safe method to increment the skipped files counter.
    /// </summary>
    public void IncrementSkippedFiles() => Interlocked.Increment(ref _skippedFiles);

    /// <summary>
    /// Gets file results as a list for JSON serialization.
    /// Converts the thread-safe ConcurrentBag to a standard List.
    /// </summary>
    public List<FileProcessingResult> GetFileResultsAsList() => FileResults.ToList();

    /// <summary>
    /// Gets errors as a list for JSON serialization.
    /// Converts the thread-safe ConcurrentBag to a standard List.
    /// </summary>
    public List<BulkProcessingError> GetErrorsAsList() => Errors.ToList();
}