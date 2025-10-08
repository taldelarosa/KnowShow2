using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

Console.WriteLine("Testing filename generation...");

var filenameService = new FilenameService();

// Test case that's failing: Series with 150 'B' chars, Episode with 150 'C' chars, max length 100
var testRequest = new FilenameGenerationRequest
{
    Series = new string('B', 150),
    Season = "01",
    Episode = "01", 
    EpisodeName = new string('C', 150),
    FileExtension = ".mkv",
    MatchConfidence = 0.95,
    MaxLength = 100
};

var result = filenameService.GenerateFilename(testRequest);

Console.WriteLine($"IsValid: {result.IsValid}");
Console.WriteLine($"ValidationError: {result.ValidationError}");
Console.WriteLine($"SuggestedFilename: '{result.SuggestedFilename}'");
Console.WriteLine($"Length: {result.TotalLength}");
Console.WriteLine($"WasTruncated: {result.WasTruncated}");

if (result.SuggestedFilename != null)
{
    // Check if filename contains S01E01
    Console.WriteLine($"Contains 'S01E01': {result.SuggestedFilename.Contains("S01E01")}");
    Console.WriteLine($"Ends with '.mkv': {result.SuggestedFilename.EndsWith(".mkv")}");
}