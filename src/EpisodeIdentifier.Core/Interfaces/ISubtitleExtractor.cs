using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for subtitle extraction services that extract subtitle data from video files.
/// </summary>
public interface ISubtitleExtractor
{
    /// <summary>
    /// Extracts PGS subtitle data from a video file.
    /// </summary>
    /// <param name="videoPath">The path to the video file.</param>
    /// <param name="preferredLanguage">The preferred language for subtitle extraction (optional).</param>
    /// <returns>The extracted PGS subtitle data as a byte array.</returns>
    Task<byte[]> ExtractPgsSubtitles(string videoPath, string? preferredLanguage = null);

    /// <summary>
    /// Extracts and converts subtitle data to text format.
    /// </summary>
    /// <param name="videoPath">The path to the video file.</param>
    /// <param name="preferredLanguage">The preferred language for subtitle extraction (optional).</param>
    /// <returns>The extracted and converted subtitle text.</returns>
    Task<string> ExtractAndConvertSubtitles(string videoPath, string? preferredLanguage = null);
}
