using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Contract;

[TestClass]
public class CliOutputTests
{
    [TestMethod]
    public async Task SuccessfulIdentification_OutputsCorrectJson()
    {
        // Arrange
        var expectedJson = new
        {
            series = "TestShow",
            season = "01",
            episode = "02",
            matchConfidence = 0.97,
            ambiguityNotes = (string)null,
            error = (object)null
        };

        // Act
        // TODO: Implement CLI execution and output capture
        string actualJson = "{}"; // Placeholder

        // Assert
        var expectedObj = JsonSerializer.Serialize(expectedJson);
        var actualObj = JsonSerializer.Deserialize<dynamic>(actualJson);
        Assert.IsNotNull(actualObj);
        // TODO: Add property comparisons after implementing CLI
    }

    [TestMethod]
    public async Task NoSubtitlesFound_OutputsErrorJson()
    {
        // Arrange
        var expectedJson = new
        {
            error = new
            {
                code = "NO_SUBTITLES_FOUND",
                message = "No PGS subtitles could be extracted from the video file."
            }
        };

        // Act
        // TODO: Implement CLI execution and output capture
        string actualJson = "{}"; // Placeholder

        // Assert
        var expectedObj = JsonSerializer.Serialize(expectedJson);
        var actualObj = JsonSerializer.Deserialize<dynamic>(actualJson);
        Assert.IsNotNull(actualObj);
        // TODO: Add error property comparisons after implementing CLI
    }

    [TestMethod]
    public async Task UnsupportedFileType_OutputsErrorJson()
    {
        // Arrange
        var expectedJson = new
        {
            error = new
            {
                code = "UNSUPPORTED_FILE_TYPE",
                message = "The provided file is not AV1 encoded."
            }
        };

        // Act
        // TODO: Implement CLI execution and output capture
        string actualJson = "{}"; // Placeholder

        // Assert
        var expectedObj = JsonSerializer.Serialize(expectedJson);
        var actualObj = JsonSerializer.Deserialize<dynamic>(actualJson);
        Assert.IsNotNull(actualObj);
        // TODO: Add error property comparisons after implementing CLI
    }
}
