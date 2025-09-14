using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Simple test implementation of IAppConfigService for integration tests
/// </summary>
internal class TestAppConfigService : IAppConfigService
{
    public AppConfig Config { get; private set; }

    public TestAppConfigService()
    {
        Config = new AppConfig
        {
            RenameConfidenceThreshold = 0.85,  // Standard threshold above 0.75
            MatchConfidenceThreshold = 0.8     // Standard threshold
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
}