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
    /// Parses raw subtitle content into structured subtitle data.
    /// This method converts the raw text content into a structured format
    /// with individual subtitle entries, timing, and metadata.
    /// </summary>
    /// <param name="content">Raw subtitle content to parse.</param>
    /// <param name="cancellationToken">Token to cancel the parsing operation.</param>
    /// <returns>Structured parsing result with subtitle entries and metadata.</returns>
    Task<SubtitleParsingResult> ParseContentAsync(
        string content, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the provided content is in the correct format for this handler.
    /// This method performs format validation without full parsing.
    /// </summary>
    /// <param name="content">Content to validate.</param>
    /// <returns>True if the content matches the expected format, false otherwise.</returns>
    bool CanHandle(string content);
}
