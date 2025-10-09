using Xunit;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Tests.Contract;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for series/season filtering functionality (Feature 010).
/// These tests verify filtering behavior with multi-series databases.
/// </summary>
public class SeriesSeasonFilteringTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly FuzzyHashService _hashService;

    public SeriesSeasonFilteringTests()
    {
        _testDbPath = TestDatabaseConfig.GetTempDatabasePath();
        _hashService = TestDatabaseConfig.CreateTestFuzzyHashService(_testDbPath);

        // Setup multi-series test data
        SetupMultiSeriesDatabase().Wait();
    }

    public void Dispose()
    {
        _hashService?.Dispose();
        TestDatabaseConfig.CleanupTempDatabase(_testDbPath);
    }

    private async Task SetupMultiSeriesDatabase()
    {
        // Add Bones episodes (Season 1 and 2)
        for (int ep = 1; ep <= 5; ep++)
        {
            await _hashService.StoreHash(new LabelledSubtitle
            {
                Series = "Bones",
                Season = "01",
                Episode = ep.ToString("D2"),
                SubtitleText = $"Bones S01E{ep:D2} unique content for testing",
                EpisodeName = $"Bones Episode {ep}"
            });
        }

        for (int ep = 1; ep <= 5; ep++)
        {
            await _hashService.StoreHash(new LabelledSubtitle
            {
                Series = "Bones",
                Season = "02",
                Episode = ep.ToString("D2"),
                SubtitleText = $"Bones S02E{ep:D2} unique content for testing",
                EpisodeName = $"Bones Season 2 Episode {ep}"
            });
        }

        // Add Breaking Bad episodes (Season 1)
        for (int ep = 1; ep <= 3; ep++)
        {
            await _hashService.StoreHash(new LabelledSubtitle
            {
                Series = "Breaking Bad",
                Season = "01",
                Episode = ep.ToString("D2"),
                SubtitleText = $"Breaking Bad S01E{ep:D2} unique content for testing",
                EpisodeName = $"Breaking Bad Episode {ep}"
            });
        }

        // Add The Office episodes (Season 1)
        for (int ep = 1; ep <= 3; ep++)
        {
            await _hashService.StoreHash(new LabelledSubtitle
            {
                Series = "The Office",
                Season = "01",
                Episode = ep.ToString("D2"),
                SubtitleText = $"The Office S01E{ep:D2} unique content for testing",
                EpisodeName = $"The Office Episode {ep}"
            });
        }
    }

    /// <summary>
    /// T009: Integration test - Multi-series database filtering
    /// Expected: FAILS (filtering doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_WithSeriesFilter_ReturnsOnlyMatchingSeries()
    {
        // Arrange
        var bonesSearchText = "Bones S01E03 unique content for testing";

        // Act - Search with series filter for "Bones"
        var results = await _hashService.FindMatches(
            bonesSearchText,
            threshold: 0.5,
            seriesFilter: "Bones");

        // Assert - Should only return Bones episodes, not Breaking Bad or The Office
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Bones", r.Subtitle.Series));

        // Verify no other series in results
        Assert.DoesNotContain(results, r => r.Subtitle.Series == "Breaking Bad");
        Assert.DoesNotContain(results, r => r.Subtitle.Series == "The Office");
    }

    /// <summary>
    /// T010: Integration test - Series + Season filtering reduces record count
    /// Expected: FAILS (filtering doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_WithSeriesAndSeasonFilter_ReturnsOnlyMatchingSeason()
    {
        // Arrange
        var bonesS02SearchText = "Bones S02E02 unique content for testing";

        // Act - Search with series and season filter
        var results = await _hashService.FindMatches(
            bonesS02SearchText,
            threshold: 0.5,
            seriesFilter: "Bones",
            seasonFilter: 2);

        // Assert - Should only return Bones Season 2 episodes
        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.Equal("Bones", r.Subtitle.Series);
            Assert.Equal("02", r.Subtitle.Season);
        });

        // Verify count is limited to season 2 episodes only (max 5)
        Assert.True(results.Count <= 5, $"Expected max 5 results, got {results.Count}");
    }

    /// <summary>
    /// T011: Integration test - Empty results for non-existent series
    /// Expected: FAILS (graceful handling doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_WithNonExistentSeries_ReturnsEmptyList()
    {
        // Arrange
        var searchText = "Some random subtitle content";

        // Act - Search for non-existent series
        var results = await _hashService.FindMatches(
            searchText,
            threshold: 0.5,
            seriesFilter: "NonExistentShow");

        // Assert - Should return empty list, NOT throw exception
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    /// <summary>
    /// T009b: Verify filtering actually reduces database scan
    /// This tests that series filter excludes other series from comparison
    /// </summary>
    [Fact]
    public async Task FindMatches_WithSeriesFilter_DoesNotMatchOtherSeries()
    {
        // Arrange - Create unique content that could match across series
        var sharedContent = "This is shared dialog content";

        await _hashService.StoreHash(new LabelledSubtitle
        {
            Series = "Bones",
            Season = "03",
            Episode = "01",
            SubtitleText = sharedContent,
            EpisodeName = "Bones with shared content"
        });

        await _hashService.StoreHash(new LabelledSubtitle
        {
            Series = "Breaking Bad",
            Season = "02",
            Episode = "01",
            SubtitleText = sharedContent,
            EpisodeName = "Breaking Bad with shared content"
        });

        // Act - Search with series filter
        var bonesResults = await _hashService.FindMatches(
            sharedContent,
            threshold: 0.5,
            seriesFilter: "Bones");

        var breakingBadResults = await _hashService.FindMatches(
            sharedContent,
            threshold: 0.5,
            seriesFilter: "Breaking Bad");

        // Assert - Each filter should only return its own series
        Assert.All(bonesResults, r => Assert.Equal("Bones", r.Subtitle.Series));
        Assert.All(breakingBadResults, r => Assert.Equal("Breaking Bad", r.Subtitle.Series));
    }

    /// <summary>
    /// T010b: Verify season filter works correctly with zero-padded seasons
    /// </summary>
    [Fact]
    public async Task FindMatches_WithSeasonFilter_HandlesZeroPaddingCorrectly()
    {
        // Arrange
        var searchText = "Bones S01E01 unique content for testing";

        // Act - Search with season 1 (should match "01" in database)
        var results = await _hashService.FindMatches(
            searchText,
            threshold: 0.5,
            seriesFilter: "Bones",
            seasonFilter: 1);

        // Assert - Should find Season 1 episodes
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("01", r.Subtitle.Season));

        // Should not find Season 2 episodes
        Assert.DoesNotContain(results, r => r.Subtitle.Season == "02");
    }
}
