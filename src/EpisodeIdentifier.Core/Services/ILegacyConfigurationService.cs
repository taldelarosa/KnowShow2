using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Application configuration service interface with concurrency control.
/// Provides both legacy compatibility and modern configuration management with MaxConcurrency support.
/// </summary>
public interface IAppConfigService
{
    /// <summary>
    /// Legacy configuration property for backward compatibility.
    /// </summary>
    AppConfig Config { get; }

    /// <summary>
    /// Gets the maximum number of concurrent operations allowed.
    /// This value is read from the configuration and validated to be within the range 1-100.
    /// Defaults to 1 if not specified or invalid.
    /// </summary>
    int MaxConcurrency { get; }

    /// <summary>
    /// Gets the most recent configuration load result, if any.
    /// Provides metadata about parsing/clamping to help consumers choose safe defaults.
    /// </summary>
    ConfigurationResult? LastConfigurationResult { get; }

    // Legacy methods for backward compatibility
    Task LoadConfigurationAsync(string? configPath = null);
    Task SaveConfigurationAsync(string? configPath = null);

    // Modern configuration methods for new concurrent processing system
    /// <summary>
    /// Loads configuration from the default config file.
    /// </summary>
    /// <returns>Configuration result containing the loaded config or validation errors.</returns>
    Task<ConfigurationResult> LoadConfiguration();

    /// <summary>
    /// Reloads configuration if the underlying file has changed.
    /// </summary>
    /// <returns>True if configuration was reloaded, false if no changes detected.</returns>
    Task<bool> ReloadIfChanged();

    /// <summary>
    /// Validates a configuration object against business rules.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    ConfigurationResult ValidateConfiguration(Configuration config);
}
