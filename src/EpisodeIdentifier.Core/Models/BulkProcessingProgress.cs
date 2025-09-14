namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the current progress of a bulk processing operation.
/// </summary>
public class BulkProcessingProgress
{
    /// <summary>
    /// Gets or sets the request ID for the operation being tracked.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current phase of processing.
    /// </summary>
    public BulkProcessingPhase CurrentPhase { get; set; } = BulkProcessingPhase.Initializing;

    /// <summary>
    /// Gets or sets the total number of files discovered for processing.
    /// </summary>
    public int TotalFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of files processed so far.
    /// </summary>
    public int ProcessedFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of files that failed processing.
    /// </summary>
    public int FailedFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of files skipped.
    /// </summary>
    public int SkippedFiles { get; set; } = 0;

    /// <summary>
    /// Gets or sets the current file being processed.
    /// </summary>
    public string? CurrentFile { get; set; }

    /// <summary>
    /// Gets or sets when processing started.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this progress update was created.
    /// </summary>
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the elapsed time since processing started.
    /// </summary>
    public TimeSpan ElapsedTime => LastUpdateTime.Subtract(StartTime);

    /// <summary>
    /// Gets the percentage of files processed (0-100).
    /// </summary>
    public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;

    /// <summary>
    /// Gets the current processing rate in files per second.
    /// </summary>
    public double ProcessingRate => ElapsedTime.TotalSeconds > 0 ? ProcessedFiles / ElapsedTime.TotalSeconds : 0;

    /// <summary>
    /// Gets the estimated time remaining based on current processing rate.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (ProcessingRate <= 0 || ProcessedFiles >= TotalFiles)
                return null;

            var remainingFiles = TotalFiles - ProcessedFiles;
            var secondsRemaining = remainingFiles / ProcessingRate;
            return TimeSpan.FromSeconds(secondsRemaining);
        }
    }

    /// <summary>
    /// Gets the estimated completion time.
    /// </summary>
    public DateTime? EstimatedCompletionTime => EstimatedTimeRemaining.HasValue ? LastUpdateTime.Add(EstimatedTimeRemaining.Value) : null;

    /// <summary>
    /// Gets or sets any current errors or issues.
    /// </summary>
    public List<BulkProcessingError> CurrentErrors { get; set; } = new();

    /// <summary>
    /// Gets or sets additional details about the processing operation.
    /// This includes batch information, configuration details, etc.
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Gets or sets additional progress metrics.
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}