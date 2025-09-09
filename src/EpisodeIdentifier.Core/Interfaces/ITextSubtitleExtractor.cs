using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for extracting text subtitle tracks from video files.
/// Handles the initial extraction phase of the nonPGS workflow.
/// </summary>
public interface ITextSubtitleExtractor
{
    /// <summary>
    /// Detects available text subtitle tracks from a video file.
    /// Uses FFmpeg to analyze the file and identify text-based subtitle streams.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file to analyze</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>List of detected text subtitle tracks</returns>
    Task<IReadOnlyList<TextSubtitleTrack>> DetectTextSubtitleTracksAsync(
        string videoFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text content from a specific subtitle track.
    /// Uses FFmpeg to extract the subtitle stream and convert to text format.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file</param>
    /// <param name="track">The subtitle track to extract</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Extraction result with track info and extracted content</returns>
    Task<TextSubtitleExtractionResult> ExtractTextSubtitleContentAsync(
        string videoFilePath,
        TextSubtitleTrack track,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to extract all available text subtitle tracks from a video file.
    /// This is the main entry point for the nonPGS workflow when multiple tracks are available.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Extraction result containing all successfully extracted tracks</returns>
    Task<TextSubtitleExtractionResult> TryExtractAllTextSubtitlesAsync(
        string videoFilePath,
        CancellationToken cancellationToken = default);
}
