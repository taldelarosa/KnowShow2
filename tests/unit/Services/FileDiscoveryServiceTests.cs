using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions.TestingHelpers;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Tests.Unit.Services;

/// <summary>
/// Unit tests for FileDiscoveryService.
/// Uses mock file system for isolation and fast execution.
/// </summary>
public class FileDiscoveryServiceTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly ILogger<FileDiscoveryService> _logger;
    private readonly FileDiscoveryService _service;

    public FileDiscoveryServiceTests()
    {
        _mockFileSystem = new MockFileSystem();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<FileDiscoveryService>();
        _service = new FileDiscoveryService(_mockFileSystem, _logger);
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithSingleFile_ShouldReturnFile()
    {
        // Arrange
        var filePath = "/test/file.txt";
        _mockFileSystem.AddFile(filePath, new MockFileData("test content"));
        var paths = new[] { filePath };
        var options = new BulkProcessingOptions();

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Should().Be(filePath);
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithDirectory_ShouldReturnFilesInDirectory()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/file2.txt", new MockFileData("content2"));
        _mockFileSystem.AddFile("/test/file3.jpg", new MockFileData("content3"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions();

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain("/test/file1.txt");
        results.Should().Contain("/test/file2.txt");
        results.Should().Contain("/test/file3.jpg");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithRecursiveDirectory_ShouldReturnAllFiles()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/subdir/file2.txt", new MockFileData("content2"));
        _mockFileSystem.AddFile("/test/subdir/nested/file3.txt", new MockFileData("content3"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions { Recursive = true };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain("/test/file1.txt");
        results.Should().Contain("/test/subdir/file2.txt");
        results.Should().Contain("/test/subdir/nested/file3.txt");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithNonRecursiveDirectory_ShouldReturnOnlyTopLevelFiles()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/subdir/file2.txt", new MockFileData("content2"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions { Recursive = false };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().ContainSingle();
        results.Should().Contain("/test/file1.txt");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithMaxDepth_ShouldRespectDepthLimit()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/level1/file2.txt", new MockFileData("content2"));
        _mockFileSystem.AddFile("/test/level1/level2/file3.txt", new MockFileData("content3"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions { Recursive = true, MaxDepth = 1 };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain("/test/file1.txt");
        results.Should().Contain("/test/level1/file2.txt");
        results.Should().NotContain("/test/level1/level2/file3.txt");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithIncludeExtensions_ShouldFilterByExtension()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/file2.jpg", new MockFileData("content2"));
        _mockFileSystem.AddFile("/test/file3.txt", new MockFileData("content3"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions
        {
            IncludeExtensions = new List<string> { ".txt" }
        };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain("/test/file1.txt");
        results.Should().Contain("/test/file3.txt");
        results.Should().NotContain("/test/file2.jpg");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithExcludeExtensions_ShouldExcludeSpecifiedExtensions()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/file2.jpg", new MockFileData("content2"));
        _mockFileSystem.AddFile("/test/file3.png", new MockFileData("content3"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions
        {
            ExcludeExtensions = new List<string> { ".jpg", ".png" }
        };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().ContainSingle();
        results.Should().Contain("/test/file1.txt");
        results.Should().NotContain("/test/file2.jpg");
        results.Should().NotContain("/test/file3.png");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithExcludeOverridingInclude_ShouldPrioritizeExclude()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/file2.txt", new MockFileData("content2"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions
        {
            IncludeExtensions = new List<string> { ".txt" },
            ExcludeExtensions = new List<string> { ".txt" }
        };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithCancellation_ShouldStopProcessing()
    {
        // Arrange
        var directoryPath = "/test";
        for (int i = 0; i < 10; i++)
        {
            _mockFileSystem.AddFile($"/test/file{i}.txt", new MockFileData($"content{i}"));
        }
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions();
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var results = new List<string>();
        var fileCount = 0;

        var act = async () =>
        {
            await foreach (var file in _service.DiscoverFilesAsync(paths, options, cts.Token))
            {
                results.Add(file);
                fileCount++;

                // Cancel after processing a few files
                if (fileCount >= 3)
                {
                    cts.Cancel();
                }
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        results.Should().HaveCount(3); // Should have processed exactly 3 files before cancellation
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithNonExistentPath_ShouldThrowException()
    {
        // Arrange
        var paths = new[] { "/nonexistent" };
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = async () =>
        {
            await foreach (var file in _service.DiscoverFilesAsync(paths, options))
            {
                // Should not reach here
            }
        };

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task EstimateFileCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var directoryPath = "/test";
        _mockFileSystem.AddFile("/test/file1.txt", new MockFileData("content1"));
        _mockFileSystem.AddFile("/test/file2.txt", new MockFileData("content2"));
        _mockFileSystem.AddFile("/test/subdir/file3.txt", new MockFileData("content3"));
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions { Recursive = true };

        // Act
        var count = await _service.EstimateFileCountAsync(paths, options);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task ValidatePathsAsync_WithValidPaths_ShouldReturnValid()
    {
        // Arrange
        _mockFileSystem.AddFile("/test/file.txt", new MockFileData("content"));
        _mockFileSystem.AddDirectory("/test/directory");
        var paths = new[] { "/test/file.txt", "/test/directory" };

        // Act
        var result = await _service.ValidatePathsAsync(paths);

        // Assert
        result.IsValid.Should().BeTrue();
        result.AccessiblePaths.Should().Be(2);
        result.InaccessiblePaths.Should().Be(0);
        result.PathErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidatePathsAsync_WithInvalidPaths_ShouldReturnInvalid()
    {
        // Arrange
        var paths = new[] { "/nonexistent/file.txt", "/another/nonexistent" };

        // Act
        var result = await _service.ValidatePathsAsync(paths);

        // Assert
        result.IsValid.Should().BeFalse();
        result.AccessiblePaths.Should().Be(0);
        result.InaccessiblePaths.Should().Be(2);
        result.PathErrors.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("/test/file.txt", new string[] { ".txt" }, new string[0], true)]
    [InlineData("/test/file.txt", new string[] { ".jpg" }, new string[0], false)]
    [InlineData("/test/file.txt", new string[0], new string[] { ".txt" }, false)]
    [InlineData("/test/file.txt", new string[0], new string[] { ".jpg" }, true)]
    [InlineData("/test/file.txt", new string[] { ".txt" }, new string[] { ".txt" }, false)] // Exclude takes precedence
    public void ShouldIncludeFile_WithVariousFilters_ShouldReturnExpectedResult(
        string filePath, string[] includeExtensions, string[] excludeExtensions, bool expected)
    {
        // Arrange
        var options = new BulkProcessingOptions
        {
            IncludeExtensions = includeExtensions.ToList(),
            ExcludeExtensions = excludeExtensions.ToList()
        };

        // Act
        var result = _service.ShouldIncludeFile(filePath, options);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldIncludeFile_WithNullFilePath_ShouldThrowException()
    {
        // Arrange
        var options = new BulkProcessingOptions();

        // Act & Assert
        var act = () => _service.ShouldIncludeFile(null!, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldIncludeFile_WithNullOptions_ShouldThrowException()
    {
        // Arrange & Act & Assert
        var act = () => _service.ShouldIncludeFile("/test/file.txt", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DiscoverFilesWithInfoAsync_ShouldReturnFileDetails()
    {
        // Arrange
        var directoryPath = "/test";
        var fileContent = "test content";
        _mockFileSystem.AddFile("/test/file.txt", new MockFileData(fileContent)
        {
            CreationTime = new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            LastWriteTime = new DateTime(2023, 1, 2, 10, 0, 0, DateTimeKind.Utc)
        });
        var paths = new[] { directoryPath };
        var options = new BulkProcessingOptions();

        // Act
        var results = new List<FileDiscoveryResult>();
        await foreach (var file in _service.DiscoverFilesWithInfoAsync(paths, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().ContainSingle();
        var fileInfo = results[0];
        fileInfo.FilePath.Should().Be("/test/file.txt");
        fileInfo.FileName.Should().Be("file.txt");
        fileInfo.Extension.Should().Be(".txt");
        fileInfo.FileSizeBytes.Should().Be(fileContent.Length);
        fileInfo.CreatedTime.Should().Be(new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        fileInfo.ModifiedTime.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0, DateTimeKind.Utc));
    }
}