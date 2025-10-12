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
    /// Maximum number of concurrent episode identification operations.
    /// Range: 1-100, Default: 1 for backward compatibility.
    /// Controls how many files can be processed simultaneously during bulk operations.
    /// Values outside the range are automatically clamped during loading.
    /// </summary>
    // Validation attribute ensures contract tests recognize the expected range; service also clamps on initial loads
    [Range(1, 100)]
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Configuration settings for bulk processing operations.
    /// Optional - when null, defaults are used.
    /// </summary>
    public BulkProcessingConfiguration? BulkProcessing { get; set; }

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
    /// Context-triggered piecewise hashing (fuzzy matching).
    /// </summary>
    CTPH = 0
}

/// <summary>
/// Configuration settings for bulk processing operations.
/// </summary>
public class BulkProcessingConfiguration
{
    /// <summary>
    /// Default batch size for processing files.
    /// Range: 1-10000, Default: 100.
    /// </summary>
    [Range(1, 10000)]
    public int DefaultBatchSize { get; set; } = 100;

    /// <summary>
    /// Default maximum number of concurrent processing tasks.
    /// Range: 1-100, Default: CPU core count.
    /// </summary>
    [Range(1, 100)]
    public int DefaultMaxConcurrency { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Default progress reporting interval in milliseconds.
    /// Range: 100-60000, Default: 1000ms.
    /// </summary>
    [Range(100, 60000)]
    public int DefaultProgressReportingInterval { get; set; } = 1000;

    /// <summary>
    /// Whether to force garbage collection between batches by default.
    /// Default: true for memory optimization.
    /// </summary>
    public bool DefaultForceGarbageCollection { get; set; } = true;

    /// <summary>
    /// Whether to create backups before processing files by default.
    /// Default: false for performance.
    /// </summary>
    public bool DefaultCreateBackups { get; set; } = false;

    /// <summary>
    /// Whether to continue processing when individual files fail by default.
    /// Default: true for maximum throughput.
    /// </summary>
    public bool DefaultContinueOnError { get; set; } = true;

    /// <summary>
    /// Default maximum number of errors before aborting processing.
    /// Null means no limit. Range: 1-100000.
    /// </summary>
    [Range(1, 100000)]
    public int? DefaultMaxErrorsBeforeAbort { get; set; } = null;

    /// <summary>
    /// Default timeout for processing individual files.
    /// Null means no timeout. Range: 1 second to 1 hour.
    /// </summary>
    public TimeSpan? DefaultFileProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default file extensions to include in bulk processing.
    /// Empty list uses system defaults.
    /// </summary>
    public List<string> DefaultFileExtensions { get; set; } = new();

    /// <summary>
    /// Maximum allowed batch size for validation.
    /// Range: 1-50000, Default: 10000.
    /// </summary>
    [Range(1, 50000)]
    public int MaxBatchSize { get; set; } = 10000;

    /// <summary>
    /// Maximum allowed concurrency for validation.
    /// Range: 1-500, Default: CPU cores * 8.
    /// </summary>
    [Range(1, 500)]
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 8;

    /// <summary>
    /// Whether to enable detailed batch statistics logging.
    /// Default: false for performance.
    /// </summary>
    public bool EnableBatchStatistics { get; set; } = false;

    /// <summary>
    /// Whether to enable memory usage monitoring during processing.
    /// Default: true for stability.
    /// </summary>
    public bool EnableMemoryMonitoring { get; set; } = true;
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
            .WithMessage("HashingAlgorithm must be CTPH");

        RuleFor(x => x.FilenamePatterns)
            .NotNull()
            .WithMessage("FilenamePatterns is required")
            .SetValidator(new FilenamesPatternsValidator());

        RuleFor(x => x.FilenameTemplate)
            .NotEmpty()
            .WithMessage("FilenameTemplate is required")
            .Must(ContainRequiredPlaceholders)
            .WithMessage("FilenameTemplate must contain required placeholders: {SeriesName}, {Season}, {Episode}");

        // Concurrent processing validation
        // Values outside 1-100 are invalid at the validation layer; initial loads may clamp before validation
        RuleFor(x => x.MaxConcurrency)
            .InclusiveBetween(1, 100)
            .WithMessage("MaxConcurrency must be between 1 and 100");

        // Bulk processing configuration validation rules
        When(x => x.BulkProcessing != null, () =>
        {
            RuleFor(x => x.BulkProcessing!.DefaultBatchSize)
                .InclusiveBetween(1, 10000)
                .WithMessage("BulkProcessing.DefaultBatchSize must be between 1 and 10000");

            RuleFor(x => x.BulkProcessing!.DefaultMaxConcurrency)
                .InclusiveBetween(1, 100)
                .WithMessage("BulkProcessing.DefaultMaxConcurrency must be between 1 and 100");

            RuleFor(x => x.BulkProcessing!.DefaultProgressReportingInterval)
                .InclusiveBetween(100, 60000)
                .WithMessage("BulkProcessing.DefaultProgressReportingInterval must be between 100 and 60000 milliseconds");

            When(x => x.BulkProcessing!.DefaultMaxErrorsBeforeAbort.HasValue, () =>
            {
                RuleFor(x => x.BulkProcessing!.DefaultMaxErrorsBeforeAbort!.Value)
                    .InclusiveBetween(1, 100000)
                    .WithMessage("BulkProcessing.DefaultMaxErrorsBeforeAbort must be between 1 and 100000");
            });

            When(x => x.BulkProcessing!.DefaultFileProcessingTimeout.HasValue, () =>
            {
                RuleFor(x => x.BulkProcessing!.DefaultFileProcessingTimeout!.Value)
                    .GreaterThanOrEqualTo(TimeSpan.FromSeconds(1))
                    .WithMessage("BulkProcessing.DefaultFileProcessingTimeout must be at least 1 second")
                    .LessThanOrEqualTo(TimeSpan.FromHours(1))
                    .WithMessage("BulkProcessing.DefaultFileProcessingTimeout must be at most 1 hour");
            });

            RuleFor(x => x.BulkProcessing!.MaxBatchSize)
                .InclusiveBetween(1, 50000)
                .WithMessage("BulkProcessing.MaxBatchSize must be between 1 and 50000");

            RuleFor(x => x.BulkProcessing!.MaxConcurrency)
                .InclusiveBetween(1, 500)
                .WithMessage("BulkProcessing.MaxConcurrency must be between 1 and 500");

            RuleFor(x => x.BulkProcessing!)
                .Must(x => x.DefaultBatchSize <= x.MaxBatchSize)
                .WithMessage("BulkProcessing.DefaultBatchSize must not exceed MaxBatchSize")
                .WithName("DefaultBatchSize");

            RuleFor(x => x.BulkProcessing!)
                .Must(x => x.DefaultMaxConcurrency <= x.MaxConcurrency)
                .WithMessage("BulkProcessing.DefaultMaxConcurrency must not exceed MaxConcurrency")
                .WithName("DefaultMaxConcurrency");
        });
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

        // Check for placeholders with optional format specifiers (e.g., {Season:D2})
        var requiredPlaceholders = new[] { "SeriesName", "Season", "Episode" };
        return requiredPlaceholders.All(placeholder =>
            template.Contains($"{{{placeholder}}}") || template.Contains($"{{{placeholder}:"));
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
    // Metadata to aid consumers in making safe choices
    // Captures the originally provided MaxConcurrency value before any clamping/fallback
    public int? OriginalMaxConcurrency { get; set; }
    // True if the originally provided MaxConcurrency was outside [1,100]
    public bool OriginalMaxConcurrencyOutOfRange { get; set; }
    // True if MaxConcurrency was clamped to fit the valid range
    public bool WasMaxConcurrencyClamped { get; set; }
    // True if MaxConcurrency was forcibly defaulted to 1 (explicit fallback)
    public bool WasMaxConcurrencyDefaulted { get; set; }
    // Parsing/flow hints
    public bool WasLenientParse { get; set; }
    public bool WasReloadOperation { get; set; }

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
