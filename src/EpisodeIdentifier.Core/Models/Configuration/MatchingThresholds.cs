using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Enumeration for subtitle types to clarify which threshold to use.
/// </summary>
public enum SubtitleType
{
    /// <summary>
    /// Text-based subtitles (SRT, ASS, WebVTT, etc.) - Highest accuracy
    /// </summary>
    TextBased,

    /// <summary>
    /// PGS (Presentation Graphic Stream) subtitles requiring OCR - Medium accuracy
    /// </summary>
    PGS,

    /// <summary>
    /// DVD/VobSub subtitles requiring OCR - Lower accuracy
    /// </summary>
    VobSub
}

/// <summary>
/// Configuration for matching thresholds specific to a subtitle type.
/// Combines both match confidence (0.0-1.0) and fuzzy hash similarity (0-100) into one coherent structure.
/// </summary>
public class SubtitleTypeThresholds
{
    /// <summary>
    /// Minimum confidence threshold for episode matches (0.0-1.0).
    /// This is the final confidence score after fuzzy hash comparison.
    /// For CTPH hashing, this is calculated from the fuzzy hash similarity (0-100) divided by 100.
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal MatchConfidence { get; set; }

    /// <summary>
    /// Minimum confidence threshold for file renaming (0.0-1.0).
    /// Must be greater than or equal to MatchConfidence.
    /// Only matches above this threshold will trigger automatic file renaming.
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal RenameConfidence { get; set; }

    /// <summary>
    /// Minimum fuzzy hash similarity score for matches (0-100).
    /// For CTPH hashing, this is the raw similarity percentage.
    /// This value is converted to confidence (0.0-1.0) by dividing by 100.
    /// Higher values mean stricter matching (fewer false positives).
    /// Lower values mean more lenient matching (more potential matches, but may include false positives).
    /// </summary>
    [Range(0, 100)]
    public int FuzzyHashSimilarity { get; set; }
}

/// <summary>
/// Configuration for matching thresholds across all subtitle types.
/// Each subtitle type can have different thresholds since OCR accuracy varies.
/// </summary>
public class MatchingThresholds
{
    /// <summary>
    /// Thresholds for text-based subtitles (SRT, ASS, WebVTT).
    /// These are the most accurate and should have the highest thresholds.
    /// Recommended: MatchConfidence=0.7, RenameConfidence=0.8, FuzzyHashSimilarity=70
    /// </summary>
    [Required]
    public SubtitleTypeThresholds TextBased { get; set; } = new();

    /// <summary>
    /// Thresholds for PGS (Presentation Graphic Stream) subtitles.
    /// These require OCR and may have some errors.
    /// Recommended: MatchConfidence=0.6, RenameConfidence=0.7, FuzzyHashSimilarity=60
    /// </summary>
    [Required]
    public SubtitleTypeThresholds PGS { get; set; } = new();

    /// <summary>
    /// Thresholds for DVD/VobSub subtitles.
    /// These require OCR and typically have lower quality.
    /// Recommended: MatchConfidence=0.5, RenameConfidence=0.6, FuzzyHashSimilarity=50
    /// </summary>
    [Required]
    public SubtitleTypeThresholds VobSub { get; set; } = new();

    /// <summary>
    /// Get the appropriate thresholds for a given subtitle type.
    /// </summary>
    public SubtitleTypeThresholds GetThresholdsForType(SubtitleType type)
    {
        return type switch
        {
            SubtitleType.TextBased => TextBased,
            SubtitleType.PGS => PGS,
            SubtitleType.VobSub => VobSub,
            _ => throw new ArgumentException($"Unknown subtitle type: {type}", nameof(type))
        };
    }
}

/// <summary>
/// FluentValidation validator for SubtitleTypeThresholds.
/// </summary>
public class SubtitleTypeThresholdsValidator : AbstractValidator<SubtitleTypeThresholds>
{
    public SubtitleTypeThresholdsValidator()
    {
        RuleFor(x => x.MatchConfidence)
            .InclusiveBetween(0.0m, 1.0m)
            .WithMessage("MatchConfidence must be between 0.0 and 1.0");

        RuleFor(x => x.RenameConfidence)
            .InclusiveBetween(0.0m, 1.0m)
            .WithMessage("RenameConfidence must be between 0.0 and 1.0");

        RuleFor(x => x)
            .Must(x => x.RenameConfidence >= x.MatchConfidence)
            .WithMessage("RenameConfidence must be greater than or equal to MatchConfidence")
            .WithName("RenameConfidence");

        RuleFor(x => x.FuzzyHashSimilarity)
            .InclusiveBetween(0, 100)
            .WithMessage("FuzzyHashSimilarity must be between 0 and 100")
            .GreaterThan(0)
            .WithMessage("FuzzyHashSimilarity must be greater than 0");
    }
}

/// <summary>
/// FluentValidation validator for MatchingThresholds.
/// </summary>
public class MatchingThresholdsValidator : AbstractValidator<MatchingThresholds>
{
    public MatchingThresholdsValidator()
    {
        RuleFor(x => x.TextBased)
            .NotNull()
            .WithMessage("TextBased thresholds are required")
            .SetValidator(new SubtitleTypeThresholdsValidator());

        RuleFor(x => x.PGS)
            .NotNull()
            .WithMessage("PGS thresholds are required")
            .SetValidator(new SubtitleTypeThresholdsValidator());

        RuleFor(x => x.VobSub)
            .NotNull()
            .WithMessage("VobSub thresholds are required")
            .SetValidator(new SubtitleTypeThresholdsValidator());
    }
}
