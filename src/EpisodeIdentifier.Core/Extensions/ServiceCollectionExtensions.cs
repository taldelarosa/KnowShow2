using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EpisodeIdentifier.Core.Extensions;

/// <summary>
/// Extension methods for dependency injection configuration in Episode Identification System.
/// Provides centralized service registration for proper DI container setup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all episode identification services including bulk processing and CLI capabilities.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddEpisodeIdentificationServices(this IServiceCollection services)
    {
        // Core infrastructure services
        // Only add default file system if none provided (tests may override with MockFileSystem)
        services.TryAddSingleton<IFileSystem, FileSystem>();
        services.AddConsoleLogging();

        // Configuration services (modern + legacy compatibility)
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IAppConfigService, ConfigurationService>();

        // Filename and file operations
        services.AddScoped<IFilenameService, FilenameService>();
        services.AddScoped<FilenameService>(); // concrete for ctor injection
        services.AddScoped<IFileRenameService, FileRenameService>();
        services.AddScoped<FileRenameService>(); // concrete for ctor injection
        services.AddScoped<IProgressTracker, ProgressTracker>();
        services.AddScoped<IFileDiscoveryService, FileDiscoveryService>();

        // Video analysis and subtitle extraction stack
        services.AddScoped<VideoFormatValidator>();
        services.AddScoped<ISubtitleExtractor, SubtitleExtractor>();
        services.AddScoped<SubtitleExtractor>(); // For concrete ctor injection
        services.AddScoped<PgsRipService>();
        services.AddScoped<PgsToTextConverter>();
        services.AddScoped<EnhancedPgsToTextConverter>();
        services.AddScoped<VideoTextSubtitleExtractor>();

        // Text subtitle parsing support (optional but useful for tests)
        services.AddScoped<ISubtitleFormatHandler, SrtFormatHandler>();
        services.AddScoped<ISubtitleFormatHandler, AssFormatHandler>();
        services.AddScoped<ISubtitleFormatHandler, VttFormatHandler>();
        services.AddScoped<ITextSubtitleExtractor, TextSubtitleExtractor>();

        // Hashing and matching
        services.AddScoped<SubtitleNormalizationService>();
        services.AddScoped<FuzzyHashService>(provider =>
            new FuzzyHashService(
                ":memory:", // isolated, in-memory DB for tests and lightweight scenarios
                provider.GetRequiredService<ILogger<FuzzyHashService>>(),
                provider.GetRequiredService<SubtitleNormalizationService>()
            ));

        // CTPH hashing (enhanced pipeline)
        services.AddScoped<ICTPhHashingService, CTPhHashingService>();
        services.AddScoped<EnhancedCTPhHashingService>();

        // Episode identification orchestrator (register concrete + interface)
        services.AddScoped<EpisodeIdentificationService>();
        services.AddScoped<IEpisodeIdentificationService, EpisodeIdentificationService>();

        // Video file processing orchestrator
        services.AddScoped<IVideoFileProcessingService, VideoFileProcessingService>();

        // Bulk processing services
        services.AddScoped<IBulkProcessor, BulkProcessorService>();

        return services;
    }

    /// <summary>
    /// Adds console logging configuration for command-line interfaces.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddConsoleLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services;
    }
}
