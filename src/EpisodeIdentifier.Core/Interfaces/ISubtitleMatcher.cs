using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for subtitle matching services that identify episodes based on subtitle content.
/// </summary>
public interface ISubtitleMatcher
{
    /// <summary>
    /// Identifies an episode using subtitle text content.
    /// </summary>
    /// <param name="subtitleText">The subtitle text content to match.</param>
    /// <param name="minConfidence">The minimum confidence threshold for matches. If null, uses configuration default.</param>
    /// <param name="seriesFilter">Optional series name to filter results (case-insensitive)</param>
    /// <param name="seasonFilter">Optional season number to filter results (requires seriesFilter)</param>
    /// <returns>The identification result containing match information.</returns>
    Task<IdentificationResult> IdentifyEpisode(string subtitleText, double? minConfidence = null, string? seriesFilter = null, int? seasonFilter = null);
}