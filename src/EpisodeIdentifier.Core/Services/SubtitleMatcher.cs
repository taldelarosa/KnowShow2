using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleMatcher
{
    private readonly FuzzyHashService _hashService;
    private readonly ILogger<SubtitleMatcher> _logger;

    public SubtitleMatcher(FuzzyHashService hashService, ILogger<SubtitleMatcher> logger)
    {
        _hashService = hashService;
        _logger = logger;
    }

    public async Task<IdentificationResult> IdentifyEpisode(string subtitleText, double minConfidence = 0.8)
    {
        _logger.LogInformation("Attempting to identify episode using subtitle text");

        var matches = await _hashService.FindMatches(subtitleText, minConfidence);
        
        if (!matches.Any())
        {
            _logger.LogInformation("No matches found above confidence threshold {Threshold}", minConfidence);
            return new IdentificationResult 
            { 
                MatchConfidence = 0,
                Error = IdentificationError.NoSubtitlesFound
            };
        }

        var bestMatch = matches.First();
        var result = new IdentificationResult
        {
            Series = bestMatch.Subtitle.Series,
            Season = bestMatch.Subtitle.Season,
            Episode = bestMatch.Subtitle.Episode,
            MatchConfidence = bestMatch.Confidence
        };

        // Check for ambiguous results
        if (matches.Count > 1 && matches.Skip(1).Any(m => m.Confidence > minConfidence))
        {
            result.AmbiguityNotes = $"Multiple episodes matched with confidence > {minConfidence}";
            _logger.LogWarning("Ambiguous match found: {Notes}", result.AmbiguityNotes);
        }

        return result;
    }
}
