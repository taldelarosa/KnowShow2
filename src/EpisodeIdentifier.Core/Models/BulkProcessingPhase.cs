namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the different phases of bulk processing.
/// </summary>
public enum BulkProcessingPhase
{
    /// <summary>
    /// Unknown or unspecified phase.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Initializing the processing operation.
    /// </summary>
    Initializing = 1,

    /// <summary>
    /// Validating the processing request and options.
    /// </summary>
    Validating = 2,

    /// <summary>
    /// Discovering files in the specified paths.
    /// </summary>
    Discovery = 3,

    /// <summary>
    /// Preparing files for processing.
    /// </summary>
    Preparing = 4,

    /// <summary>
    /// Processing individual files.
    /// </summary>
    Processing = 5,

    /// <summary>
    /// Creating backups if requested.
    /// </summary>
    Backup = 6,

    /// <summary>
    /// Renaming files based on identification results.
    /// </summary>
    Renaming = 7,

    /// <summary>
    /// Finalizing the processing operation.
    /// </summary>
    Finalizing = 8,

    /// <summary>
    /// Cleaning up temporary resources.
    /// </summary>
    Cleanup = 9,

    /// <summary>
    /// Processing operation is complete.
    /// </summary>
    Completed = 10,

    /// <summary>
    /// Processing operation was cancelled.
    /// </summary>
    Cancelled = 11,

    /// <summary>
    /// Processing operation failed.
    /// </summary>
    Failed = 12
}