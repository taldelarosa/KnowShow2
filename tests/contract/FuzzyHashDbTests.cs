using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using System.IO;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Tests.Contract;

public class FuzzyHashDbTests
{

    [Fact]
    public async Task HashInsertAndLookup_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var subtitleText = "Test subtitle content";
        var subtitle = new LabelledSubtitle
        {
            Series = "TestShow",
            Season = "01",
            Episode = "02",
            SubtitleText = subtitleText
        };

        var tempFile = TestDatabaseConfig.GetTempDatabasePath();
        try
        {
            var service = TestDatabaseConfig.CreateTestFuzzyHashService(tempFile);

            // Act
            await service.StoreHash(subtitle);
            var matches = await service.FindMatches(subtitleText);

            // Assert
            matches.Should().HaveCount(1, "Should find exactly one match");
            matches[0].Subtitle.Series.Should().Be(subtitle.Series);
            matches[0].Subtitle.Season.Should().Be(subtitle.Season);
            matches[0].Subtitle.Episode.Should().Be(subtitle.Episode);
            matches[0].Confidence.Should().BeApproximately(1.0, 0.001, "Should have 100% confidence for exact match");
        }
        finally
        {
            TestDatabaseConfig.CleanupTempDatabase(tempFile);
        }
    }

    [Fact]
    public async Task NoMatchFound_ReturnsLowConfidence()
    {
        // Arrange
        var unmatchedSubtitle = "Unique subtitle content that won't match";
        var storedSubtitle = new LabelledSubtitle
        {
            Series = "TestShow",
            Season = "01",
            Episode = "02",
            SubtitleText = "Completely different content"
        };

        var tempFile = TestDatabaseConfig.GetTempDatabasePath();
        try
        {
            var service = TestDatabaseConfig.CreateTestFuzzyHashService(tempFile);

            // Act
            await service.StoreHash(storedSubtitle);
            var matches = await service.FindMatches(unmatchedSubtitle);

            // Assert
            matches.Should().HaveCount(0, "Should find no matches above threshold");
        }
        finally
        {
            TestDatabaseConfig.CleanupTempDatabase(tempFile);
        }
    }

    [Fact]
    public async Task AmbiguousMatch_ReturnsMultipleResults()
    {
        // Arrange
        var similarContent = "The quick brown fox";
        var similarSubtitles = new[]
        {
            new LabelledSubtitle { Series = "Show1", Season = "01", Episode = "01", SubtitleText = "The quick brown fox jumps over" },
            new LabelledSubtitle { Series = "Show2", Season = "02", Episode = "02", SubtitleText = "The quick brown fox runs fast" }
        };

        var tempFile = TestDatabaseConfig.GetTempDatabasePath();
        try
        {
            var service = TestDatabaseConfig.CreateTestFuzzyHashService(tempFile);

            // Act
            foreach (var subtitle in similarSubtitles)
            {
                await service.StoreHash(subtitle);
            }
            var matches = await service.FindMatches(similarContent, threshold: 0.5);

            // Assert
            matches.Should().HaveCountGreaterOrEqualTo(2, "Should find multiple similar matches");
            foreach (var match in matches)
            {
                match.Confidence.Should().BeGreaterOrEqualTo(0.5, "All matches should meet threshold");
            }
        }
        finally
        {
            TestDatabaseConfig.CleanupTempDatabase(tempFile);
        }
    }
}
