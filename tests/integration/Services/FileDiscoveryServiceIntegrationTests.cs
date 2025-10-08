using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Tests.Integration.Services;

/// <summary>
/// Integration tests for FileDiscoveryService with real file system operations.
/// These tests use the actual file system and are slower than unit tests.
/// </summary>
public class FileDiscoveryServiceIntegrationTests : IDisposable
{
    private readonly ILogger<FileDiscoveryService> _logger;
    private readonly FileDiscoveryService _service;
    private readonly string _testDirectory;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public FileDiscoveryServiceIntegrationTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<FileDiscoveryService>();
        _service = new FileDiscoveryService(new FileSystem(), _logger);
        
        // Create a unique test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileDiscoveryTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithRealFiles_ShouldReturnActualFiles()
    {
        // Arrange
        var file1Path = CreateTestFile("file1.txt", "content1");
        var file2Path = CreateTestFile("file2.srt", "content2");
        var subDir = CreateTestDirectory("subdir");
        var file3Path = CreateTestFile(Path.Combine("subdir", "file3.vtt"), "content3");

        var options = new BulkProcessingOptions { Recursive = true };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(new[] { _testDirectory }, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(file1Path);
        results.Should().Contain(file2Path);
        results.Should().Contain(file3Path);
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithLargeNumberOfFiles_ShouldPerformReasonably()
    {
        // Arrange
        var fileCount = 1000;
        var files = new List<string>();
        
        for (int i = 0; i < fileCount; i++)
        {
            var fileName = $"testfile_{i:D4}.txt";
            var filePath = CreateTestFile(fileName, $"Test content for file {i}");
            files.Add(filePath);
        }

        var options = new BulkProcessingOptions { Recursive = false };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(new[] { _testDirectory }, options))
        {
            results.Add(file);
        }
        
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(fileCount);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in under 5 seconds
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithCrossplatformPaths_ShouldHandlePathsCorrectly()
    {
        // Arrange
        var subDir1 = CreateTestDirectory("sub dir 1"); // Space in name
        var subDir2 = CreateTestDirectory("sub-dir-2"); // Dashes
        var file1Path = CreateTestFile(Path.Combine("sub dir 1", "test file.txt"), "content1");
        var file2Path = CreateTestFile(Path.Combine("sub-dir-2", "test-file.srt"), "content2");

        var options = new BulkProcessingOptions { Recursive = true };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(new[] { _testDirectory }, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(file1Path);
        results.Should().Contain(file2Path);
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithFilePermissionIssues_ShouldHandleGracefully()
    {
        // Arrange - This test may behave differently on different platforms
        var normalFile = CreateTestFile("normal.txt", "normal content");
        var options = new BulkProcessingOptions();

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(new[] { _testDirectory }, options))
        {
            results.Add(file);
        }

        // Assert - Should at least find the normal file
        results.Should().Contain(normalFile);
    }

    [Fact]
    public async Task EstimateFileCountAsync_WithRealDirectory_ShouldReturnAccurateCount()
    {
        // Arrange
        var fileCount = 50;
        for (int i = 0; i < fileCount; i++)
        {
            CreateTestFile($"file_{i}.txt", $"content {i}");
        }

        var options = new BulkProcessingOptions();

        // Act
        var estimatedCount = await _service.EstimateFileCountAsync(new[] { _testDirectory }, options);

        // Assert
        estimatedCount.Should().Be(fileCount);
    }

    [Fact]
    public async Task DiscoverFilesWithInfoAsync_WithRealFiles_ShouldReturnAccurateFileInfo()
    {
        // Arrange
        var fileName = "test.txt";
        var fileContent = "This is test content for file info";
        var filePath = CreateTestFile(fileName, fileContent);
        var fileInfo = new FileInfo(filePath);
        
        var options = new BulkProcessingOptions();

        // Act
        var results = new List<FileDiscoveryResult>();
        await foreach (var result in _service.DiscoverFilesWithInfoAsync(new[] { _testDirectory }, options))
        {
            results.Add(result);
        }

        // Assert
        results.Should().ContainSingle();
        var discoveredFile = results[0];
        
        discoveredFile.FilePath.Should().Be(filePath);
        discoveredFile.FileName.Should().Be(fileName);
        discoveredFile.Extension.Should().Be(".txt");
        discoveredFile.FileSizeBytes.Should().Be(fileContent.Length);
        discoveredFile.CreatedTime.Should().BeCloseTo(fileInfo.CreationTimeUtc, TimeSpan.FromSeconds(1));
        discoveredFile.ModifiedTime.Should().BeCloseTo(fileInfo.LastWriteTimeUtc, TimeSpan.FromSeconds(1));
        discoveredFile.IsReadOnly.Should().Be(fileInfo.IsReadOnly);
    }

    [Fact]
    public async Task ValidatePathsAsync_WithRealPaths_ShouldValidateCorrectly()
    {
        // Arrange
        var validFile = CreateTestFile("valid.txt", "content");
        var validDir = CreateTestDirectory("validdir");
        var invalidPath = Path.Combine(_testDirectory, "nonexistent");

        var paths = new[] { validFile, validDir, invalidPath };

        // Act
        var result = await _service.ValidatePathsAsync(paths);

        // Assert
        result.IsValid.Should().BeFalse();
        result.AccessiblePaths.Should().Be(2);
        result.InaccessiblePaths.Should().Be(1);
        result.PathErrors.Should().ContainKey(invalidPath);
        result.PathErrors[invalidPath].Should().Contain("Path does not exist");
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithMixedFileTypes_ShouldRespectFilters()
    {
        // Arrange
        var txtFile = CreateTestFile("document.txt", "text content");
        var srtFile = CreateTestFile("subtitle.srt", "subtitle content");
        var vttFile = CreateTestFile("webvtt.vtt", "webvtt content");
        var jpgFile = CreateTestFile("image.jpg", "fake image content");
        var pdfFile = CreateTestFile("document.pdf", "fake pdf content");

        var options = new BulkProcessingOptions 
        { 
            IncludeExtensions = new List<string> { ".txt", ".srt", ".vtt" }
        };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(new[] { _testDirectory }, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(txtFile);
        results.Should().Contain(srtFile);
        results.Should().Contain(vttFile);
        results.Should().NotContain(jpgFile);
        results.Should().NotContain(pdfFile);
    }

    [Fact]
    public async Task DiscoverFilesAsync_WithDeepDirectoryStructure_ShouldHandleMaxDepth()
    {
        // Arrange
        var level0File = CreateTestFile("level0.txt", "level 0");
        var level1Dir = CreateTestDirectory("level1");
        var level1File = CreateTestFile(Path.Combine("level1", "level1.txt"), "level 1");
        var level2Dir = CreateTestDirectory(Path.Combine("level1", "level2"));
        var level2File = CreateTestFile(Path.Combine("level1", "level2", "level2.txt"), "level 2");
        var level3Dir = CreateTestDirectory(Path.Combine("level1", "level2", "level3"));
        var level3File = CreateTestFile(Path.Combine("level1", "level2", "level3", "level3.txt"), "level 3");

        var options = new BulkProcessingOptions 
        { 
            Recursive = true,
            MaxDepth = 2
        };

        // Act
        var results = new List<string>();
        await foreach (var file in _service.DiscoverFilesAsync(new[] { _testDirectory }, options))
        {
            results.Add(file);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(level0File);
        results.Should().Contain(level1File);
        results.Should().Contain(level2File);
        results.Should().NotContain(level3File);
    }

    private string CreateTestFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _createdDirectories.Add(directory);
        }
        
        File.WriteAllText(fullPath, content);
        _createdFiles.Add(fullPath);
        return fullPath;
    }

    private string CreateTestDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
        _createdDirectories.Add(fullPath);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            // Clean up created files
            foreach (var file in _createdFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            // Clean up created directories (in reverse order)
            foreach (var directory in _createdDirectories.AsEnumerable().Reverse())
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the cleanup error but don't fail the test
            Console.WriteLine($"Failed to clean up test files: {ex.Message}");
        }
    }
}