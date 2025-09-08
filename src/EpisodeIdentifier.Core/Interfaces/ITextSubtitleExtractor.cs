using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for extracting text subtitle tracks from video files.
/// Handles the initial extraction phase of the nonPGS workflow.
/// </summary>
public interface ITextSubtitleExtractor
{
    /// <summary>
    /// Extracts all available text subtitle tracks from a video file.
    /// This method identifies and extracts content from supported text subtitle formats.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file to extract subtitles from.</param>
    /// <param name="cancellationToken">Token to cancel the extraction operation.</param>
    /// <returns>Result containing all extracted text subtitle tracks.</returns>
    Task<TextSubtitleExtractionResult> ExtractTextSubtitlesAsync(
        string videoFilePath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the text subtitle formats available in a video file without extracting content.
    /// This method provides a quick way to check what subtitle formats are available.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file to analyze.</param>
    /// <param name="cancellationToken">Token to cancel the analysis operation.</param>
    /// <returns>List of available text subtitle formats with track information.</returns>
    Task<List<(int TrackIndex, string Language, SubtitleFormat Format)>> GetAvailableTextSubtitleFormatsAsync(
        string videoFilePath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts content from a specific text subtitle track by index.
    /// This method allows selective extraction of individual subtitle tracks.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file containing the subtitle track.</param>
    /// <param name="trackIndex">Zero-based index of the subtitle track to extract.</param>
    /// <param name="cancellationToken">Token to cancel the extraction operation.</param>
    /// <returns>The extracted text subtitle track, or null if extraction failed.</returns>
    Task<TextSubtitleTrack?> ExtractSpecificTrackAsync(
        string videoFilePath, 
        int trackIndex, 
        CancellationToken cancellationToken = default);
}
