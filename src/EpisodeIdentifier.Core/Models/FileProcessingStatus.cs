namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the processing status of an individual file.
/// </summary>
public enum FileProcessingStatus
{
    /// <summary>
    /// File has not been processed yet.
    /// </summary>
    NotProcessed = 0,

    /// <summary>
    /// File is currently being processed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// File was processed successfully.
    /// </summary>
    Success = 2,

    /// <summary>
    /// File processing failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// File was skipped (e.g., unsupported format, already processed).
    /// </summary>
    Skipped = 4,

    /// <summary>
    /// File processing was cancelled.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// File processing completed with warnings.
    /// </summary>
    Warning = 6,

    /// <summary>
    /// File is queued for processing.
    /// </summary>
    Queued = 7
}