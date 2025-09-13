using EpisodeIdentifier.Core.Models.Configuration;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for configuration management with hot-reload support.
/// This is the new configuration interface for the fuzzy hashing plus system.
/// </summary>
public interface IFuzzyConfigurationService
{
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