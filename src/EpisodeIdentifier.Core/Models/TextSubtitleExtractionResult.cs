namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of text subtitle extraction from a video file.
/// Contains all extracted text subtitle tracks and associated metadata.
/// </summary>
public class TextSubtitleExtractionResult
{
    /// <summary>
    /// Path to the source video file.
    /// </summary>
    public string VideoFilePath { get; set; } = string.Empty;

    /// <summary>
    /// List of successfully extracted text subtitle tracks.
    /// </summary>
    public List<TextSubtitleTrack> ExtractedTracks { get; set; } = new();

    /// <summary>
    /// Total number of subtitle tracks found in the video file.
    /// </summary>
    public int TotalTrackCount { get; set; }

    /// <summary>
    /// Number of successfully extracted text subtitle tracks.
    /// </summary>
    public int SuccessfulExtractions { get; set; }

    /// <summary>
    /// Number of tracks that failed extraction.
    /// </summary>
    public int FailedExtractions { get; set; }

    /// <summary>
    /// Number of tracks that were skipped (e.g., PGS tracks).
    /// </summary>
    public int SkippedTracks { get; set; }

    /// <summary>
    /// Overall processing status of the extraction operation.
    /// </summary>
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

    /// <summary>
    /// Error message if the entire extraction operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total time taken for extraction in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Indicates whether the extraction was successful overall.
    /// </summary>
    public bool IsSuccessful => Status == ProcessingStatus.Completed && SuccessfulExtractions > 0;

    /// <summary>
    /// Gets all tracks that have a specific processing status.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <returns>Collection of tracks with the specified status.</returns>
    public IEnumerable<TextSubtitleTrack> GetTracksByStatus(ProcessingStatus status)
    {
        return ExtractedTracks.Where(track => track.Status == status);
    }

    /// <summary>
    /// Gets all tracks that match a specific subtitle format.
    /// </summary>
    /// <param name="format">The format to filter by.</param>
    /// <returns>Collection of tracks with the specified format.</returns>
    public IEnumerable<TextSubtitleTrack> GetTracksByFormat(SubtitleFormat format)
    {
        return ExtractedTracks.Where(track => track.Format == format);
    }
}
