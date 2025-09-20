using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using System.Reflection;

namespace EpisodeIdentifier.Debug
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup basic logging
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            var logger = loggerFactory.CreateLogger<FuzzyHashService>();
            var normalizationLogger = loggerFactory.CreateLogger<SubtitleNormalizationService>();
            var normalizationService = new SubtitleNormalizationService(normalizationLogger);

            // Create temp database
            var tempDbPath = $"debug_{Guid.NewGuid()}.db";
            
            try
            {
                using var fuzzyHashService = new FuzzyHashService(tempDbPath, logger, normalizationService);

                // Test case 2: DatabaseConnectionPoolingTests scenario
                Console.WriteLine("=== Test Case: DatabaseConnectionPoolingTests scenario ===");
                
                var storedText = "This is a longer subtitle text for episode 5 with more content to simulate real-world subtitle data that might be several sentences long and contain various details about the episode plot, character dialogue, and scene descriptions.";
                var searchText = "longer subtitle text episode 5";
                
                Console.WriteLine($"Stored: '{storedText}'");
                Console.WriteLine($"Search: '{searchText}'");

                // Use reflection to access private methods for debugging
                var type = typeof(FuzzyHashService);
                var generateHashMethod = type.GetMethod("GenerateFuzzyHash", BindingFlags.NonPublic | BindingFlags.Instance);
                var compareHashMethod = type.GetMethod("CompareFuzzyHashes", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (generateHashMethod != null && compareHashMethod != null)
                {
                    var hash1 = (string)generateHashMethod.Invoke(fuzzyHashService, new object[] { storedText });
                    var hash2 = (string)generateHashMethod.Invoke(fuzzyHashService, new object[] { searchText });
                    
                    var similarity = (double)compareHashMethod.Invoke(fuzzyHashService, new object[] { hash1, hash2 });
                    Console.WriteLine($"\nSimilarity: {similarity:P2}");
                    
                    Console.WriteLine($"\nStored Hash: {hash1}");
                    Console.WriteLine($"Search Hash: {hash2}");
                    
                    // Also test simpler/shorter scenario
                    Console.WriteLine("\n=== Simpler Case Test ===");
                    var simple1 = "This is episode 5 with longer subtitle text";
                    var simple2 = "longer subtitle text episode 5";
                    Console.WriteLine($"Simple1: '{simple1}'");
                    Console.WriteLine($"Simple2: '{simple2}'");
                    
                    var simpleHash1 = (string)generateHashMethod.Invoke(fuzzyHashService, new object[] { simple1 });
                    var simpleHash2 = (string)generateHashMethod.Invoke(fuzzyHashService, new object[] { simple2 });
                    
                    var simpleSimilarity = (double)compareHashMethod.Invoke(fuzzyHashService, new object[] { simpleHash1, simpleHash2 });
                    Console.WriteLine($"\nSimple Similarity: {simpleSimilarity:P2}");
                }
                else
                {
                    Console.WriteLine("Could not access private methods for debugging");
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