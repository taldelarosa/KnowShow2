using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Threading.Tasks;
using System.IO;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Tests.Contract;

[TestClass]
public class FuzzyHashDbTests
{
    private string GetTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
    }

    private ILogger<FuzzyHashService> CreateLogger()
    {
        return LoggerFactory.Create(builder => builder.AddConsole())
                          .CreateLogger<FuzzyHashService>();
    }

    [TestMethod]
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

        var tempFile = GetTempFilePath();
        try
        {
            var service = new FuzzyHashService(tempFile, CreateLogger());

            // Act
            await service.StoreHash(subtitle);
            var matches = await service.FindMatches(subtitleText);

            // Assert
            Assert.AreEqual(1, matches.Count, "Should find exactly one match");
            Assert.AreEqual(subtitle.Series, matches[0].Subtitle.Series);
            Assert.AreEqual(subtitle.Season, matches[0].Subtitle.Season);
            Assert.AreEqual(subtitle.Episode, matches[0].Subtitle.Episode);
            Assert.AreEqual(1.0, matches[0].Confidence, 0.001, "Should have 100% confidence for exact match");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
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

        var tempFile = GetTempFilePath();
        try
        {
            var service = new FuzzyHashService(tempFile, CreateLogger());

            // Act
            await service.StoreHash(storedSubtitle);
            var matches = await service.FindMatches(unmatchedSubtitle);

            // Assert
            Assert.AreEqual(0, matches.Count, "Should find no matches above threshold");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task AmbiguousMatch_ReturnsMultipleResults()
    {
        // Arrange
        var similarContent = "The quick brown fox";
        var similarSubtitles = new[]
        {
            new LabelledSubtitle { Series = "Show1", Season = "01", Episode = "01", SubtitleText = "The quick brown fox jumps over" },
            new LabelledSubtitle { Series = "Show2", Season = "02", Episode = "02", SubtitleText = "The quick brown fox runs fast" }
        };

        var tempFile = GetTempFilePath();
        try
        {
            var service = new FuzzyHashService(tempFile, CreateLogger());

            // Act
            foreach (var subtitle in similarSubtitles)
            {
                await service.StoreHash(subtitle);
            }
            var matches = await service.FindMatches(similarContent, threshold: 0.5);

            // Assert
            Assert.IsTrue(matches.Count >= 2, "Should find multiple similar matches");
            foreach (var match in matches)
            {
                Assert.IsTrue(match.Confidence >= 0.5, "All matches should meet threshold");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
