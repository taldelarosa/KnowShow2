using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Primary configuration entity that encapsulates all system settings.
/// Contains validation rules and supports hot-reloading during file processing.
/// </summary>
public class Configuration
{
    /// <summary>
    /// Configuration schema version for migration support.
    /// Must be a valid semantic version string.
    /// </summary>
    [Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Minimum confidence threshold for episode matches (0.0-1.0).
    /// Cannot exceed RenameConfidenceThreshold.
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal MatchConfidenceThreshold { get; set; }

    /// <summary>
    /// Minimum confidence threshold for file renaming (0.0-1.0).
    /// Must be greater than or equal to MatchConfidenceThreshold.
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal RenameConfidenceThreshold { get; set; }

    /// <summary>
    /// CTPH similarity threshold for fuzzy matching (0-100).
    /// Required when HashingAlgorithm is CTPH.
    /// </summary>
    [Range(0, 100)]
    public int FuzzyHashThreshold { get; set; }

    /// <summary>
    /// Active hashing algorithm selection.
    /// Determines which hashing service implementation to use.
    /// </summary>
    public HashingAlgorithm HashingAlgorithm { get; set; }

    /// <summary>
    /// Regex patterns for parsing episode filenames.
    /// Required for episode identification workflow.
    /// </summary>
    [Required]
    public FilenamePatterns FilenamePatterns { get; set; } = new();

    /// <summary>
    /// Template for generating new filenames after identification.
    /// Must contain required placeholders: {SeriesName}, {Season}, {Episode}.
    /// </summary>
    [Required]
    public string FilenameTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this configuration was last loaded.
    /// Used for hot-reload change detection.
    /// </summary>
    public DateTime LastLoaded { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source file path for this configuration.
    /// Used for hot-reload file monitoring.
    /// </summary>
    public string? SourceFilePath { get; set; }
}

/// <summary>
/// Enumeration defining supported hashing methods.
/// </summary>
public enum HashingAlgorithm
{
    /// <summary>
    /// Legacy MD5 hashing (backward compatibility).
    /// </summary>
    MD5 = 0,

    /// <summary>
    /// Legacy SHA1 hashing (backward compatibility).
    /// </summary>
    SHA1 = 1,

    /// <summary>
    /// Context-triggered piecewise hashing (fuzzy matching).
    /// </summary>
    CTPH = 2
}

/// <summary>
/// FluentValidation validator for Configuration entity.
/// Implements all validation rules from the data model specification.
/// </summary>
public class ConfigurationValidator : AbstractValidator<Configuration>
{
    public ConfigurationValidator()
    {
        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required")
            .Must(BeValidSemanticVersion)
            .WithMessage("Version must be a valid semantic version (e.g., '2.0', '1.2.3')");

        RuleFor(x => x.MatchConfidenceThreshold)
            .InclusiveBetween(0.0m, 1.0m)
            .WithMessage("MatchConfidenceThreshold must be between 0.0 and 1.0");

        RuleFor(x => x.RenameConfidenceThreshold)
            .InclusiveBetween(0.0m, 1.0m)
            .WithMessage("RenameConfidenceThreshold must be between 0.0 and 1.0");

        RuleFor(x => x)
            .Must(x => x.RenameConfidenceThreshold >= x.MatchConfidenceThreshold)
            .WithMessage("RenameConfidenceThreshold must be greater than or equal to MatchConfidenceThreshold")
            .WithName("RenameConfidenceThreshold");

        RuleFor(x => x.FuzzyHashThreshold)
            .InclusiveBetween(0, 100)
            .WithMessage("FuzzyHashThreshold must be between 0 and 100");

        When(x => x.HashingAlgorithm == HashingAlgorithm.CTPH, () =>
        {
            RuleFor(x => x.FuzzyHashThreshold)
                .GreaterThan(0)
                .WithMessage("FuzzyHashThreshold is required when HashingAlgorithm is CTPH");
        });

        RuleFor(x => x.HashingAlgorithm)
            .IsInEnum()
            .WithMessage("HashingAlgorithm must be a valid value (MD5, SHA1, or CTPH)");

        RuleFor(x => x.FilenamePatterns)
            .NotNull()
            .WithMessage("FilenamePatterns is required")
            .SetValidator(new FilenamesPatternsValidator());

        RuleFor(x => x.FilenameTemplate)
            .NotEmpty()
            .WithMessage("FilenameTemplate is required")
            .Must(ContainRequiredPlaceholders)
            .WithMessage("FilenameTemplate must contain required placeholders: {SeriesName}, {Season}, {Episode}");
    }

    private static bool BeValidSemanticVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Simple semantic version validation (Major.Minor.Patch or Major.Minor)
        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
            return false;

        return parts.All(part => int.TryParse(part, out var number) && number >= 0);
    }

    private static bool ContainRequiredPlaceholders(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return false;

        var requiredPlaceholders = new[] { "{SeriesName}", "{Season}", "{Episode}" };
        return requiredPlaceholders.All(placeholder => template.Contains(placeholder));
    }
}

/// <summary>
/// Validation result container for configuration validation operations.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }

    public static ValidationResult Failure(IEnumerable<string> errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}

/// <summary>
/// Result container for configuration loading operations.
/// </summary>
public class ConfigurationResult
{
    public bool IsValid { get; set; }
    public Configuration? Configuration { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ConfigurationResult Success(Configuration configuration)
    {
        return new ConfigurationResult
        {
            IsValid = true,
            Configuration = configuration
        };
    }

    public static ConfigurationResult Failure(params string[] errors)
    {
        return new ConfigurationResult
        {
            IsValid = false,
            Configuration = null,
            Errors = errors.ToList()
        };
    }

    public static ConfigurationResult Failure(IEnumerable<string> errors)
    {
        return new ConfigurationResult
        {
            IsValid = false,
            Configuration = null,
            Errors = errors.ToList()
        };
    }
}