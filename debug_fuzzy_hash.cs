using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Debug
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup basic logging
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            var logger = loggerFactory.CreateLogger<FuzzyHashService>();
            var normalizationService = new SubtitleNormalizationService();

            // Create temp database
            var tempDbPath = $"debug_{Guid.NewGuid()}.db";
            
            try
            {
                using var fuzzyHashService = new FuzzyHashService(tempDbPath, logger, normalizationService);

                // Test case 1: DatabaseConcurrencyOptimizationTests scenario
                Console.WriteLine("=== Test Case 1: DatabaseConcurrencyOptimizationTests scenario ===");
                
                var storedText = "Test subtitle for concurrency optimization";
                var searchText = "Test subtitle";
                
                Console.WriteLine($"Storing: '{storedText}'");
                await fuzzyHashService.StoreHash(new LabelledSubtitle
                {
                    Series = "TestSeries",
                    Season = "01", 
                    Episode = "01",
                    SubtitleText = storedText
                });

                Console.WriteLine($"Searching: '{searchText}' with threshold 0.5");
                var matches1 = await fuzzyHashService.FindMatches(searchText, 0.5);
                Console.WriteLine($"Found {matches1.Count} matches");
                foreach (var match in matches1)
                {
                    Console.WriteLine($"  - {match.Subtitle.Series} S{match.Subtitle.Season}E{match.Subtitle.Episode}: {match.Confidence:P2}");
                }

                // Test case 2: DatabaseConnectionPoolingTests scenario  
                Console.WriteLine("\n=== Test Case 2: DatabaseConnectionPoolingTests scenario ===");
                
                var storedText2 = "This is a longer subtitle text for episode 5 with more content to simulate real-world subtitle data that might be several sentences long and contain various details about the episode plot, character dialogue, and scene descriptions.";
                var searchText2 = "longer subtitle text episode 5";
                
                Console.WriteLine($"Storing: '{storedText2}'");
                await fuzzyHashService.StoreHash(new LabelledSubtitle
                {
                    Series = "LargeDataset",
                    Season = "01", 
                    Episode = "05",
                    SubtitleText = storedText2
                });

                Console.WriteLine($"Searching: '{searchText2}' with threshold 0.6");
                var matches2 = await fuzzyHashService.FindMatches(searchText2, 0.6);
                Console.WriteLine($"Found {matches2.Count} matches");
                foreach (var match in matches2)
                {
                    Console.WriteLine($"  - {match.Subtitle.Series} S{match.Subtitle.Season}E{match.Subtitle.Episode}: {match.Confidence:P2}");
                }

                // Also try with lower threshold
                Console.WriteLine($"Searching: '{searchText2}' with threshold 0.3");
                var matches3 = await fuzzyHashService.FindMatches(searchText2, 0.3);
                Console.WriteLine($"Found {matches3.Count} matches");
                foreach (var match in matches3)
                {
                    Console.WriteLine($"  - {match.Subtitle.Series} S{match.Subtitle.Season}E{match.Subtitle.Episode}: {match.Confidence:P2}");
                }
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                {
                    System.IO.File.Delete(tempDbPath);
                }
            }
        }
    }
}