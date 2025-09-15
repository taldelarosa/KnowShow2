using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using EpisodeIdentifier.Core.Models.Hashing;
using EpisodeIdentifier.Core.Services.Hashing;

namespace EpisodeIdentifier.Core.Tests.Unit.Services.Hashing
{
    /// <summary>
    /// Unit tests for text search fallback functionality
    /// </summary>
    public class TextSearchFallbackTests
    {
        [Fact]
        public void FileComparisonResult_SupportsTextFallback()
        {
            // Arrange & Act
            var result = FileComparisonResult.SuccessWithTextFallback(
                "hash1", "hash2", 45, 85, true,
                TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50),
                "Test Series", "1", "1");

            // Assert
            Assert.True(result.UsedTextFallback);
            Assert.Equal(45, result.SimilarityScore);
            Assert.Equal(85, result.TextSimilarityScore);
            Assert.Equal(TimeSpan.FromMilliseconds(50), result.TextFallbackTime);
            Assert.Equal("Test Series", result.MatchedSeries);
            Assert.Equal("1", result.MatchedSeason);
            Assert.Equal("1", result.MatchedEpisode);
        }

        [Fact]
        public void FileComparisonResult_StandardSuccess_DoesNotUseFallback()
        {
            // Arrange & Act
            var result = FileComparisonResult.Success(
                "hash1", "hash2", 85, true, TimeSpan.FromMilliseconds(10));

            // Assert
            Assert.False(result.UsedTextFallback);
            Assert.Equal(85, result.SimilarityScore);
            Assert.Equal(0, result.TextSimilarityScore); // Default value
            Assert.Equal(TimeSpan.Zero, result.TextFallbackTime); // Default value
        }

        [Fact]
        public void EnhancedComparisonResult_SuccessWithTextFallback_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var result = EnhancedComparisonResult.SuccessWithTextFallback(
                40, 80, true,
                TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(45),
                "Test Show", "2", "3", "Test Episode");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.IsMatch);
            Assert.True(result.UsedTextFallback);
            Assert.Equal(40, result.HashSimilarityScore);
            Assert.Equal(80, result.TextSimilarityScore);
            Assert.Equal("Test Show", result.MatchedSeries);
            Assert.Equal("2", result.MatchedSeason);
            Assert.Equal("3", result.MatchedEpisode);
            Assert.Equal("Test Episode", result.MatchedEpisodeName);
        }

        [Fact]
        public void EnhancedComparisonResult_Success_DoesNotUseFallback()
        {
            // Arrange & Act
            var result = EnhancedComparisonResult.Success(
                80, true, TimeSpan.FromMilliseconds(5),
                "Test Show", "1", "1", "Pilot");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.IsMatch);
            Assert.False(result.UsedTextFallback);
            Assert.Equal(80, result.HashSimilarityScore);
            Assert.Equal(0, result.TextSimilarityScore); // Default value
        }

        [Fact]
        public void EnhancedComparisonResult_Failure_SetsErrorState()
        {
            // Arrange & Act
            var result = EnhancedComparisonResult.Failure("Test error");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(result.IsMatch);
            Assert.False(result.UsedTextFallback);
            Assert.Equal("Test error", result.ErrorMessage);
        }

        [Fact]
        public void EnhancedComparisonResult_NoMatch_SetsCorrectState()
        {
            // Arrange & Act
            var result = EnhancedComparisonResult.NoMatch(TimeSpan.FromMilliseconds(10));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsMatch);
            Assert.False(result.UsedTextFallback);
            Assert.Equal(TimeSpan.FromMilliseconds(10), result.HashComparisonTime);
        }
    }
}