namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the overall status of a bulk processing operation.
/// </summary>
public enum BulkProcessingStatus
{
    /// <summary>
    /// Processing has not started yet.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Processing is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Processing completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Processing was cancelled by the user.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Processing failed due to an error.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Processing completed with some warnings or non-fatal errors.
    /// </summary>
    CompletedWithWarnings = 5,

    /// <summary>
    /// Processing was paused and can be resumed.
    /// </summary>
    Paused = 6
}
