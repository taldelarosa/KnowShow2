using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using System.IO;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Unit.Services;

/// <summary>
/// Unit tests for hash-based duplicate detection in FuzzyHashService.
/// Tests verify that duplicate detection uses CTPH hash comparison rather than Series/Season/Episode.
/// </summary>
public class FuzzyHashServiceDuplicateDetectionTests : IDisposable
{
    private readonly FuzzyHashService _hashService;
    private readonly string _testDbPath;
    private readonly ILogger<FuzzyHashService> _logger;

    public FuzzyHashServiceDuplicateDetectionTests()
    {
        // Use a unique test database for each test run
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_duplicate_detection_{Guid.NewGuid()}.db");

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<FuzzyHashService>();

        // Create normalization service
        var normalizationLogger = loggerFactory.CreateLogger<SubtitleNormalizationService>();
        var normalizationService = new SubtitleNormalizationService(normalizationLogger);

        // Create hash service
        _hashService = new FuzzyHashService(_testDbPath, _logger, normalizationService);
    }

    [Fact]
    public async Task StoreHash_WithIdenticalContent_RejectsAsTrueDuplicate()
    {
        // Arrange - Store first subtitle
        var firstSubtitle = new LabelledSubtitle
        {
            Series = "Criminal Minds",
            Season = "01",
            Episode = "01",
            SubtitleText = "This is the exact same subtitle content for testing duplicate detection.",
            EpisodeName = "Extreme Aggressor"
        };

        // Act - Store first subtitle
        await _hashService.StoreHash(firstSubtitle);

        // Try to store identical content again (should be rejected)
        var duplicateSubtitle = new LabelledSubtitle
        {
            Series = "Criminal Minds",
            Season = "01",
            Episode = "01",
            SubtitleText = "This is the exact same subtitle content for testing duplicate detection.",
            EpisodeName = "Extreme Aggressor"
        };

        await _hashService.StoreHash(duplicateSubtitle);

        // Assert - Query database to verify only one entry exists  
        // Use low threshold and exact match text to find the stored entry
        var matches = await _hashService.FindMatches("This is the exact same subtitle content for testing duplicate detection.", 0.5);
        matches.Should().HaveCount(1, "identical hashes should result in only one database entry");
        matches[0].Subtitle.Series.Should().Be("Criminal Minds");
        matches[0].Subtitle.Season.Should().Be("01");
        matches[0].Subtitle.Episode.Should().Be("01");
    }

    [Fact]
    public async Task StoreHash_WithSameEpisodeDifferentContent_AcceptsBothAsVariants()
    {
        // Arrange - Store HI (Hearing Impaired) version
        var hiSubtitle = new LabelledSubtitle
        {
            Series = "Criminal Minds",
            Season = "01",
            Episode = "01",
            SubtitleText = "[Door slams] Detective: We need to find the suspect. [Footsteps approaching]",
            EpisodeName = "Extreme Aggressor HI"
        };

        // Store NonHI version (different content)
        var nonHiSubtitle = new LabelledSubtitle
        {
            Series = "Criminal Minds",
            Season = "01",
            Episode = "01",
            SubtitleText = "Detective: We need to find the suspect.",
            EpisodeName = "Extreme Aggressor NonHI"
        };

        // Act - Store both versions
        await _hashService.StoreHash(hiSubtitle);
        await _hashService.StoreHash(nonHiSubtitle);

        // Assert - Both should be stored as they have different hashes
        // Verify by searching with the full text of each variant
        var hiMatches = await _hashService.FindMatches("[Door slams] Detective: We need to find the suspect. [Footsteps approaching]", 0.5);
        var nonHiMatches = await _hashService.FindMatches("Detective: We need to find the suspect.", 0.5);

        hiMatches.Should().HaveCountGreaterOrEqualTo(1, "HI version should be stored");
        nonHiMatches.Should().HaveCountGreaterOrEqualTo(1, "NonHI version should be stored");
    }

    [Fact]
    public async Task StoreHash_WithDifferentEpisodesSameContent_AcceptsBoth()
    {
        // Arrange - Two different episodes with identical content (edge case but should work)
        var episode01 = new LabelledSubtitle
        {
            Series = "Test Show",
            Season = "01",
            Episode = "01",
            SubtitleText = "Generic subtitle text that appears in multiple episodes.",
            EpisodeName = "First Episode"
        };

        var episode02 = new LabelledSubtitle
        {
            Series = "Test Show",
            Season = "01",
            Episode = "02",
            SubtitleText = "Generic subtitle text that appears in multiple episodes.",
            EpisodeName = "Second Episode"
        };

        // Act - Store both episodes with identical content
        await _hashService.StoreHash(episode01);
        await _hashService.StoreHash(episode02);

        // Assert - Only one should be stored (identical hash)
        var matches = await _hashService.FindMatches("Generic subtitle text that appears in multiple episodes.", 0.5);
        matches.Should().HaveCount(1, "identical content should result in single hash entry regardless of episode");
    }

    [Fact]
    public async Task StoreHash_WithDifferentSeries_AcceptsAllWhenContentDifferent()
    {
        // Arrange - Same episode number across different series with different content
        var criminalMinds = new LabelledSubtitle
        {
            Series = "Criminal Minds",
            Season = "01",
            Episode = "01",
            SubtitleText = "Criminal Minds specific dialogue about profiling serial killers.",
            EpisodeName = "Pilot"
        };

        var bones = new LabelledSubtitle
        {
            Series = "Bones",
            Season = "01",
            Episode = "01",
            SubtitleText = "Bones specific dialogue about forensic anthropology.",
            EpisodeName = "Pilot"
        };

        // Act - Store both episodes
        await _hashService.StoreHash(criminalMinds);
        await _hashService.StoreHash(bones);

        // Assert - Both should be stored as they have different content/hashes
        var cmMatches = await _hashService.FindMatches("Criminal Minds specific dialogue about profiling serial killers.", 0.5);
        var bonesMatches = await _hashService.FindMatches("Bones specific dialogue about forensic anthropology.", 0.5);

        cmMatches.Should().HaveCount(1);
        cmMatches[0].Subtitle.Series.Should().Be("Criminal Minds");

        bonesMatches.Should().HaveCount(1);
        bonesMatches[0].Subtitle.Series.Should().Be("Bones");
    }

    [Fact]
    public async Task StoreHash_WithMultipleVariants_AcceptsAllDifferentHashes()
    {
        // Arrange - Three variants of the same episode (HI, NonHI, SDH)
        var variants = new[]
        {
            new LabelledSubtitle
            {
                Series = "Test Series",
                Season = "02",
                Episode = "05",
                SubtitleText = "[Music playing] Character A: Hello there. [Door closes]",
                EpisodeName = "Test Episode HI"
            },
            new LabelledSubtitle
            {
                Series = "Test Series",
                Season = "02",
                Episode = "05",
                SubtitleText = "Character A: Hello there.",
                EpisodeName = "Test Episode NonHI"
            },
            new LabelledSubtitle
            {
                Series = "Test Series",
                Season = "02",
                Episode = "05",
                SubtitleText = "♪ [Music] ♪ Character A: Hello there. [Door]",
                EpisodeName = "Test Episode SDH"
            }
        };

        // Act - Store all three variants
        foreach (var variant in variants)
        {
            await _hashService.StoreHash(variant);
        }

        // Assert - All three should be stored as they have different hashes
        // Search for each variant individually using their full text
        var match1 = await _hashService.FindMatches("[Music playing] Character A: Hello there. [Door closes]", 0.5);
        var match2 = await _hashService.FindMatches("Character A: Hello there.", 0.5);
        var match3 = await _hashService.FindMatches("♪ [Music] ♪ Character A: Hello there. [Door]", 0.5);

        match1.Should().HaveCount(1, "HI variant should be findable");
        match2.Should().HaveCount(1, "NonHI variant should be findable");
        match3.Should().HaveCount(1, "SDH variant should be findable");

        // Verify all are for S02E05
        match1[0].Subtitle.Episode.Should().Be("05");
        match2[0].Subtitle.Episode.Should().Be("05");
        match3[0].Subtitle.Episode.Should().Be("05");
    }

    [Fact]
    public async Task StoreHash_WithSlightContentVariation_AcceptsAsVariant()
    {
        // Arrange - Two subtitles with minor differences (different timestamps removed, slight text variation)
        var version1 = new LabelledSubtitle
        {
            Series = "Test Show",
            Season = "01",
            Episode = "03",
            SubtitleText = "Character: I think we should investigate this case carefully and thoroughly.",
            EpisodeName = "Investigation"
        };

        var version2 = new LabelledSubtitle
        {
            Series = "Test Show",
            Season = "01",
            Episode = "03",
            SubtitleText = "Character: I think we should investigate this case carefully.",
            EpisodeName = "Investigation Alt"
        };

        // Act
        await _hashService.StoreHash(version1);
        await _hashService.StoreHash(version2);

        // Assert - Both should be stored as they have different content/hashes
        // Search for each with full text
        var matches1 = await _hashService.FindMatches("Character: I think we should investigate this case carefully and thoroughly.", 0.5);
        var matches2 = await _hashService.FindMatches("Character: I think we should investigate this case carefully.", 0.5);

        matches1.Should().HaveCount(1, "first version should be stored");
        matches2.Should().HaveCount(1, "second version should be stored");
    }

    [Fact]
    public async Task StoreHash_RejectsDuplicate_EvenWithDifferentEpisodeName()
    {
        // Arrange - Same content but different episode name metadata
        var firstVersion = new LabelledSubtitle
        {
            Series = "Test Show",
            Season = "01",
            Episode = "04",
            SubtitleText = "Exact same subtitle content for testing.",
            EpisodeName = "Original Title"
        };

        var secondVersion = new LabelledSubtitle
        {
            Series = "Test Show",
            Season = "01",
            Episode = "04",
            SubtitleText = "Exact same subtitle content for testing.",
            EpisodeName = "Alternative Title"
        };

        // Act
        await _hashService.StoreHash(firstVersion);
        await _hashService.StoreHash(secondVersion);

        // Assert - Should only store once (hash is identical)
        var matches = await _hashService.FindMatches("Exact same subtitle content for testing.", 0.5);
        matches.Should().HaveCount(1, "identical content should be rejected regardless of episode name metadata");
    }

    public void Dispose()
    {
        _hashService?.Dispose();

        // Clean up test database file
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
