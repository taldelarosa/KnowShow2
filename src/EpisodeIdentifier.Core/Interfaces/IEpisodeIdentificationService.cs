using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for episode identification services that support multiple hashing methods.
/// Provides unified access to both legacy fuzzy hashing and CTPH fuzzy hashing based on configuration.
/// </summary>
public interface IEpisodeIdentificationService : IDisposable
{
    /// <summary>
    /// Identifies an episode using the most appropriate hashing method based on configuration.
    /// Uses CTPH fuzzy hashing if enabled and configured, otherwise falls back to legacy hashing.
    /// </summary>
    /// <param name="subtitleText">The subtitle text content to identify</param>
    /// <param name="sourceFilePath">Optional path to the source file (used for CTPH hashing)</param>
    /// <param name="minConfidence">Optional minimum confidence threshold</param>
    /// <returns>Episode identification result with match information</returns>
    Task<IdentificationResult> IdentifyEpisodeAsync(string subtitleText, string? sourceFilePath = null, double? minConfidence = null);
}