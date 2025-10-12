using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Simple test implementation of IAppConfigService for integration tests
/// </summary>
internal class TestAppConfigService : IAppConfigService
{
    public AppConfig Config { get; private set; }

    public int MaxConcurrency => Config.MaxConcurrency;
    public ConfigurationResult? LastConfigurationResult => ConfigurationResult.Success(new Configuration { MaxConcurrency = Config.MaxConcurrency });

    public TestAppConfigService()
    {
        Config = new AppConfig
        {
            RenameConfidenceThreshold = 0.85,  // Standard threshold above 0.75
            MatchConfidenceThreshold = 0.8,    // Standard threshold
            MaxConcurrency = 4                  // Default test value
        };
    }

    public Task LoadConfigurationAsync(string? configPath = null)
    {
        return Task.CompletedTask;
    }

    public Task SaveConfigurationAsync(string? configPath = null)
    {
        return Task.CompletedTask;
    }

    public Task<ConfigurationResult> LoadConfiguration()
    {
        var config = new Configuration { MaxConcurrency = Config.MaxConcurrency };
        return Task.FromResult(ConfigurationResult.Success(config));
    }

    public Task<bool> ReloadIfChanged()
    {
        return Task.FromResult(false);
    }

    public ConfigurationResult ValidateConfiguration(Configuration config)
    {
        return ConfigurationResult.Success(config);
    }
}
