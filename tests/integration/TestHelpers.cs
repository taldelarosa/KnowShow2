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
    public ConfigurationResult? LastConfigurationResult => ConfigurationResult.Success(new Configuration
    {
        MaxConcurrency = Config.MaxConcurrency,
        MatchingThresholds = new MatchingThresholds
        {
            TextBased = new SubtitleTypeThresholds { MatchConfidence = 0.8m, RenameConfidence = 0.85m, FuzzyHashSimilarity = 75 },
            PGS = new SubtitleTypeThresholds { MatchConfidence = 0.7m, RenameConfidence = 0.75m, FuzzyHashSimilarity = 65 },
            VobSub = new SubtitleTypeThresholds { MatchConfidence = 0.6m, RenameConfidence = 0.7m, FuzzyHashSimilarity = 55 }
        }
    });

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
        var config = new Configuration
        {
            MaxConcurrency = Config.MaxConcurrency,
            MatchingThresholds = new MatchingThresholds
            {
                TextBased = new SubtitleTypeThresholds { MatchConfidence = 0.8m, RenameConfidence = 0.85m, FuzzyHashSimilarity = 75 },
                PGS = new SubtitleTypeThresholds { MatchConfidence = 0.7m, RenameConfidence = 0.75m, FuzzyHashSimilarity = 65 },
                VobSub = new SubtitleTypeThresholds { MatchConfidence = 0.6m, RenameConfidence = 0.7m, FuzzyHashSimilarity = 55 }
            }
        };
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
