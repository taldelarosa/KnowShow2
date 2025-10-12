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

    // Concurrent Operation Details

    /// <summary>
    /// Gets or sets the maximum number of concurrent operations configured for this processing.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of operations currently running concurrently.
    /// </summary>
    public int ActiveConcurrentOperations { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total number of operations that have been queued for processing.
    /// </summary>
    public int QueuedOperations { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total number of operations completed so far.
    /// </summary>
    public int CompletedOperations { get; set; } = 0;

    /// <summary>
    /// Gets or sets the average time per operation based on completed operations.
    /// </summary>
    public TimeSpan AverageOperationTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the list of files currently being processed concurrently.
    /// </summary>
    public List<string> CurrentlyProcessingFiles { get; set; } = new();

    /// <summary>
    /// Gets the concurrency utilization as a percentage (0-100).
    /// Shows how well the available concurrent slots are being utilized.
    /// </summary>
    public double ConcurrencyUtilization => MaxConcurrency > 0 ? (double)ActiveConcurrentOperations / MaxConcurrency * 100 : 0;

    /// <summary>
    /// Gets the estimated throughput based on current concurrency settings and operation time.
    /// Returns operations per minute.
    /// </summary>
    public double EstimatedThroughput
    {
        get
        {
            if (AverageOperationTime.TotalSeconds <= 0 || MaxConcurrency <= 0)
                return 0;

            var operationsPerSecond = MaxConcurrency / AverageOperationTime.TotalSeconds;
            return operationsPerSecond * 60; // Convert to operations per minute
        }
    }

    /// <summary>
    /// Gets a summary of concurrent processing status for display.
    /// </summary>
    public string ConcurrencyStatus => $"{ActiveConcurrentOperations}/{MaxConcurrency} active ({ConcurrencyUtilization:F1}% utilization)";

    /// <summary>
    /// Updates the concurrent operation metrics.
    /// </summary>
    /// <param name="activeConcurrentOps">Current number of active concurrent operations</param>
    /// <param name="queuedOps">Number of operations in queue</param>
    /// <param name="completedOps">Total completed operations</param>
    /// <param name="averageOpTime">Average time per operation</param>
    /// <param name="currentlyProcessing">List of files currently being processed</param>
    public void UpdateConcurrencyMetrics(
        int activeConcurrentOps,
        int queuedOps,
        int completedOps,
        TimeSpan averageOpTime,
        List<string>? currentlyProcessing = null)
    {
        ActiveConcurrentOperations = activeConcurrentOps;
        QueuedOperations = queuedOps;
        CompletedOperations = completedOps;
        AverageOperationTime = averageOpTime;
        CurrentlyProcessingFiles = currentlyProcessing ?? new List<string>();
        LastUpdateTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a snapshot of the current concurrent processing details for logging or display.
    /// </summary>
    /// <returns>Dictionary containing concurrent operation details</returns>
    public Dictionary<string, object> GetConcurrencySnapshot()
    {
        return new Dictionary<string, object>
        {
            ["maxConcurrency"] = MaxConcurrency,
            ["activeConcurrentOperations"] = ActiveConcurrentOperations,
            ["queuedOperations"] = QueuedOperations,
            ["completedOperations"] = CompletedOperations,
            ["concurrencyUtilization"] = ConcurrencyUtilization,
            ["averageOperationTimeMs"] = AverageOperationTime.TotalMilliseconds,
            ["estimatedThroughputPerMin"] = EstimatedThroughput,
            ["currentlyProcessingFiles"] = CurrentlyProcessingFiles.ToArray(),
            ["concurrencyStatus"] = ConcurrencyStatus
        };
    }
}
