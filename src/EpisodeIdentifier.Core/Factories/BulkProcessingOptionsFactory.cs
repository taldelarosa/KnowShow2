using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Core.Factories;

/// <summary>
/// Factory class for creating BulkProcessingOptions instances with proper configuration integration.
/// Provides consistent creation patterns and error handling for bulk processing scenarios.
/// </summary>
public static class BulkProcessingOptionsFactory
{
    /// <summary>
    /// Creates a BulkProcessingOptions instance with configuration-based MaxConcurrency.
    /// Handles configuration loading failures gracefully with fallback to safe defaults.
    /// </summary>
    /// <param name="configService">Configuration service for reading MaxConcurrency setting.</param>
    /// <returns>BulkProcessingOptions instance with validated MaxConcurrency value.</returns>
    public static async Task<BulkProcessingOptions> CreateFromConfigAsync(IAppConfigService configService)
    {
        try
        {
            // Load configuration to ensure fresh settings
            await configService.LoadConfiguration();

            // Create options with configuration-based concurrency
            // The configService.MaxConcurrency property already handles validation and fallback
            return new BulkProcessingOptions 
            { 
                MaxConcurrency = configService.MaxConcurrency 
            };
        }
        catch
        {
            // If configuration service fails, return options with safe defaults
            return new BulkProcessingOptions(); // Uses MaxConcurrency = 1 by default
        }
    }

    /// <summary>
    /// Creates a BulkProcessingOptions instance with default settings.
    /// Useful for scenarios where configuration is not available or not required.
    /// </summary>
    /// <returns>BulkProcessingOptions instance with safe default values.</returns>
    public static BulkProcessingOptions CreateDefault()
    {
        return new BulkProcessingOptions
        {
            MaxConcurrency = 1, // Safe default for backward compatibility
            BatchSize = 100,    // Reasonable default batch size
            ContinueOnError = true, // Continue processing on individual failures
            ProgressReportingInterval = 1000 // 1 second progress updates
        };
    }

    /// <summary>
    /// Creates a BulkProcessingOptions instance with custom MaxConcurrency.
    /// Validates the concurrency value and falls back to safe defaults if invalid.
    /// </summary>
    /// <param name="maxConcurrency">Desired MaxConcurrency value.</param>
    /// <returns>BulkProcessingOptions instance with validated MaxConcurrency.</returns>
    public static BulkProcessingOptions CreateWithConcurrency(int maxConcurrency)
    {
        var options = CreateDefault();
        
        // Simple validation - clamp to safe range
        options.MaxConcurrency = maxConcurrency switch
        {
            < 1 => 1,      // Minimum concurrency
            > 100 => 100,  // Maximum concurrency  
            _ => maxConcurrency
        };
        
        return options;
    }
}
