namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the processing status of text subtitle tracks in the nonPGS workflow.
/// </summary>
public enum ProcessingStatus
{
    /// <summary>
    /// Processing has not yet started
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Successfully processed
    /// </summary>
    Completed,

    /// <summary>
    /// Processing failed due to an error
    /// </summary>
    Failed,

    /// <summary>
    /// Processing was skipped (e.g., unsupported format)
    /// </summary>
    Skipped
}
