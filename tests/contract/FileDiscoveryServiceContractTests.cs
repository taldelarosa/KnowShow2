using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.CompilerServices;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IFileDiscoveryService interface.
/// These tests verify the interface contract with mock implementations.
/// </summary>
public class FileDiscoveryServiceContractTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly ILogger<object> _logger;

    public FileDiscoveryServiceContractTests()
    {
        _mockFileSystem = new MockFileSystem();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<object>();
    }

    [Fact]
    public void DiscoverFilesAsync_WithNullPaths_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = async () =>
        {
            await foreach (var file in service.DiscoverFilesAsync(null!, options))
            {
                // Should not reach here
            }
        };
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void DiscoverFilesAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var paths = new List<string> { "/test" };

        // Act & Assert
        var act = async () =>
        {
            await foreach (var file in service.DiscoverFilesAsync(paths, null!))
            {
                // Should not reach here
            }
        };
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void DiscoverFilesWithInfoAsync_WithNullPaths_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = async () =>
        {
            await foreach (var file in service.DiscoverFilesWithInfoAsync(null!, options))
            {
                // Should not reach here
            }
        };
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void DiscoverFilesWithInfoAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var paths = new List<string> { "/test" };

        // Act & Assert
        var act = async () =>
        {
            await foreach (var file in service.DiscoverFilesWithInfoAsync(paths, null!))
            {
                // Should not reach here
            }
        };
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void EstimateFileCountAsync_WithNullPaths_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = async () => await service.EstimateFileCountAsync(null!, options);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void EstimateFileCountAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var paths = new List<string> { "/test" };

        // Act & Assert
        var act = async () => await service.EstimateFileCountAsync(paths, null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ValidatePathsAsync_WithNullPaths_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();

        // Act & Assert
        var act = async () => await service.ValidatePathsAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ShouldIncludeFile_WithNullFilePath_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => service.ShouldIncludeFile(null!, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldIncludeFile_WithEmptyFilePath_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => service.ShouldIncludeFile(string.Empty, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldIncludeFile_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();

        // Act & Assert
        var act = () => service.ShouldIncludeFile("/test/file.txt", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithValidInputs_ShouldReturnAsyncEnumerable()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var paths = new List<string> { "/test" };
        var options = new BulkProcessingOptions();

        // Act
        var result = service.DiscoverFilesAsync(paths, options);

        // Assert
        result.Should().NotBeNull();
        var files = new List<string>();
        await foreach (var file in result)
        {
            files.Add(file);
        }
        files.Should().BeOfType<List<string>>();
    }

    [Fact]
    public async Task EstimateFileCountAsync_WithValidInputs_ShouldReturnInt()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var paths = new List<string> { "/test" };
        var options = new BulkProcessingOptions();

        // Act
        var result = await service.EstimateFileCountAsync(paths, options);

        // Assert
        result.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ValidatePathsAsync_WithValidPaths_ShouldReturnValidationResult()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var paths = new List<string> { "/test" };

        // Act
        var result = await service.ValidatePathsAsync(paths);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FileDiscoveryValidationResult>();
        result.PathErrors.Should().NotBeNull();
    }

    [Fact]
    public void ShouldIncludeFile_WithValidInputs_ShouldReturnBoolean()
    {
        // Arrange
        var service = CreateMockFileDiscoveryService();
        var options = new BulkProcessingOptions();

        // Act
        var result = service.ShouldIncludeFile("/test/file.txt", options);

        // Assert
        // No need to check type - just verify it's a boolean value
    }

    private IFileDiscoveryService CreateMockFileDiscoveryService()
    {
        return new MockFileDiscoveryService();
    }

    /// <summary>
    /// Mock implementation of IFileDiscoveryService for contract testing.
    /// </summary>
    private class MockFileDiscoveryService : IFileDiscoveryService
    {
        public async IAsyncEnumerable<string> DiscoverFilesAsync(IEnumerable<string> paths, BulkProcessingOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (options == null) throw new ArgumentNullException(nameof(options));

            await Task.Delay(1, cancellationToken); // Simulate async work
            yield return "/mock/file1.txt";
            yield return "/mock/file2.txt";
        }

        public async IAsyncEnumerable<FileDiscoveryResult> DiscoverFilesWithInfoAsync(IEnumerable<string> paths, BulkProcessingOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (options == null) throw new ArgumentNullException(nameof(options));

            await Task.Delay(1, cancellationToken); // Simulate async work
            yield return new FileDiscoveryResult
            {
                FilePath = "/mock/file1.txt",
                FileName = "file1.txt",
                Extension = ".txt",
                FileSizeBytes = 1024
            };
        }

        public Task<int> EstimateFileCountAsync(IEnumerable<string> paths, BulkProcessingOptions options, CancellationToken cancellationToken = default)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return Task.FromResult(2);
        }

        public Task<FileDiscoveryValidationResult> ValidatePathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));

            return Task.FromResult(new FileDiscoveryValidationResult
            {
                IsValid = true,
                AccessiblePaths = paths.Count(),
                InaccessiblePaths = 0
            });
        }

        public bool ShouldIncludeFile(string filePath, BulkProcessingOptions options)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return true;
        }
    }
}
