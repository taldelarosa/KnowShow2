using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Utility class for selecting the best PGS subtitle track from available tracks
/// </summary>
public static class PgsTrackSelector
{
    /// <summary>
    /// Selects the best PGS subtitle track based on language preferences
    /// </summary>
    /// <param name="tracks">Available subtitle tracks</param>
    /// <param name="preferredLanguage">Preferred language code (optional)</param>
    /// <returns>The best matching subtitle track</returns>
    /// <exception cref="ArgumentException">Thrown when no tracks are provided</exception>
    public static SubtitleTrackInfo SelectBestTrack(List<SubtitleTrackInfo> tracks, string? preferredLanguage = null)
    {
        if (tracks == null || !tracks.Any())
        {
            throw new ArgumentException("At least one subtitle track must be provided", nameof(tracks));
        }

        // If preferred language specified, try to find it
        if (!string.IsNullOrEmpty(preferredLanguage))
        {
            var langTrack = tracks.FirstOrDefault(t => 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (langTrack != null)
            {
                return langTrack;
            }
        }

        // Default preferences: English first, then first available
        var englishTrack = tracks.FirstOrDefault(t => 
            string.Equals(t.Language, "eng", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Language, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Language, "english", StringComparison.OrdinalIgnoreCase));
        
        return englishTrack ?? tracks.First();
    }
}