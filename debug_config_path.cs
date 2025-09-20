using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Use a unique config path
        var uniqueConfigName = $"episodeidentifier_debug_{Guid.NewGuid():N}.config.json";
        var testConfigPath = Path.Combine(AppContext.BaseDirectory, uniqueConfigName);

        Console.WriteLine($"Test config path: {testConfigPath}");

        // Register services with the unique config path
        services.AddScoped<IConfigurationService>(provider => new ConfigurationService(
            provider.GetRequiredService<ILogger<ConfigurationService>>(),
            fileSystem: null,
            configFilePath: testConfigPath));

        var serviceProvider = services.BuildServiceProvider();
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Try to load config (should fail - no file exists)
        var result = await configService.LoadConfiguration();
        
        Console.WriteLine($"Config loaded: {result.IsValid}");
        Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
    }
}
