namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the supported text subtitle formats for the nonPGS workflow.
/// </summary>
public enum SubtitleFormat
{
    /// <summary>
    /// SubRip Subtitle format (.srt)
    /// </summary>
    SRT,

    /// <summary>
    /// Advanced SubStation Alpha format (.ass)
    /// </summary>
    ASS,

    /// <summary>
    /// WebVTT format (.vtt)
    /// </summary>
    VTT
}
