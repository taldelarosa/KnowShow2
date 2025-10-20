using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IVobSubExtractor interface.
/// These tests define the expected behavior of any IVobSubExtractor implementation.
/// </summary>
public class VobSubExtractorContractTests
{
    private readonly string _testDataPath;

    public VobSubExtractorContractTests()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test-videos");
    }

    [Fact(Skip = "Requires test data file that doesn't exist")]
    public async Task ExtractAsync_WithValidMkvAndDvdSubtitle_ReturnsSuccessWithPaths()
    {
        // Arrange
        var extractor = CreateExtractor();
        var videoPath = Path.Combine(_testDataPath, "test-with-dvd-subtitle.mkv");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"vobsub_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDirectory);
        var cancellationToken = new CancellationToken();

        // Act
        var result = await extractor.ExtractAsync(videoPath, trackIndex: 3, outputDirectory, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IdxFilePath.Should().NotBeNullOrEmpty();
        result.SubFilePath.Should().NotBeNullOrEmpty();
        File.Exists(result.IdxFilePath).Should().BeTrue();
        File.Exists(result.SubFilePath).Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.TrackIndex.Should().Be(3);
        result.SourceVideoPath.Should().Be(videoPath);
        result.ExtractionDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);

        // Cleanup
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_WithNonExistentFile_ThrowsArgumentException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var videoPath = "/nonexistent/video.mkv";
        var outputDirectory = Path.GetTempPath();
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await extractor.ExtractAsync(videoPath, trackIndex: 0, outputDirectory, cancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*video*file*exist*");
    }

    [Fact(Skip = "Requires test data file that doesn't exist")]
    public async Task ExtractAsync_WithInvalidTrackIndex_ReturnsFailureResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var videoPath = Path.Combine(_testDataPath, "test-with-dvd-subtitle.mkv");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"vobsub_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDirectory);
        var cancellationToken = new CancellationToken();

        // Act
        var result = await extractor.ExtractAsync(videoPath, trackIndex: 999, outputDirectory, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.IdxFilePath.Should().BeNullOrEmpty();
        result.SubFilePath.Should().BeNullOrEmpty();

        // Cleanup
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact(Skip = "Requires mkvtoolnix (mkvextract) to be installed - not available in CI")]
    public async Task IsMkvExtractAvailableAsync_WhenToolInstalled_ReturnsTrue()
    {
        // Arrange
        var extractor = CreateExtractor();

        // Act
        var isAvailable = await extractor.IsMkvExtractAvailableAsync();

        // Assert
        // Note: This test may fail on systems without mkvextract installed
        // In CI/CD, ensure mkvextract is available in the test environment
        isAvailable.Should().BeTrue("mkvextract should be available in the test environment");
    }

    [Fact(Skip = "Requires test data file that doesn't exist")]
    public async Task ExtractAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var videoPath = Path.Combine(_testDataPath, "test-with-dvd-subtitle.mkv");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"vobsub_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDirectory);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = async () => await extractor.ExtractAsync(videoPath, trackIndex: 3, outputDirectory, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Cleanup
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_WithNullVideoPath_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var outputDirectory = Path.GetTempPath();
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await extractor.ExtractAsync(null!, trackIndex: 0, outputDirectory, cancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExtractAsync_WithNullOutputDirectory_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var videoPath = Path.Combine(_testDataPath, "test-with-dvd-subtitle.mkv");
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await extractor.ExtractAsync(videoPath, trackIndex: 0, null!, cancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExtractAsync_WithNegativeTrackIndex_ThrowsArgumentException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var videoPath = Path.Combine(_testDataPath, "test-with-dvd-subtitle.mkv");
        var outputDirectory = Path.GetTempPath();
        var cancellationToken = new CancellationToken();

        // Act & Assert
        var act = async () => await extractor.ExtractAsync(videoPath, trackIndex: -1, outputDirectory, cancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*track*index*");
    }

    /// <summary>
    /// Creates an instance of IVobSubExtractor for testing.
    /// </summary>
    private IVobSubExtractor CreateExtractor()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<VobSubExtractor>();
        return new VobSubExtractor(logger);
    }
}
