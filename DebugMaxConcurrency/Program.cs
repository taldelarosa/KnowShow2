using System;using System;using System.Text.Json;

using System.IO;

using System.Text.Json;using System.IO;using EpisodeIdentifier.Core.Models.Configuration;

using EpisodeIdentifier.Core.Models.Configuration;

using EpisodeIdentifier.Core.Services;using System.Text.Json;using EpisodeIdentifier.Core.Services;

using Microsoft.Extensions.Logging;

using System.IO.Abstractions;using EpisodeIdentifier.Core.Models.Configuration;using Microsoft.Extensions.Logging.Abstractions;


var tempPath = Path.Combine(Path.GetTempPath(), $"debug_maxconcurrency_{Guid.NewGuid()}.json");using EpisodeIdentifier.Core.Services;


try using Microsoft.Extensions.Logging;Console.WriteLine("Testing MaxConcurrency clamping logic...");

{

    Console.WriteLine("Debug MaxConcurrency Clamping Test");using System.IO.Abstractions;

    Console.WriteLine($"Using temp file: {tempPath}");

    // Test 1: Create configuration with very large MaxConcurrency

    var testConfig = new 

    {var tempPath = Path.Combine(Path.GetTempPath(), $"debug_maxconcurrency_clamping_{Guid.NewGuid()}.json");var config = new Configuration

        Version = "1.0.0",

        MaxConcurrency = 2147483647,{

        MatchConfidenceThreshold = 0.8,

        RenameConfidenceThreshold = 0.9,try     Version = "2.0",

        FuzzyHashThreshold = 70,

        HashingAlgorithm = "SHA1",{    MatchConfidenceThreshold = 0.6m,

        FilenamePatterns = new 

        {    Console.WriteLine($"Debug MaxConcurrency Clamping Test");    RenameConfidenceThreshold = 0.7m,

            PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)"

        },    Console.WriteLine($"Using temp file: {tempPath}");    FuzzyHashThreshold = 75,

        FilenameTemplate = "{SeriesName} S{Season}E{Episode}"

    };        HashingAlgorithm = HashingAlgorithm.CTPH,

    

    var json = JsonSerializer.Serialize(testConfig, new JsonSerializerOptions { WriteIndented = true });    // Create test configuration with MaxConcurrency > 100    FilenamePatterns = new FilenamePatterns

    Console.WriteLine($"JSON:\n{json}");

        var testConfig = new     {

    await File.WriteAllTextAsync(tempPath, json);

        {        PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"

    var loggerFactory = LoggerFactory.Create(builder => 

        builder.AddConsole().SetMinimumLevel(LogLevel.Debug));        Version = "1.0.0",    },

    var logger = loggerFactory.CreateLogger<ConfigurationService>();

    var fileSystem = new FileSystem();        MaxConcurrency = 2147483647, // int.MaxValue    FilenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}",

    

    var configService = new ConfigurationService(logger, fileSystem);        MatchConfidenceThreshold = 0.8,    MaxConcurrency = int.MaxValue,

    

    Console.WriteLine("\n=== Loading Configuration ===");        RenameConfidenceThreshold = 0.9,    BulkProcessing = null

    var result = await configService.LoadConfigurationAsync(tempPath);

    Console.WriteLine($"IsValid: {result.IsValid}");        FuzzyHashThreshold = 70,};

    

    if (!result.IsValid)        HashingAlgorithm = "SHA1",

    {

        Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");        FilenamePatterns = new Console.WriteLine($"Original MaxConcurrency: {config.MaxConcurrency}");

    }

            {

    Console.WriteLine($"ConfigService.MaxConcurrency: {configService.MaxConcurrency}");

                PrimaryPattern = @"(?<SeriesName>.+)\.S(?<Season>\d+)E(?<Episode>\d+)"// Test 2: Serialize to JSON

    if (result.Configuration != null)

    {        },var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        Console.WriteLine($"Configuration.MaxConcurrency: {result.Configuration.MaxConcurrency}");

    }        FilenameTemplate = "{SeriesName} S{Season}E{Episode}",Console.WriteLine($"JSON contains maxConcurrency: {json.Contains("maxConcurrency")}");

}

finally        BulkProcessing = (object?)null

{

    if (File.Exists(tempPath))    };// Test 3: Write to temp file and read back

    {

        File.Delete(tempPath);    var tempFile = Path.GetTempFileName();

    }

}    var json = JsonSerializer.Serialize(testConfig, new JsonSerializerOptions { WriteIndented = true });await File.WriteAllTextAsync(tempFile, json);

    Console.WriteLine($"JSON content:\n{json}");

    // Test 4: Create ConfigurationService and load

    await File.WriteAllTextAsync(tempPath, json);var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, null, tempFile);

    var result = await configService.LoadConfiguration();

    // Create ConfigurationService

    var loggerFactory = LoggerFactory.Create(builder => Console.WriteLine($"Load result IsValid: {result.IsValid}");

        builder.AddConsole().SetMinimumLevel(LogLevel.Trace));Console.WriteLine($"Load result Errors: {string.Join(", ", result.Errors)}");

    var logger = loggerFactory.CreateLogger<ConfigurationService>();Console.WriteLine($"ConfigService MaxConcurrency: {configService.MaxConcurrency}");

    var fileSystem = new FileSystem();

    if (result.Configuration != null)

    var configService = new ConfigurationService(logger, fileSystem);{

        Console.WriteLine($"Result Configuration MaxConcurrency: {result.Configuration.MaxConcurrency}");

    Console.WriteLine("\n=== Loading Configuration ===");}

    var result = await configService.LoadConfigurationAsync(tempPath);

    Console.WriteLine($"LoadConfiguration Result: IsValid={result.IsValid}");File.Delete(tempFile);

    
    if (!result.IsValid)
    {
        Console.WriteLine($"Validation Errors: {string.Join(", ", result.Errors)}");
    }
    
    Console.WriteLine($"ConfigService MaxConcurrency: {configService.MaxConcurrency}");
    
    if (result.Configuration != null)
    {
        Console.WriteLine($"Result Configuration MaxConcurrency: {result.Configuration.MaxConcurrency}");
    }
}
finally
{
    if (File.Exists(tempPath))
    {
        File.Delete(tempPath);
    }
}
