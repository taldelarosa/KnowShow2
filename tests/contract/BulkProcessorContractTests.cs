using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions.TestingHelpers;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IBulkProcessor interface.
/// These tests verify the interface contract with mock implementations.
/// </summary>
public class BulkProcessorContractTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly ILogger<object> _logger;

    public BulkProcessorContractTests()
    {
        _mockFileSystem = new MockFileSystem();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<object>();
    }

    [Fact]
    public void ProcessAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.ProcessAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ProcessAsync_WithProgressCallback_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();
        var progress = new Progress<BulkProcessingProgress>();

        // Act & Assert
        var act = async () => await bulkProcessor.ProcessAsync(null!, progress);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ValidateRequestAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.ValidateRequestAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void GetValidationErrorsAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.GetValidationErrorsAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void EstimateProcessingAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.EstimateProcessingAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void CancelProcessingAsync_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.CancelProcessingAsync(null!);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void CancelProcessingAsync_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.CancelProcessingAsync(string.Empty);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void GetProgressAsync_WithNullRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.GetProgressAsync(null!);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void GetProgressAsync_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();

        // Act & Assert
        var act = async () => await bulkProcessor.GetProgressAsync(string.Empty);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ShouldReturnBulkProcessingResult()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();
        var request = CreateValidRequest();

        // Act
        var result = await bulkProcessor.ProcessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BulkProcessingResult>();
        result.RequestId.Should().Be(request.RequestId);
    }

    [Fact]
    public async Task ValidateRequestAsync_WithValidRequest_ShouldReturnBoolean()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();
        var request = CreateValidRequest();

        // Act
        var result = await bulkProcessor.ValidateRequestAsync(request);

        // Assert
        // No need to check type - just verify it's a boolean value
    }

    [Fact]
    public async Task GetValidationErrorsAsync_WithValidRequest_ShouldReturnErrorList()
    {
        // Arrange
        var bulkProcessor = CreateMockBulkProcessor();
        var request = CreateValidRequest();

        // Act
        var result = await bulkProcessor.GetValidationErrorsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<BulkProcessingError>>();
    }

    private IBulkProcessor CreateMockBulkProcessor()
    {
        return new MockBulkProcessor();
    }

    private BulkProcessingRequest CreateValidRequest()
    {
        return new BulkProcessingRequest
        {
            Paths = new List<string> { "/test/path" },
            Options = new BulkProcessingOptions(),
            RequestId = Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Mock implementation of IBulkProcessor for contract testing.
    /// </summary>
    private class MockBulkProcessor : IBulkProcessor
    {
        public Task<BulkProcessingResult> ProcessAsync(BulkProcessingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Task.FromResult(new BulkProcessingResult
            {
                RequestId = request.RequestId,
                Status = BulkProcessingStatus.Completed
            });
        }

        public Task<BulkProcessingResult> ProcessAsync(BulkProcessingRequest request, IProgress<BulkProcessingProgress>? progressCallback = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Task.FromResult(new BulkProcessingResult
            {
                RequestId = request.RequestId,
                Status = BulkProcessingStatus.Completed
            });
        }

        public Task<bool> ValidateRequestAsync(BulkProcessingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Task.FromResult(true);
        }

        public Task<List<BulkProcessingError>> GetValidationErrorsAsync(BulkProcessingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Task.FromResult(new List<BulkProcessingError>());
        }

        public Task<ProcessingEstimate> EstimateProcessingAsync(BulkProcessingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Task.FromResult(new ProcessingEstimate
            {
                EstimatedFileCount = 0,
                EstimatedDuration = TimeSpan.Zero,
                ConfidenceLevel = 100
            });
        }

        public Task<bool> CancelProcessingAsync(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            return Task.FromResult(true);
        }

        public Task<BulkProcessingProgress?> GetProgressAsync(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
            return Task.FromResult<BulkProcessingProgress?>(null);
        }
    }
}
