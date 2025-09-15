using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using NSubstitute;

namespace EpisodeIdentifier.Tests.Unit.Services;

/// <summary>
/// Unit tests for BulkProcessorService.
/// Tests basic bulk processing orchestration and validation.
/// </summary>
public class BulkProcessorServiceTests : IDisposable
{
    private readonly ILogger<BulkProcessorService> _logger;
    private readonly IFileDiscoveryService _fileDiscoveryService;
    private readonly IProgressTracker _progressTracker;
    private readonly IVideoFileProcessingService _videoFileProcessingService;
    private readonly BulkProcessorService _bulkProcessorService;

    public BulkProcessorServiceTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<BulkProcessorService>();

        _fileDiscoveryService = Substitute.For<IFileDiscoveryService>();
        _progressTracker = Substitute.For<IProgressTracker>();
        _videoFileProcessingService = Substitute.For<IVideoFileProcessingService>();

        _bulkProcessorService = new BulkProcessorService(
            _logger, _fileDiscoveryService, _progressTracker, _videoFileProcessingService);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act & Assert
        _bulkProcessorService.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowException()
    {
        // Arrange & Act & Assert
        var act = () => new BulkProcessorService(
            null!, _fileDiscoveryService, _progressTracker, _videoFileProcessingService);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessAsync_WithNullRequest_ShouldThrowException()
    {
        // Arrange & Act & Assert
        var act = async () => await _bulkProcessorService.ProcessAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateRequestAsync_WithEmptyPaths_ShouldReturnFalse()
    {
        // Arrange
        var request = new BulkProcessingRequest
        {
            RequestId = "test",
            Paths = new List<string>(),
            Options = new BulkProcessingOptions()
        };

        // Act
        var result = await _bulkProcessorService.ValidateRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_WithValidationErrors_ShouldReturnFailedResult()
    {
        // Arrange
        var request = new BulkProcessingRequest
        {
            RequestId = "test",
            Paths = new List<string>(), // Empty paths to trigger validation error
            Options = new BulkProcessingOptions()
        };

        // Act
        var result = await _bulkProcessorService.ProcessAsync(request);

        // Assert
        result.Status.Should().Be(BulkProcessingStatus.Failed);
        result.Errors.Should().NotBeEmpty();
        result.RequestId.Should().Be("test");
    }

    [Fact]
    public async Task ProcessAsync_WithNoFilesFound_ShouldCompleteSuccessfully()
    {
        // Arrange
        var request = new BulkProcessingRequest
        {
            RequestId = "test",
            Paths = new List<string> { "/test/path" },
            Options = new BulkProcessingOptions { BatchSize = 10, MaxConcurrency = 4 }
        };

        // Setup mocks
        _fileDiscoveryService.ValidatePathsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new FileDiscoveryValidationResult { IsValid = true, PathErrors = new Dictionary<string, List<string>>() });

        _fileDiscoveryService.EstimateFileCountAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<BulkProcessingOptions>(), Arg.Any<CancellationToken>())
            .Returns(0);

        _fileDiscoveryService.DiscoverFilesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<BulkProcessingOptions>(), Arg.Any<CancellationToken>())
            .Returns(CreateEmptyAsyncEnumerable());

        // Act
        var result = await _bulkProcessorService.ProcessAsync(request);

        // Assert
        result.Status.Should().Be(BulkProcessingStatus.Completed);
        result.TotalFiles.Should().Be(0);
        result.ProcessedFiles.Should().Be(0);
        result.FailedFiles.Should().Be(0);
        result.SkippedFiles.Should().Be(0);
    }

    private static async IAsyncEnumerable<string> CreateEmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}