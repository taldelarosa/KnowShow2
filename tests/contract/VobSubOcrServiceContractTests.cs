using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IVobSubOcrService interface.
/// These tests define the expected behavior of any IVobSubOcrService implementation.
/// </summary>
public class VobSubOcrServiceContractTests
{
    private readonly string _testDataPath;

    public VobSubOcrServiceContractTests()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test-vobsub");
    }

    [Fact(Skip = "Requires test data files that don't exist")]
    public async Task PerformOcrAsync_WithValidVobSubFiles_ReturnsSuccessWithText()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = Path.Combine(_testDataPath, "test-subtitle.idx");
        var subFilePath = Path.Combine(_testDataPath, "test-subtitle.sub");
        var cancellationToken = new CancellationToken();

        // Act
        var result = await ocrService.PerformOcrAsync(idxFilePath, subFilePath, language: "eng", cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().NotBeNullOrEmpty();
        result.CharacterCount.Should().BeGreaterThan(0);
        result.ConfidenceScore.Should().BeInRange(0.0, 1.0);
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.ImageCount.Should().BeGreaterThan(0);
        result.Language.Should().Be("eng");
        result.OcrDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task PerformOcrAsync_WithMissingIdxFile_ThrowsArgumentException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = "/nonexistent/subtitle.idx";
        var subFilePath = Path.Combine(_testDataPath, "test-subtitle.sub");
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await ocrService.PerformOcrAsync(idxFilePath, subFilePath, language: "eng", cancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*.idx*file*exist*");
    }

    [Fact]
    public async Task PerformOcrAsync_WithMissingSubFile_ThrowsArgumentException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        // Use a valid path for idx file but missing sub file
        // Create a temporary file to serve as the .idx file
        var tempIdxFile = Path.Combine(Path.GetTempPath(), $"temp_test_{Guid.NewGuid()}.idx");
        File.WriteAllText(tempIdxFile, "# dummy idx file");
        
        try
        {
            var subFilePath = "/nonexistent/subtitle.sub";
            var cancellationToken = new CancellationToken();

            // Act & Assert
            var act = async () => await ocrService.PerformOcrAsync(tempIdxFile, subFilePath, language: "eng", cancellationToken);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*.sub*file*exist*");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempIdxFile))
            {
                File.Delete(tempIdxFile);
            }
        }
    }

    [Fact(Skip = "Requires test data files that don't exist")]
    public async Task PerformOcrAsync_WithNoTextInImages_ReturnsSuccessWithEmptyText()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = Path.Combine(_testDataPath, "empty-subtitle.idx");
        var subFilePath = Path.Combine(_testDataPath, "empty-subtitle.sub");
        var cancellationToken = new CancellationToken();

        // Act
        var result = await ocrService.PerformOcrAsync(idxFilePath, subFilePath, language: "eng", cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().BeEmpty();
        result.CharacterCount.Should().Be(0);
        result.ConfidenceScore.Should().Be(0.0);
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task IsTesseractAvailableAsync_WhenToolInstalled_ReturnsTrue()
    {
        // Arrange
        var ocrService = CreateOcrService();

        // Act
        var isAvailable = await ocrService.IsTesseractAvailableAsync();

        // Assert
        // Note: This test may fail on systems without Tesseract installed
        // In CI/CD, ensure Tesseract is available in the test environment
        isAvailable.Should().BeTrue("Tesseract should be available in the test environment");
    }

    [Fact]
    public void GetOcrLanguageCode_WithValidLanguage_ReturnsCode()
    {
        // Arrange
        var ocrService = CreateOcrService();

        // Act
        var languageCode = ocrService.GetOcrLanguageCode("eng");

        // Assert
        languageCode.Should().Be("eng");
    }

    [Theory]
    [InlineData("eng", "eng")]
    [InlineData("english", "eng")]
    [InlineData("spa", "spa")]
    [InlineData("spanish", "spa")]
    [InlineData("fra", "fra")]
    [InlineData("french", "fra")]
    public void GetOcrLanguageCode_WithVariousLanguages_ReturnsCorrectCode(string input, string expected)
    {
        // Arrange
        var ocrService = CreateOcrService();

        // Act
        var languageCode = ocrService.GetOcrLanguageCode(input);

        // Assert
        languageCode.Should().Be(expected);
    }

    [Fact(Skip = "Requires test data files that don't exist")]
    public async Task PerformOcrAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = Path.Combine(_testDataPath, "test-subtitle.idx");
        var subFilePath = Path.Combine(_testDataPath, "test-subtitle.sub");
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = async () => await ocrService.PerformOcrAsync(idxFilePath, subFilePath, language: "eng", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PerformOcrAsync_WithNullIdxFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var subFilePath = Path.Combine(_testDataPath, "test-subtitle.sub");
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await ocrService.PerformOcrAsync(null!, subFilePath, language: "eng", cancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PerformOcrAsync_WithNullSubFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = Path.Combine(_testDataPath, "test-subtitle.idx");
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await ocrService.PerformOcrAsync(idxFilePath, null!, language: "eng", cancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PerformOcrAsync_WithNullLanguage_ThrowsArgumentNullException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = Path.Combine(_testDataPath, "test-subtitle.idx");
        var subFilePath = Path.Combine(_testDataPath, "test-subtitle.sub");
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await ocrService.PerformOcrAsync(idxFilePath, subFilePath, language: null!, cancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PerformOcrAsync_WithEmptyLanguage_ThrowsArgumentException()
    {
        // Arrange
        var ocrService = CreateOcrService();
        var idxFilePath = Path.Combine(_testDataPath, "test-subtitle.idx");
        var subFilePath = Path.Combine(_testDataPath, "test-subtitle.sub");
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await ocrService.PerformOcrAsync(idxFilePath, subFilePath, language: "", cancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*language*empty*");
    }

    /// <summary>
    /// Creates an instance of IVobSubOcrService for testing.
    /// </summary>
    private IVobSubOcrService CreateOcrService()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<VobSubOcrService>();
        return new VobSubOcrService(logger);
    }
}
