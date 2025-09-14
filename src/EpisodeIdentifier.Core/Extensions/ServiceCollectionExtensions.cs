using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;

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
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddConsoleLogging();

        // Core episode identification services
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IFilenameService, FilenameService>();
        services.AddScoped<IFileRenameService, FileRenameService>();
        services.AddScoped<IProgressTracker, ProgressTracker>();

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