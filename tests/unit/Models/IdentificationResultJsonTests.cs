using Xunit;
using EpisodeIdentifier.Core.Models;
using System.Text.Json;

namespace EpisodeIdentifier.Tests.Unit.Models
{
    public class IdentificationResultJsonTests
    {
        [Fact]
        public void IdentificationResult_ShouldSerializeNewMatchingMethodFields()
        {
            // Arrange
            var result = new IdentificationResult
            {
                Series = "Test Series",
                Season = "01",
                Episode = "01",
                EpisodeName = "Test Episode",
                MatchConfidence = 0.85,
                MatchingMethod = "TextFallback",
                UsedTextFallback = true,
                HashSimilarityScore = 75,
                TextSimilarityScore = 85
            };

            // Act
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            // Assert
            Assert.Contains("\"matchingMethod\": \"TextFallback\"", json);
            Assert.Contains("\"usedTextFallback\": true", json);
            Assert.Contains("\"hashSimilarityScore\": 75", json);
            Assert.Contains("\"textSimilarityScore\": 85", json);

            // Ensure the JSON contains all expected fields
            var deserialized = JsonSerializer.Deserialize<IdentificationResult>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.Equal("TextFallback", deserialized?.MatchingMethod);
            Assert.True(deserialized?.UsedTextFallback);
            Assert.Equal(75, deserialized?.HashSimilarityScore);
            Assert.Equal(85, deserialized?.TextSimilarityScore);
        }

        [Fact]
        public void IdentificationResult_DefaultValues_ShouldSerializeCorrectly()
        {
            // Arrange
            var result = new IdentificationResult
            {
                Series = "Test Series",
                Season = "01",
                Episode = "01",
                MatchConfidence = 0.90
                // New fields should have default values
            };

            // Act  
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            // Assert
            Assert.Contains("\"matchingMethod\": null", json);
            Assert.Contains("\"usedTextFallback\": false", json);
            Assert.Contains("\"hashSimilarityScore\": null", json);
            Assert.Contains("\"textSimilarityScore\": null", json);
        }
    }
}
