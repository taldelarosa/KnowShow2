using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private const string DefaultConfigFileName = "episodeidentifier.config.json";
    
    public AppConfig Config { get; private set; } = new();

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
    }

    public async Task LoadConfigurationAsync(string? configPath = null)
    {
        configPath ??= DefaultConfigFileName;
        
        try
        {
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Configuration file '{ConfigPath}' not found, using default settings", configPath);
                await SaveConfigurationAsync(configPath); // Create default config file
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (config != null)
            {
                Config = config;
                _logger.LogInformation("Configuration loaded from '{ConfigPath}'", configPath);
                LogCurrentSettings();
            }
            else
            {
                _logger.LogWarning("Failed to deserialize configuration from '{ConfigPath}', using defaults", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from '{ConfigPath}', using defaults", configPath);
        }
    }

    public async Task SaveConfigurationAsync(string? configPath = null)
    {
        configPath ??= DefaultConfigFileName;
        
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(Config, jsonOptions);
            await File.WriteAllTextAsync(configPath, jsonContent);
            
            _logger.LogInformation("Configuration saved to '{ConfigPath}'", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to '{ConfigPath}'", configPath);
        }
    }

    private void LogCurrentSettings()
    {
        _logger.LogDebug("Current configuration:");
        _logger.LogDebug("  Match Confidence Threshold: {MatchThreshold:P1}", Config.MatchConfidenceThreshold);
        _logger.LogDebug("  Rename Confidence Threshold: {RenameThreshold:P1}", Config.RenameConfidenceThreshold);
        _logger.LogDebug("  Filename Template: {Template}", Config.FilenameTemplate);
        _logger.LogDebug("  Primary Pattern: {Pattern}", Config.FilenamePatterns.PrimaryPattern);
        _logger.LogDebug("  Secondary Pattern: {Pattern}", Config.FilenamePatterns.SecondaryPattern);
        _logger.LogDebug("  Tertiary Pattern: {Pattern}", Config.FilenamePatterns.TertiaryPattern);
    }
}