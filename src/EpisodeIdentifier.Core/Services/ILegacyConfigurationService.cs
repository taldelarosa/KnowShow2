using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Legacy configuration service interface for backward compatibility.
/// Will be removed when the new fuzzy hashing plus system is fully integrated.
/// </summary>
public interface IAppConfigService
{
    AppConfig Config { get; }
    Task LoadConfigurationAsync(string? configPath = null);
    Task SaveConfigurationAsync(string? configPath = null);
}