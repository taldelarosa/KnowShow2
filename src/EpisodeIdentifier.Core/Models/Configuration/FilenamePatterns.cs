using System.ComponentModel.DataAnnotations;
using FluentValidation;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Entity containing regex patterns for parsing episode information from filenames.
/// Validates pattern syntax and provides episode data extraction capabilities.
/// </summary>
public class FilenamePatterns
{
    /// <summary>
    /// Primary regex pattern for episode identification.
    /// Must contain named capture groups: SeriesName, Season, Episode.
    /// Used as the main parsing strategy before fallback patterns.
    /// </summary>
    [Required]
    public string PrimaryPattern { get; set; } = string.Empty;

    /// <summary>
    /// Additional regex patterns for fallback matching.
    /// Each pattern should contain named capture groups for episode data.
    /// Applied when PrimaryPattern fails to match.
    /// </summary>
    public List<string> FallbackPatterns { get; set; } = new();

    /// <summary>
    /// Pattern for extracting series name from filename.
    /// Used when episode patterns don't include series name extraction.
    /// Should contain SeriesName named capture group.
    /// </summary>
    public string? SeriesNamePattern { get; set; }

    /// <summary>
    /// Pattern for season/episode number parsing.
    /// Used for specialized season-episode extraction scenarios.
    /// Should contain Season and Episode named capture groups.
    /// </summary>
    public string? SeasonEpisodePattern { get; set; }

    /// <summary>
    /// Validates if all patterns are syntactically correct regex patterns.
    /// </summary>
    /// <returns>True if all patterns are valid, false otherwise.</returns>
    public bool AreAllPatternsValid()
    {
        try
        {
            // Test primary pattern
            if (!string.IsNullOrEmpty(PrimaryPattern))
            {
                _ = new Regex(PrimaryPattern, RegexOptions.Compiled);
            }

            // Test fallback patterns
            foreach (var pattern in FallbackPatterns)
            {
                if (!string.IsNullOrEmpty(pattern))
                {
                    _ = new Regex(pattern, RegexOptions.Compiled);
                }
            }

            // Test series name pattern
            if (!string.IsNullOrEmpty(SeriesNamePattern))
            {
                _ = new Regex(SeriesNamePattern, RegexOptions.Compiled);
            }

            // Test season episode pattern
            if (!string.IsNullOrEmpty(SeasonEpisodePattern))
            {
                _ = new Regex(SeasonEpisodePattern, RegexOptions.Compiled);
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates if the primary pattern contains required named capture groups.
    /// </summary>
    /// <returns>True if required groups are present, false otherwise.</returns>
    public bool HasRequiredCaptureGroups()
    {
        try
        {
            if (string.IsNullOrEmpty(PrimaryPattern))
                return false;

            var regex = new Regex(PrimaryPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var groupNames = regex.GetGroupNames().Select(n => n.ToLowerInvariant()).ToHashSet();

            // Accept common legacy aliases for compatibility
            var requiredGroups = new[]
            {
                new[] { "seriesname", "series" },
                new[] { "season" },
                new[] { "episode" }
            };

            bool HasAny(string[] candidates) => candidates.Any(c => groupNames.Contains(c));

            return requiredGroups.All(HasAny);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all unique group names from all patterns.
    /// Used for pattern analysis and validation.
    /// </summary>
    /// <returns>Collection of all capture group names across patterns.</returns>
    public IEnumerable<string> GetAllCaptureGroups()
    {
        var groupNames = new HashSet<string>();

        try
        {
            // Primary pattern groups
            if (!string.IsNullOrEmpty(PrimaryPattern))
            {
                var regex = new Regex(PrimaryPattern, RegexOptions.Compiled);
                foreach (var name in regex.GetGroupNames().Where(name => name != "0"))
                {
                    groupNames.Add(name);
                }
            }

            // Fallback pattern groups
            foreach (var pattern in FallbackPatterns.Where(p => !string.IsNullOrEmpty(p)))
            {
                var regex = new Regex(pattern, RegexOptions.Compiled);
                foreach (var name in regex.GetGroupNames().Where(name => name != "0"))
                {
                    groupNames.Add(name);
                }
            }

            // Series name pattern groups
            if (!string.IsNullOrEmpty(SeriesNamePattern))
            {
                var regex = new Regex(SeriesNamePattern, RegexOptions.Compiled);
                foreach (var name in regex.GetGroupNames().Where(name => name != "0"))
                {
                    groupNames.Add(name);
                }
            }

            // Season episode pattern groups
            if (!string.IsNullOrEmpty(SeasonEpisodePattern))
            {
                var regex = new Regex(SeasonEpisodePattern, RegexOptions.Compiled);
                foreach (var name in regex.GetGroupNames().Where(name => name != "0"))
                {
                    groupNames.Add(name);
                }
            }
        }
        catch (ArgumentException)
        {
            // Return empty collection if any pattern is invalid
            return Enumerable.Empty<string>();
        }

        return groupNames;
    }

    /// <summary>
    /// Creates a deep copy of this FilenamePatterns instance.
    /// Used for configuration hot-reloading scenarios.
    /// </summary>
    /// <returns>New FilenamePatterns instance with copied values.</returns>
    public FilenamePatterns Clone()
    {
        return new FilenamePatterns
        {
            PrimaryPattern = PrimaryPattern,
            FallbackPatterns = new List<string>(FallbackPatterns),
            SeriesNamePattern = SeriesNamePattern,
            SeasonEpisodePattern = SeasonEpisodePattern
        };
    }
}

/// <summary>
/// FluentValidation validator for FilenamePatterns entity.
/// Implements regex syntax validation and capture group requirements.
/// </summary>
public class FilenamesPatternsValidator : AbstractValidator<FilenamePatterns>
{
    public FilenamesPatternsValidator()
    {
        RuleFor(x => x.PrimaryPattern)
            .NotEmpty()
            .WithMessage("PrimaryPattern is required")
            .Must(BeValidRegexPattern)
            .WithMessage("PrimaryPattern must be a valid regex pattern");

        RuleFor(x => x)
            .Must(x => x.HasRequiredCaptureGroups())
            .WithMessage("PrimaryPattern must contain named capture groups: SeriesName, Season, Episode")
            .WithName("PrimaryPattern");

        RuleForEach(x => x.FallbackPatterns)
            .Must(BeValidRegexPattern!)
            .WithMessage("Each fallback pattern must be a valid regex pattern")
            .When(x => x.FallbackPatterns.Any());

        RuleFor(x => x.SeriesNamePattern)
            .Must(BeValidRegexPattern!)
            .WithMessage("SeriesNamePattern must be a valid regex pattern")
            .When(x => !string.IsNullOrEmpty(x.SeriesNamePattern));

        RuleFor(x => x.SeasonEpisodePattern)
            .Must(BeValidRegexPattern!)
            .WithMessage("SeasonEpisodePattern must be a valid regex pattern")
            .When(x => !string.IsNullOrEmpty(x.SeasonEpisodePattern));

        RuleFor(x => x)
            .Must(x => x.AreAllPatternsValid())
            .WithMessage("All regex patterns must be syntactically valid")
            .WithName("Patterns");
    }

    private static bool BeValidRegexPattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true; // Optional patterns can be empty

        try
        {
            _ = new Regex(pattern, RegexOptions.Compiled);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Result container for filename pattern matching operations.
/// </summary>
public class PatternMatchResult
{
    public bool IsMatch { get; set; }
    public string? PatternUsed { get; set; }
    public Dictionary<string, string> CaptureGroups { get; set; } = new();
    public string? SeriesName { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? OriginalFilename { get; set; }

    public static PatternMatchResult Success(
        string patternUsed,
        Dictionary<string, string> captureGroups,
        string originalFilename)
    {
        var result = new PatternMatchResult
        {
            IsMatch = true,
            PatternUsed = patternUsed,
            CaptureGroups = captureGroups,
            OriginalFilename = originalFilename
        };

        // Extract standard episode information
        if (captureGroups.TryGetValue("SeriesName", out var seriesName))
        {
            result.SeriesName = seriesName.Trim();
        }

        if (captureGroups.TryGetValue("Season", out var seasonStr) &&
            int.TryParse(seasonStr, out var season))
        {
            result.Season = season;
        }

        if (captureGroups.TryGetValue("Episode", out var episodeStr) &&
            int.TryParse(episodeStr, out var episode))
        {
            result.Episode = episode;
        }

        return result;
    }

    public static PatternMatchResult NoMatch(string originalFilename)
    {
        return new PatternMatchResult
        {
            IsMatch = false,
            OriginalFilename = originalFilename
        };
    }

    /// <summary>
    /// Validates that essential episode information was extracted.
    /// </summary>
    /// <returns>True if SeriesName, Season, and Episode are all available.</returns>
    public bool HasEssentialEpisodeData()
    {
        return !string.IsNullOrWhiteSpace(SeriesName) && Season.HasValue && Episode.HasValue;
    }
}
