using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for parsing and handling specific text subtitle formats.
/// Each implementation handles a specific subtitle format (SRT, ASS, VTT).
/// </summary>
public interface ISubtitleFormatHandler
{
    /// <summary>
    /// The subtitle format that this handler supports.
    /// </summary>
    SubtitleFormat SupportedFormat { get; }

    /// <summary>
    /// Parses subtitle content from a stream with optional encoding specification.
    /// This method converts the raw text content into a structured format
    /// with individual subtitle entries, timing, and metadata.
    /// </summary>
    /// <param name="stream">Stream containing subtitle content to parse.</param>
    /// <param name="encoding">Text encoding to use for reading the stream. If null, UTF-8 is used.</param>
    /// <param name="cancellationToken">Token to cancel the parsing operation.</param>
    /// <returns>Structured parsing result with subtitle entries and metadata.</returns>
    Task<SubtitleParsingResult> ParseSubtitleTextAsync(
        Stream stream, 
        string? encoding = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the provided content is in the correct format for this handler.
    /// This method performs format validation without full parsing.
    /// </summary>
    /// <param name="content">Content to validate.</param>
    /// <returns>True if the content matches the expected format, false otherwise.</returns>
    bool CanHandle(string content);
}
