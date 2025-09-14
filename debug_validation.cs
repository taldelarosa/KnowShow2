using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Models;
using FluentValidation.TestHelper;

Console.WriteLine("Testing configuration validation...");

var validator = new ConfigurationValidator();

var config = new Configuration
{
    Version = "2.0",
    MatchConfidenceThreshold = 0.8m,
    RenameConfidenceThreshold = 0.85m,
    FuzzyHashThreshold = 75,
    HashingAlgorithm = HashingAlgorithm.CTPH,
    FilenamePatterns = new FilenamePatterns
    {
        PrimaryPattern = "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
    },
    FilenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
};

Console.WriteLine($"Template: '{config.FilenameTemplate}'");
Console.WriteLine($"Contains SeriesName: {config.FilenameTemplate.Contains("{SeriesName}")}");
Console.WriteLine($"Contains Season: {config.FilenameTemplate.Contains("{Season}")}");
Console.WriteLine($"Contains Episode: {config.FilenameTemplate.Contains("{Episode}")}");

var result = validator.TestValidate(config);
Console.WriteLine($"Validation errors: {result.Errors.Count}");
foreach (var error in result.Errors)
{
    Console.WriteLine($"  - {error.PropertyName}: {error.ErrorMessage}");
}