namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of a bulk processing operation.
/// </summary>
public class BulkProcessingResult
{
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
    /// Gets or sets the number of files successfully processed.
    /// </summary>
    public int ProcessedFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of files that failed processing.
    /// </summary>
    public int FailedFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of files skipped during processing.
    /// </summary>
    public int SkippedFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the processing rate in files per second.
    /// </summary>
    public double ProcessingRate => Duration.TotalSeconds > 0 ? ProcessedFiles / Duration.TotalSeconds : 0;

    /// <summary>
    /// Gets or sets detailed results for individual files.
    /// </summary>
    public List<FileProcessingResult> FileResults { get; set; } = new();

    /// <summary>
    /// Gets or sets any errors that occurred during bulk processing.
    /// </summary>
    public List<BulkProcessingError> Errors { get; set; } = new();

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
}