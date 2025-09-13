using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

public interface IConfigurationService
{
    AppConfig Config { get; }
    Task LoadConfigurationAsync(string? configPath = null);
    Task SaveConfigurationAsync(string? configPath = null);
}