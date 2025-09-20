using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using EpisodeIdentifier.Core.Services.Hashing;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models.Hashing;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Examples
{
    /// <summary>
    /// Example demonstrating the text search fallback functionality
    /// </summary>
    public class TextSearchFallbackExample
    {
        /// <summary>
        /// Demonstrates how to use the enhanced CTPH service with text fallback
        /// </summary>
        public static async Task RunExample()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            var logger = loggerFactory.CreateLogger<TextSearchFallbackExample>();
            var enhancedLogger = loggerFactory.CreateLogger<EnhancedCTPhHashingService>();
            var fuzzyLogger = loggerFactory.CreateLogger<FuzzyHashService>();

            logger.LogInformation("=== Text Search Fallback Demo ===");

            try
            {
                // Setup services (in a real application, these would be configured via DI)
                var dbPath = ":memory:"; // Use in-memory database for demo
                var normalizationLogger = loggerFactory.CreateLogger<SubtitleNormalizationService>();
                var normalizationService = new SubtitleNormalizationService(normalizationLogger);
                var fuzzyHashService = new FuzzyHashService(dbPath, fuzzyLogger, normalizationService);

                // Create a mock CTPH service
                var mockCtphService = new MockCTPhHashingService();

                // Create a mock configuration service for testing
                var configService = new ConfigurationService(Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigurationService>.Instance);

                // Create the enhanced service
                var enhancedService = new EnhancedCTPhHashingService(
                    mockCtphService, fuzzyHashService, enhancedLogger, configService);

                // Example 1: Add some sample data to the database
                await AddSampleData(fuzzyHashService, logger);

                // Example 2: Test with a subtitle that should match via text fallback
                var testSubtitle = @"
                    Hello, how are you doing today?
                    I'm doing quite well, thank you for asking.
                    The weather is beautiful outside.
                    Would you like to go for a walk?
                ";

                logger.LogInformation("Testing subtitle text fallback with sample text...");
                var result = await enhancedService.CompareSubtitleWithFallback(testSubtitle, enableTextFallback: true);

                // Display results
                DisplayResults(result, logger);

                // Example 3: Test with a subtitle that shouldn't match
                var nomatchSubtitle = @"
                    This is completely different content
                    that shouldn't match anything in the database.
                    It's about cooking and recipes.
                ";

                logger.LogInformation("Testing subtitle text fallback with non-matching text...");
                var noMatchResult = await enhancedService.CompareSubtitleWithFallback(nomatchSubtitle, enableTextFallback: true);

                DisplayResults(noMatchResult, logger);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running text search fallback example");
            }
        }

        private static async Task AddSampleData(FuzzyHashService fuzzyHashService, ILogger logger)
        {
            logger.LogInformation("Adding sample data to demonstrate text fallback...");

            var sampleEpisodes = new[]
            {
                new LabelledSubtitle
                {
                    Series = "Sample Show",
                    Season = "1",
                    Episode = "1",
                    EpisodeName = "Pilot Episode",
                    SubtitleText = @"
                        Hello, how are you doing today?
                        I'm doing well, thanks for asking.
                        The weather looks great outside.
                        Should we go for a nice walk?
                    "
                },
                new LabelledSubtitle
                {
                    Series = "Sample Show",
                    Season = "1",
                    Episode = "2",
                    EpisodeName = "The Meeting",
                    SubtitleText = @"
                        Welcome everyone to today's meeting.
                        We have several important topics to discuss.
                        First, let's review the quarterly reports.
                        Then we'll move on to planning.
                    "
                },
                new LabelledSubtitle
                {
                    Series = "Another Series",
                    Season = "2",
                    Episode = "5",
                    EpisodeName = "The Adventure",
                    SubtitleText = @"
                        The journey begins at dawn tomorrow.
                        Make sure you pack everything we need.
                        This will be our greatest adventure yet.
                        I can't wait to see what we discover.
                    "
                }
            };

            foreach (var episode in sampleEpisodes)
            {
                await fuzzyHashService.StoreHash(episode);
                logger.LogDebug("Added sample episode: {Series} S{Season}E{Episode}",
                    episode.Series, episode.Season, episode.Episode);
            }

            logger.LogInformation("Sample data added successfully");
        }

        private static void DisplayResults(EnhancedComparisonResult result, ILogger logger)
        {
            logger.LogInformation("=== Comparison Results ===");

            if (!result.IsSuccess)
            {
                logger.LogWarning("Comparison failed: {Error}", result.ErrorMessage);
                return;
            }

            logger.LogInformation("Match found: {IsMatch}", result.IsMatch);
            logger.LogInformation("Used text fallback: {UsedFallback}", result.UsedTextFallback);
            logger.LogInformation("Hash similarity score: {HashScore}%", result.HashSimilarityScore);

            if (result.UsedTextFallback)
            {
                logger.LogInformation("Text similarity score: {TextScore}%", result.TextSimilarityScore);
                logger.LogInformation("Text fallback time: {FallbackTime}ms", result.TextFallbackTime.TotalMilliseconds);
            }

            logger.LogInformation("Hash comparison time: {HashTime}ms", result.HashComparisonTime.TotalMilliseconds);

            if (result.IsMatch && !string.IsNullOrEmpty(result.MatchedSeries))
            {
                logger.LogInformation("Matched episode: {Series} S{Season}E{Episode} - {EpisodeName}",
                    result.MatchedSeries, result.MatchedSeason, result.MatchedEpisode, result.MatchedEpisodeName);
            }

            logger.LogInformation("========================");
        }
    }

    /// <summary>
    /// Mock CTPH service for demonstration purposes
    /// </summary>
    public class MockCTPhHashingService : ICTPhHashingService
    {
        public Task<string> ComputeFuzzyHash(string filePath)
        {
            return Task.FromResult("mock:hash:value");
        }

        public Task<FileComparisonResult> CompareFiles(string filePath1, string filePath2)
        {
            return Task.FromResult(FileComparisonResult.Success("mock1", "mock2", 45, false, TimeSpan.FromMilliseconds(10)));
        }

        public Task<FileComparisonResult> CompareFileWithFallback(string filePath, bool enableTextFallback = true)
        {
            return Task.FromResult(FileComparisonResult.Success("mock", "database", 30, false, TimeSpan.FromMilliseconds(15)));
        }

        public int CompareFuzzyHashes(string hash1, string hash2)
        {
            return 42; // Mock similarity score
        }

        public int GetSimilarityThreshold()
        {
            return 75; // 75% threshold
        }
    }
}
