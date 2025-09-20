namespace EpisodeIdentifier.Core.Constants;

/// <summary>
/// Centralized configuration constants and defaults for the async processing feature.
/// Provides single source of truth for configuration validation and defaults.
/// </summary>
public static class ConfigurationDefaults
{
    /// <summary>
    /// Constants related to concurrency configuration.
    /// </summary>
    public static class Concurrency
    {
        /// <summary>
        /// Minimum allowed concurrent operations (inclusive).
        /// </summary>
        public const int MIN = 1;

        /// <summary>
        /// Maximum allowed concurrent operations (inclusive).
        /// </summary>
        public const int MAX = 100;

        /// <summary>
        /// Default concurrent operations for backward compatibility.
        /// </summary>
        public const int DEFAULT = 1;

        /// <summary>
        /// Validation range description for error messages.
        /// </summary>
        public const string RANGE_DESCRIPTION = "1-100";
    }

    /// <summary>
    /// Default timeout values for processing operations.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>
        /// Default timeout for processing individual files.
        /// </summary>
        public static readonly TimeSpan DEFAULT_FILE_PROCESSING = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Default configuration reload check interval.
        /// </summary>
        public static readonly TimeSpan CONFIG_RELOAD_CHECK = TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Default progress reporting settings.
    /// </summary>
    public static class Progress
    {
        /// <summary>
        /// Default progress reporting interval in milliseconds.
        /// </summary>
        public const int DEFAULT_REPORTING_INTERVAL_MS = 1000;

        /// <summary>
        /// Minimum progress reporting interval to prevent spam.
        /// </summary>
        public const int MIN_REPORTING_INTERVAL_MS = 100;

        /// <summary>
        /// Maximum progress reporting interval for responsiveness.
        /// </summary>
        public const int MAX_REPORTING_INTERVAL_MS = 60000;
    }

    /// <summary>
    /// Batch processing defaults.
    /// </summary>
    public static class Batching
    {
        /// <summary>
        /// Default batch size for bulk operations.
        /// </summary>
        public const int DEFAULT_BATCH_SIZE = 100;

        /// <summary>
        /// Minimum batch size.
        /// </summary>
        public const int MIN_BATCH_SIZE = 1;

        /// <summary>
        /// Maximum batch size for memory management.
        /// </summary>
        public const int MAX_BATCH_SIZE = 10000;
    }
}