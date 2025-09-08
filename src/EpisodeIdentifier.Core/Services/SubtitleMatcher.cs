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
            
            // Get the best match regardless of threshold for error reporting
            var bestOverallMatch = await _hashService.GetBestMatch(subtitleText);
            
            if (bestOverallMatch.HasValue)
            {
                var (subtitle, confidence) = bestOverallMatch.Value;
                var errorMessage = $"No matching episodes found in the database with sufficient confidence. Best match: {subtitle.Series} S{subtitle.Season}E{subtitle.Episode} ({confidence:P1} confidence, below {minConfidence:P0} threshold)";
                
                return new IdentificationResult 
                { 
                    MatchConfidence = confidence,
                    Error = new IdentificationError 
                    { 
                        Code = "NO_MATCHES_FOUND", 
                        Message = errorMessage 
                    }
                };
            }
            else
            {
                return new IdentificationResult 
                { 
                    MatchConfidence = 0,
                    Error = IdentificationError.NoMatchesFound
                };
            }
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
            
            // Log all close matches for debugging
            foreach (var match in matches.Take(5)) // Show top 5 matches
            {
                _logger.LogInformation("Close match: {Series} S{Season}E{Episode} - {Confidence:P2} confidence", 
                    match.Subtitle.Series, match.Subtitle.Season, match.Subtitle.Episode, match.Confidence);
            }
        }

        return result;
    }
}
