namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the source type for subtitle extraction.
/// Used to indicate where subtitles are being extracted from.
/// </summary>
public enum SubtitleSourceType
{
    /// <summary>
    /// Subtitles are embedded in the video container.
    /// </summary>
    Embedded,

    /// <summary>
    /// Subtitles are in external files alongside the video.
    /// </summary>
    External,

    /// <summary>
    /// Mixed sources - both embedded and external subtitles.
    /// </summary>
    Mixed
}
