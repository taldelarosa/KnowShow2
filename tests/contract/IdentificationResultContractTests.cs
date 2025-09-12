using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Models;
using System.Text.Json;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for enhanced IdentificationResult JSON serialization.
/// These tests verify that the JSON contract includes new fields for file renaming.
/// All tests MUST FAIL until IdentificationResult is enhanced with new properties.
/// </summary>
public class IdentificationResultContractTests
{
    [Fact]
    public void IdentificationResult_SerializesToJson_IncludesSuggestedFilename()
    {
        // Arrange
        var result = new IdentificationResult
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.95,
            SuggestedFilename = "The Office - S01E01 - Pilot.mkv" // This property should exist
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("suggestedFilename");
        json.Should().Contain("The Office - S01E01 - Pilot.mkv");
    }

    [Fact]
    public void IdentificationResult_SerializesToJson_IncludesFileRenamedStatus()
    {
        // Arrange
        var result = new IdentificationResult
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.95,
            SuggestedFilename = "The Office - S01E01 - Pilot.mkv",
            FileRenamed = true // This property should exist
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("fileRenamed");
        json.Should().Contain("true");
    }

    [Fact]
    public void IdentificationResult_SerializesToJson_IncludesOriginalFilename()
    {
        // Arrange
        var result = new IdentificationResult
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.95,
            SuggestedFilename = "The Office - S01E01 - Pilot.mkv",
            FileRenamed = true,
            OriginalFilename = "s01e01.mkv" // This property should exist
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("originalFilename");
        json.Should().Contain("s01e01.mkv");
    }

    [Fact]
    public void IdentificationResult_WithNullSuggestedFilename_SerializesCorrectly()
    {
        // Arrange
        var result = new IdentificationResult
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.75, // Low confidence, no filename suggestion
            SuggestedFilename = null,
            FileRenamed = false,
            OriginalFilename = null
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("suggestedFilename");
        json.Should().Contain("null");
        json.Should().Contain("fileRenamed");
        json.Should().Contain("false");
    }

    [Fact]
    public void IdentificationResult_DeserializesFromJson_PreservesNewFields()
    {
        // Arrange
        var json = """
        {
            "series": "The Office",
            "season": "01", 
            "episode": "01",
            "matchConfidence": 0.95,
            "ambiguityNotes": null,
            "error": null,
            "suggestedFilename": "The Office - S01E01 - Pilot.mkv",
            "fileRenamed": true,
            "originalFilename": "s01e01.mkv"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<IdentificationResult>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        result.Should().NotBeNull();
        result!.Series.Should().Be("The Office");
        result.Season.Should().Be("01");
        result.Episode.Should().Be("01");
        result.MatchConfidence.Should().Be(0.95);
        result.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        result.FileRenamed.Should().BeTrue();
        result.OriginalFilename.Should().Be("s01e01.mkv");
    }

    [Fact]
    public void IdentificationResult_WithHighConfidence_HasSuggestedFilenameProperty()
    {
        // Arrange & Act
        var result = new IdentificationResult
        {
            Series = "Breaking Bad",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.98
        };

        // Assert - These properties should exist on the type
        var suggestedFilenameProperty = typeof(IdentificationResult).GetProperty("SuggestedFilename");
        var fileRenamedProperty = typeof(IdentificationResult).GetProperty("FileRenamed");
        var originalFilenameProperty = typeof(IdentificationResult).GetProperty("OriginalFilename");

        suggestedFilenameProperty.Should().NotBeNull("SuggestedFilename property should exist");
        fileRenamedProperty.Should().NotBeNull("FileRenamed property should exist");
        originalFilenameProperty.Should().NotBeNull("OriginalFilename property should exist");

        // Type checks
        suggestedFilenameProperty!.PropertyType.Should().Be(typeof(string), "SuggestedFilename should be nullable string");
        fileRenamedProperty!.PropertyType.Should().Be(typeof(bool), "FileRenamed should be boolean");
        originalFilenameProperty!.PropertyType.Should().Be(typeof(string), "OriginalFilename should be nullable string");
    }

    [Fact]
    public void IdentificationResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new IdentificationResult();

        // Assert
        result.SuggestedFilename.Should().BeNull("SuggestedFilename should default to null");
        result.FileRenamed.Should().BeFalse("FileRenamed should default to false");
        result.OriginalFilename.Should().BeNull("OriginalFilename should default to null");
    }

    [Fact]
    public void IdentificationResult_BackwardCompatibility_PreservesExistingContract()
    {
        // Arrange
        var result = new IdentificationResult
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.95,
            AmbiguityNotes = "Some notes",
            Error = null
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert - Existing fields should still be present
        json.Should().Contain("series");
        json.Should().Contain("season");
        json.Should().Contain("episode");
        json.Should().Contain("matchConfidence");
        json.Should().Contain("ambiguityNotes");
        json.Should().Contain("error");

        // New fields should also be present (even if null/false)
        json.Should().Contain("suggestedFilename");
        json.Should().Contain("fileRenamed");
        json.Should().Contain("originalFilename");
    }

    [Fact]
    public void IdentificationResult_IsAmbiguous_PropertyStillWorks()
    {
        // Arrange
        var ambiguousResult = new IdentificationResult
        {
            MatchConfidence = 0.75,
            AmbiguityNotes = "Multiple matches found"
        };

        var clearResult = new IdentificationResult
        {
            MatchConfidence = 0.95,
            AmbiguityNotes = null
        };

        // Act & Assert
        ambiguousResult.IsAmbiguous.Should().BeTrue();
        clearResult.IsAmbiguous.Should().BeFalse();
    }

    [Fact]
    public void IdentificationResult_HasError_PropertyStillWorks()
    {
        // Arrange
        var errorResult = new IdentificationResult
        {
            Error = IdentificationError.NoSubtitlesFound
        };

        var successResult = new IdentificationResult
        {
            Error = null
        };

        // Act & Assert
        errorResult.HasError.Should().BeTrue();
        successResult.HasError.Should().BeFalse();
    }
}
