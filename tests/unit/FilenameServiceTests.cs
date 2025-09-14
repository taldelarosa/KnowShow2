using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Interfaces;
using NSubstitute;
using Xunit;

namespace EpisodeIdentifier.Tests.Unit;

public class FilenameServiceTests
{
    private readonly FilenameService _filenameService;
    private readonly IAppConfigService _mockConfigService;

    public FilenameServiceTests()
    {
        _mockConfigService = Substitute.For<IAppConfigService>();
        var mockConfig = new AppConfig
        {
            RenameConfidenceThreshold = 0.1
        };
        _mockConfigService.Config.Returns(mockConfig);
        _filenameService = new FilenameService(_mockConfigService);
    }

    [Theory]
    [InlineData("Test<Series>", "Test Series")]  // Angle brackets
    [InlineData("Test|Series", "Test Series")]   // Pipe character
    [InlineData("Test?Series", "Test Series")]   // Question mark
    [InlineData("Test*Series", "Test Series")]   // Asterisk
    [InlineData("Test\"Series", "Test Series")]  // Double quote
    [InlineData("Test:Series", "Test Series")]   // Colon
    [InlineData("Test\\Series", "Test Series")]  // Backslash
    [InlineData("Test/Series", "Test Series")]   // Forward slash
    public void SanitizeForWindows_InvalidCharacters_ReplacesWithSpaces(string input, string expected)
    {
        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CON", "CON_")]        // Reserved device name
    [InlineData("PRN", "PRN_")]        // Reserved device name
    [InlineData("AUX", "AUX_")]        // Reserved device name
    [InlineData("NUL", "NUL_")]        // Reserved device name
    [InlineData("COM1", "COM1_")]      // Reserved device name
    [InlineData("COM9", "COM9_")]      // Reserved device name
    [InlineData("LPT1", "LPT1_")]      // Reserved device name
    [InlineData("LPT9", "LPT9_")]      // Reserved device name
    public void SanitizeForWindows_ReservedNames_AppendsUnderscore(string input, string expected)
    {
        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("   Test Series   ", "Test Series")]    // Leading and trailing spaces
    [InlineData("\tTest\tSeries\t", "Test Series")]     // Tabs
    [InlineData("Test    Series", "Test Series")]       // Multiple spaces
    [InlineData("Test\n\rSeries", "Test Series")]       // Newlines and carriage returns
    public void SanitizeForWindows_WhitespaceHandling_NormalizesCorrectly(string input, string expected)
    {
        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Test.", "Test")]      // Trailing period
    [InlineData("Test..", "Test")]     // Multiple trailing periods
    [InlineData("Test. ", "Test")]     // Trailing period and space
    [InlineData("Test ", "Test")]      // Trailing space only
    public void SanitizeForWindows_TrailingCharacters_RemovesCorrectly(string input, string expected)
    {
        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeForWindows_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = _filenameService.SanitizeForWindows("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void SanitizeForWindows_OnlyInvalidCharacters_ReturnsEmptyAfterTrim()
    {
        // Act
        var result = _filenameService.SanitizeForWindows("<<<>>>");

        // Assert
        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("Test\u0001Series", "Test Series")]     // Control character
    [InlineData("Test\u001FSeries", "Test Series")]    // Control character
    [InlineData("Test\u007FSeries", "Test Series")]    // DEL character
    public void SanitizeForWindows_ControlCharacters_ReplacesWithSpaces(string input, string expected)
    {
        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Test Series - S01E01 - Episode Name", 50, "Test Series - S01E01 - Episode Name")]  // No truncation needed
    [InlineData("Very Long Series Name That Exceeds The Maximum Length Limit", 30, "Very Long Series Name That Ex")]  // Truncation needed
    [InlineData("Test Series - S01E01 - Very Long Episode Name That Should Be Truncated.mkv", 50, "Test Series - S01E01 - Very Long Episode.mkv")]  // Preserve extension
    public void TruncateToLimit_VariousLengths_HandlesCorrectly(string input, int maxLength, string expected)
    {
        // Act
        var result = _filenameService.TruncateToLimit(input, maxLength);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(result.Length <= maxLength);
    }

    [Fact]
    public void TruncateToLimit_PreservesFileExtension()
    {
        // Arrange
        var input = "Very Long Filename That Should Be Truncated But Keep Extension.mkv";
        var maxLength = 30;

        // Act
        var result = _filenameService.TruncateToLimit(input, maxLength);

        // Assert
        Assert.EndsWith(".mkv", result);
        Assert.True(result.Length <= maxLength);
    }

    [Theory]
    [InlineData("filename.txt", 260, true)]   // Valid length
    [InlineData("filename.txt", 10, false)]   // Too short for content
    public void IsValidWindowsFilename_PathLength_ValidatesCorrectly(string filename, int maxPathLength, bool expected)
    {
        // Act - use the overload that considers path length
        var result = _filenameService.IsValidWindowsFilename(filename, maxPathLength);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("valid-filename.txt", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("filename<invalid>.txt", false)]
    [InlineData("CON", false)]
    [InlineData("filename.", false)]
    public void IsValidWindowsFilename_EdgeCases_ValidatesCorrectly(string filename, bool expected)
    {
        // Act
        var result = _filenameService.IsValidWindowsFilename(filename);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValidWindowsFilename_MaxPathLength_ReturnsFalse()
    {
        // Arrange - Create a filename that would exceed Windows MAX_PATH (260 characters)
        var longFilename = new string('a', 300) + ".mkv";

        // Act
        var result = _filenameService.IsValidWindowsFilename(longFilename);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidWindowsFilename_ExactMaxPathLength_ReturnsTrue()
    {
        // Arrange - Create a filename at exactly the Windows limit (255 characters for filename)
        var maxLengthFilename = new string('a', 251) + ".mkv"; // 251 + 4 = 255

        // Act
        var result = _filenameService.IsValidWindowsFilename(maxLengthFilename);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidWindowsFilename_JustOverMaxPathLength_ReturnsFalse()
    {
        // Arrange - Create a filename just over the Windows limit
        var overLimitFilename = new string('a', 252) + ".mkv"; // 252 + 4 = 256

        // Act
        var result = _filenameService.IsValidWindowsFilename(overLimitFilename);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(50, true)]    // Well under limit
    [InlineData(200, true)]   // Near limit but valid
    [InlineData(255, true)]   // At limit
    [InlineData(256, false)]  // Over limit
    [InlineData(300, false)]  // Well over limit
    public void IsValidWindowsFilename_VariousPathLengths_ValidatesCorrectly(int filenameLength, bool expected)
    {
        // Arrange
        var filename = new string('a', filenameLength - 4) + ".mkv";

        // Act
        var result = _filenameService.IsValidWindowsFilename(filename);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TruncateToLimit_WindowsMaxPath_TruncatesCorrectly()
    {
        // Arrange
        var longFilename = new string('a', 300) + ".mkv";
        var maxLength = 255; // Windows filename limit

        // Act
        var result = _filenameService.TruncateToLimit(longFilename, maxLength);

        // Assert
        Assert.True(result.Length <= maxLength);
        Assert.EndsWith(".mkv", result);
        Assert.True(_filenameService.IsValidWindowsFilename(result));
    }

    [Fact]
    public void TruncateToLimit_PreservesExtensionUnderWindowsLimit()
    {
        // Arrange
        var longBasename = new string('a', 280);
        var filename = longBasename + ".mkv";

        // Act
        var result = _filenameService.TruncateToLimit(filename, 255);

        // Assert
        Assert.True(result.Length <= 255);
        Assert.EndsWith(".mkv", result);
        Assert.Equal(255, result.Length); // Should use full allowed length
    }

    [Theory]
    [InlineData("Series - S01E01 - Episode.mkv", 100, "Series - S01E01 - Episode.mkv")]  // No truncation
    [InlineData("Very Long Series Name With Long Episode Title.mkv", 30, "Very Long Series Name Wi.mkv")]  // Truncation preserves extension
    [InlineData("Test.mkv", 8, "Test.mkv")]  // Extension longer than allowed space handled gracefully
    [InlineData("Test.mkv", 4, ".mkv")]  // Minimum case - only extension
    public void TruncateToLimit_WindowsCompatibility_HandlesEdgeCases(string input, int maxLength, string expected)
    {
        // Act
        var result = _filenameService.TruncateToLimit(input, maxLength);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(result.Length <= maxLength);
    }
}
