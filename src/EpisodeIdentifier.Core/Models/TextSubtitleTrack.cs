namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents a text subtitle track extracted from a video file.
/// Contains both metadata and content for text-based subtitle formats.
/// </summary>
public class TextSubtitleTrack
{
    /// <summary>
    /// Index of the subtitle track in the video file (0-based).
    /// </summary>
    public int TrackIndex { get; set; }

    /// <summary>
    /// Language code of the subtitle track (e.g., "en", "ja").
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Format of the subtitle content.
    /// </summary>
    public SubtitleFormat Format { get; set; }

    /// <summary>
    /// Raw text content of the subtitle file.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Number of subtitle entries/cues in this track.
    /// </summary>
    public int SubtitleCount { get; set; }

    /// <summary>
    /// Current processing status of this track.
    /// </summary>
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total duration of the subtitle track in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Additional metadata extracted from the subtitle track.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
