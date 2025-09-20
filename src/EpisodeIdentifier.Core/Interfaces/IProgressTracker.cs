using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for tracking progress of bulk processing operations.
/// Provides thread-safe progress reporting with statistics and time estimation.
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    /// Initializes progress tracking for a new bulk processing operation.
    /// </summary>
    /// <param name="requestId">The unique identifier for the processing request.</param>
    /// <param name="totalFiles">The total number of files to process.</param>
    /// <param name="options">The processing options for this operation.</param>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when totalFiles is less than zero.</exception>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    void Initialize(string requestId, int totalFiles, BulkProcessingOptions options);

    /// <summary>
    /// Updates the current processing phase and optionally the current file being processed.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="phase">The current processing phase.</param>
    /// <param name="currentFile">The file currently being processed (optional).</param>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void UpdatePhase(string requestId, BulkProcessingPhase phase, string? currentFile = null);

    /// <summary>
    /// Updates progress with batch completion information.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="currentBatch">The current batch number (1-based).</param>
    /// <param name="totalBatches">The total number of batches.</param>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void UpdateBatchProgress(string requestId, int currentBatch, int totalBatches);

    /// <summary>
    /// Reports the successful processing of a file.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="filePath">The path of the successfully processed file.</param>
    /// <param name="processingTime">The time taken to process this file.</param>
    /// <exception cref="ArgumentException">Thrown when requestId or filePath is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void ReportFileSuccess(string requestId, string filePath, TimeSpan processingTime);

    /// <summary>
    /// Reports a file processing failure.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="filePath">The path of the failed file.</param>
    /// <param name="error">The error that occurred.</param>
    /// <param name="processingTime">The time spent attempting to process this file.</param>
    /// <exception cref="ArgumentException">Thrown when requestId or filePath is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void ReportFileFailure(string requestId, string filePath, BulkProcessingError error, TimeSpan processingTime);

    /// <summary>
    /// Reports that a file was skipped during processing.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="filePath">The path of the skipped file.</param>
    /// <param name="reason">The reason the file was skipped.</param>
    /// <exception cref="ArgumentException">Thrown when requestId, filePath, or reason is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void ReportFileSkipped(string requestId, string filePath, string reason);

    /// <summary>
    /// Gets the current progress for a processing operation.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The current progress, or null if the request is not found.</returns>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    BulkProcessingProgress? GetProgress(string requestId);

    /// <summary>
    /// Gets the current progress with thread-safe snapshot semantics.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>A task representing the current progress, or null if the request is not found.</returns>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    Task<BulkProcessingProgress?> GetProgressAsync(string requestId);

    /// <summary>
    /// Marks a processing operation as completed.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="status">The final status of the operation.</param>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void MarkCompleted(string requestId, BulkProcessingStatus status);

    /// <summary>
    /// Adds a general error to the processing operation.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="error">The error that occurred.</param>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void AddError(string requestId, BulkProcessingError error);

    /// <summary>
    /// Determines if the error limit has been exceeded for a processing operation.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>True if the error limit has been exceeded, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    bool HasExceededErrorLimit(string requestId);

    /// <summary>
    /// Clears progress tracking data for a completed operation.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>True if the data was cleared, false if the request was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    bool ClearProgress(string requestId);

    /// <summary>
    /// Gets all currently tracked processing operations.
    /// </summary>
    /// <returns>A list of request IDs for active operations.</returns>
    List<string> GetActiveOperations();

    /// <summary>
    /// Updates custom metrics for a processing operation.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="metrics">The custom metrics to add or update.</param>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when metrics is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request hasn't been initialized.</exception>
    void UpdateMetrics(string requestId, Dictionary<string, object> metrics);

    /// <summary>
    /// Event raised when progress is updated for any operation.
    /// </summary>
    event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;
}

/// <summary>
/// Event arguments for progress update events.
/// </summary>
public class ProgressUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the request ID for the updated operation.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// Gets the current progress information.
    /// </summary>
    public BulkProcessingProgress Progress { get; }

    /// <summary>
    /// Initializes a new instance of the ProgressUpdatedEventArgs class.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="progress">The progress information.</param>
    public ProgressUpdatedEventArgs(string requestId, BulkProcessingProgress progress)
    {
        RequestId = requestId;
        Progress = progress;
    }
}
