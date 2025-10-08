using Xunit;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Tests.Contract;
using System;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for filtered hash search functionality (Feature 010).
/// These tests verify the API contract for optional series/season filtering.
/// </summary>
public class FilteredHashSearchTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly FuzzyHashService _hashService;

    public FilteredHashSearchTests()
    {
        _testDbPath = TestDatabaseConfig.GetTempDatabasePath();
        _hashService = TestDatabaseConfig.CreateTestFuzzyHashService(_testDbPath);
    }

    public void Dispose()
    {
        _hashService?.Dispose();
        TestDatabaseConfig.CleanupTempDatabase(_testDbPath);
    }

    /// <summary>
    /// T003: Contract test - FindMatches accepts optional seriesFilter parameter
    /// Expected: FAILS (parameter doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_AcceptsOptionalSeriesFilterParameter()
    {
        // Arrange
        var subtitleText = "Test subtitle content";
        var seriesFilter = "Bones";

        // Act & Assert - This should compile and run
        // Currently will fail because parameter doesn't exist
        var result = await _hashService.FindMatches(
            subtitleText, 
            threshold: 0.8, 
            seriesFilter: seriesFilter);

        // Verify method accepts the parameter
        Assert.NotNull(result);
    }

    /// <summary>
    /// T004: Contract test - FindMatches accepts optional seasonFilter parameter
    /// Expected: FAILS (parameter doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_AcceptsOptionalSeasonFilterParameter()
    {
        // Arrange
        var subtitleText = "Test subtitle content";
        var seriesFilter = "Bones";
        var seasonFilter = 2;

        // Act & Assert - This should compile and run
        // Currently will fail because parameter doesn't exist
        var result = await _hashService.FindMatches(
            subtitleText,
            threshold: 0.8,
            seriesFilter: seriesFilter,
            seasonFilter: seasonFilter);

        // Verify method accepts both parameters
        Assert.NotNull(result);
    }

    /// <summary>
    /// T005: Contract test - Season without series throws ArgumentException
    /// Expected: FAILS (validation doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_ThrowsArgumentException_WhenSeasonProvidedWithoutSeries()
    {
        // Arrange
        var subtitleText = "Test subtitle content";
        int? seasonFilter = 2;
        string? seriesFilter = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _hashService.FindMatches(
                subtitleText,
                threshold: 0.8,
                seriesFilter: seriesFilter,
                seasonFilter: seasonFilter);
        });

        // Verify error message mentions series requirement
        Assert.Contains("series", exception.Message.ToLower());
        Assert.Contains("season", exception.Message.ToLower());
    }

    /// <summary>
    /// T006: Contract test - Case-insensitive series matching
    /// Expected: FAILS (case-insensitive logic doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task FindMatches_MatchesSeriesCaseInsensitively()
    {
        // Arrange - Create test data with "Bones" series
        var testSubtitle = new LabelledSubtitle
        {
            Series = "Bones",
            Season = "01",
            Episode = "01",
            SubtitleText = "This is a test subtitle for Bones S01E01",
            EpisodeName = "Test Episode"
        };
        await _hashService.StoreHash(testSubtitle);

        var searchText = "This is a test subtitle for Bones S01E01";

        // Act - Search with different case variations
        var resultUpperCase = await _hashService.FindMatches(searchText, 0.5, "BONES");
        var resultLowerCase = await _hashService.FindMatches(searchText, 0.5, "bones");
        var resultMixedCase = await _hashService.FindMatches(searchText, 0.5, "BoNeS");

        // Assert - All should return the same result (the Bones episode)
        Assert.NotEmpty(resultUpperCase);
        Assert.NotEmpty(resultLowerCase);
        Assert.NotEmpty(resultMixedCase);

        Assert.Equal(resultUpperCase.Count, resultLowerCase.Count);
        Assert.Equal(resultUpperCase.Count, resultMixedCase.Count);

        // Verify they all found the same episode
        Assert.Equal("Bones", resultUpperCase[0].Subtitle.Series);
        Assert.Equal("Bones", resultLowerCase[0].Subtitle.Series);
        Assert.Equal("Bones", resultMixedCase[0].Subtitle.Series);
    }

    /// <summary>
    /// T012: Integration test - Backwards compatibility validation
    /// Expected: SHOULD PASS (no breaking changes)
    /// </summary>
    [Fact]
    public async Task FindMatches_BackwardsCompatible_WithExistingCallPatterns()
    {
        // Arrange
        var testSubtitle = new LabelledSubtitle
        {
            Series = "TestSeries",
            Season = "01",
            Episode = "01",
            SubtitleText = "Backwards compatibility test content",
            EpisodeName = "Test"
        };
        await _hashService.StoreHash(testSubtitle);

        var searchText = "Backwards compatibility test content";

        // Act - Call using existing patterns (should still work)
        var result1 = await _hashService.FindMatches(searchText);
        var result2 = await _hashService.FindMatches(searchText, 0.8);
        var result3 = await _hashService.FindMatches(searchText, threshold: 0.7);

        // Assert - All existing call patterns should work
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
    }
}
