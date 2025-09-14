using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using System.Text.Json;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for file rename functionality with --rename CLI flag.
/// These tests verify end-to-end automatic file renaming behavior.
/// All tests MUST FAIL until the full integration is implemented.
/// </summary>
public class FileRenameIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<FileRenameIntegrationTests> _logger;
    private readonly List<string> _testFilesToCleanup = new();

    public FileRenameIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Register services
        services.AddSingleton<IAppConfigService, TestAppConfigService>();
        services.AddScoped<IFilenameService, FilenameService>();
        services.AddScoped<IFileRenameService, FileRenameService>();
        services.AddScoped<SubtitleNormalizationService>();
        services.AddScoped<FuzzyHashService>(provider =>
            new FuzzyHashService(
                ":memory:", // Use in-memory SQLite database for tests
                provider.GetRequiredService<ILogger<FuzzyHashService>>(),
                provider.GetRequiredService<SubtitleNormalizationService>()));

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<FileRenameIntegrationTests>>();
    }

    [Fact]
    public async Task RenameFlag_WithHighConfidenceMatch_RenamesFileSuccessfully()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();
        var fileRenameService = _serviceProvider.GetRequiredService<IFileRenameService>();

        var originalFile = CreateTestVideoFile("original_video.mkv");
        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            MatchConfidence = 0.95
        };

        // Act - Simulate CLI workflow with --rename flag
        var identificationResult = new IdentificationResult
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            MatchConfidence = episodeData.MatchConfidence
        };

        // Generate filename suggestion
        var filenameRequest = new FilenameGenerationRequest
        {
            Series = identificationResult.Series!,
            Season = identificationResult.Season!,
            Episode = identificationResult.Episode!,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = Path.GetExtension(originalFile),
            MatchConfidence = identificationResult.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        // Perform rename operation (--rename flag behavior)
        if (filenameResult.IsValid)
        {
            var renameRequest = new FileRenameRequest
            {
                OriginalPath = originalFile,
                SuggestedFilename = filenameResult.SuggestedFilename,
                ForceOverwrite = false
            };

            var renameResult = await fileRenameService.RenameFileAsync(renameRequest);

            if (renameResult.Success)
            {
                identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;
                identificationResult.FileRenamed = true;
                identificationResult.OriginalFilename = Path.GetFileName(originalFile);
            }
        }

        // Assert
        identificationResult.Should().NotBeNull();
        identificationResult.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        identificationResult.FileRenamed.Should().BeTrue();
        identificationResult.OriginalFilename.Should().Be("original_video.mkv");

        // Verify file was actually renamed
        File.Exists(originalFile).Should().BeFalse();

        var expectedNewPath = Path.Combine(Path.GetDirectoryName(originalFile)!, "The Office - S01E01 - Pilot.mkv");
        File.Exists(expectedNewPath).Should().BeTrue();
        _testFilesToCleanup.Add(expectedNewPath);

        _logger.LogInformation("File successfully renamed from {Original} to {New}",
            Path.GetFileName(originalFile), "The Office - S01E01 - Pilot.mkv");
    }

    [Fact]
    public async Task RenameFlag_WithLowConfidence_DoesNotRenameFile()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();
        var fileRenameService = _serviceProvider.GetRequiredService<IFileRenameService>();

        var originalFile = CreateTestVideoFile("low_confidence_video.mkv");
        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            MatchConfidence = 0.75 // Below threshold
        };

        // Act - Simulate CLI workflow with --rename flag but low confidence
        var identificationResult = new IdentificationResult
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            MatchConfidence = episodeData.MatchConfidence
        };

        // Try to generate filename suggestion
        var filenameRequest = new FilenameGenerationRequest
        {
            Series = identificationResult.Series!,
            Season = identificationResult.Season!,
            Episode = identificationResult.Episode!,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = Path.GetExtension(originalFile),
            MatchConfidence = identificationResult.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        // Should not attempt rename due to validation failure
        if (filenameResult.IsValid)
        {
            var renameRequest = new FileRenameRequest
            {
                OriginalPath = originalFile,
                SuggestedFilename = filenameResult.SuggestedFilename,
                ForceOverwrite = false
            };

            var renameResult = await fileRenameService.RenameFileAsync(renameRequest);

            if (renameResult.Success)
            {
                identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;
                identificationResult.FileRenamed = true;
                identificationResult.OriginalFilename = Path.GetFileName(originalFile);
            }
        }
        else
        {
            identificationResult.FileRenamed = false;
        }

        // Assert
        identificationResult.Should().NotBeNull();
        identificationResult.SuggestedFilename.Should().BeNull();
        identificationResult.FileRenamed.Should().BeFalse();
        identificationResult.OriginalFilename.Should().BeNull();

        // Verify original file still exists
        File.Exists(originalFile).Should().BeTrue();
        _testFilesToCleanup.Add(originalFile);

        _logger.LogInformation("Low confidence file not renamed: {Filename}", Path.GetFileName(originalFile));
    }

    [Fact]
    public async Task RenameFlag_WithExistingTargetFile_ReturnsAppropriateError()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();
        var fileRenameService = _serviceProvider.GetRequiredService<IFileRenameService>();

        var originalFile = CreateTestVideoFile("source_video.mkv");
        var existingTarget = CreateTestVideoFile("The Office - S01E01 - Pilot.mkv");

        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            MatchConfidence = 0.95
        };

        // Act - Simulate CLI workflow with existing target file
        var identificationResult = new IdentificationResult
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            MatchConfidence = episodeData.MatchConfidence
        };

        var filenameRequest = new FilenameGenerationRequest
        {
            Series = identificationResult.Series!,
            Season = identificationResult.Season!,
            Episode = identificationResult.Episode!,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = Path.GetExtension(originalFile),
            MatchConfidence = identificationResult.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);
        FileRenameResult? renameResult = null;

        if (filenameResult.IsValid)
        {
            var renameRequest = new FileRenameRequest
            {
                OriginalPath = originalFile,
                SuggestedFilename = filenameResult.SuggestedFilename,
                ForceOverwrite = false // Don't overwrite existing files
            };

            renameResult = await fileRenameService.RenameFileAsync(renameRequest);

            identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;
            identificationResult.FileRenamed = renameResult.Success;
            identificationResult.OriginalFilename = Path.GetFileName(originalFile);
        }

        // Assert
        identificationResult.Should().NotBeNull();
        identificationResult.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        identificationResult.FileRenamed.Should().BeFalse();
        identificationResult.OriginalFilename.Should().Be("source_video.mkv");

        renameResult.Should().NotBeNull();
        renameResult!.Success.Should().BeFalse();
        renameResult.ErrorType.Should().Be(FileRenameError.TargetExists);
        renameResult.ErrorMessage.Should().Contain("already exists");

        // Verify both files still exist
        File.Exists(originalFile).Should().BeTrue();
        File.Exists(existingTarget).Should().BeTrue();
        _testFilesToCleanup.Add(originalFile);
        _testFilesToCleanup.Add(existingTarget);

        _logger.LogInformation("Rename failed due to existing target: {Error}", renameResult.ErrorMessage);
    }

    [Fact]
    public async Task RenameFlag_WithForceOverwrite_OverwritesExistingFile()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();
        var fileRenameService = _serviceProvider.GetRequiredService<IFileRenameService>();

        var originalFile = CreateTestVideoFile("source_video.mkv", "original content");
        var existingTarget = CreateTestVideoFile("The Office - S01E01 - Pilot.mkv", "existing content");

        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            MatchConfidence = 0.95
        };

        // Act - Simulate CLI workflow with --force flag
        var identificationResult = new IdentificationResult
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            MatchConfidence = episodeData.MatchConfidence
        };

        var filenameRequest = new FilenameGenerationRequest
        {
            Series = identificationResult.Series!,
            Season = identificationResult.Season!,
            Episode = identificationResult.Episode!,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = Path.GetExtension(originalFile),
            MatchConfidence = identificationResult.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        if (filenameResult.IsValid)
        {
            var renameRequest = new FileRenameRequest
            {
                OriginalPath = originalFile,
                SuggestedFilename = filenameResult.SuggestedFilename,
                ForceOverwrite = true // Force overwrite
            };

            var renameResult = await fileRenameService.RenameFileAsync(renameRequest);

            if (renameResult.Success)
            {
                identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;
                identificationResult.FileRenamed = true;
                identificationResult.OriginalFilename = Path.GetFileName(originalFile);
            }
        }

        // Assert
        identificationResult.Should().NotBeNull();
        identificationResult.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        identificationResult.FileRenamed.Should().BeTrue();
        identificationResult.OriginalFilename.Should().Be("source_video.mkv");

        // Verify original file was renamed/moved and content is preserved
        File.Exists(originalFile).Should().BeFalse();
        File.Exists(existingTarget).Should().BeTrue();

        var content = File.ReadAllText(existingTarget);
        content.Should().Be("original content"); // Should contain original file content
        _testFilesToCleanup.Add(existingTarget);

        _logger.LogInformation("File successfully overwritten: {Filename}", "The Office - S01E01 - Pilot.mkv");
    }

    [Fact]
    public async Task RenameFlag_CompleteWorkflowSimulation_ReturnsCorrectJson()
    {
        // Arrange - Simulate complete CLI command: episodeidentifier --rename /path/to/video.mkv
        var originalFile = CreateTestVideoFile("episode.mkv");
        var args = new[] { "--rename", originalFile };

        // This simulates the main Program.cs HandleCommand method
        var identificationResult = await SimulateCompleteWorkflow(originalFile, renameFile: true);

        // Act - Serialize to JSON (CLI output)
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(identificationResult, jsonOptions);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("suggestedFilename");
        json.Should().Contain("fileRenamed");
        json.Should().Contain("originalFilename");
        json.Should().Contain("The Office - S01E01 - Pilot.mkv");
        json.Should().Contain("true"); // fileRenamed should be true
        json.Should().Contain("episode.mkv"); // originalFilename

        _logger.LogInformation("Complete workflow JSON output:\n{Json}", json);
    }

    [Fact]
    public async Task NoRenameFlag_GeneratesSuggestionButDoesNotRename()
    {
        // Arrange - Simulate CLI command without --rename flag
        var originalFile = CreateTestVideoFile("episode_no_rename.mkv");

        // This simulates the main Program.cs HandleCommand method without rename flag
        var identificationResult = await SimulateCompleteWorkflow(originalFile, renameFile: false);

        // Assert
        identificationResult.Should().NotBeNull();
        identificationResult.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        identificationResult.FileRenamed.Should().BeFalse();
        identificationResult.OriginalFilename.Should().BeNull(); // Not set when no rename

        // Verify original file still exists with original name
        File.Exists(originalFile).Should().BeTrue();
        _testFilesToCleanup.Add(originalFile);

        _logger.LogInformation("No rename flag - suggestion generated but file not renamed");
    }

    // Helper method to simulate the complete workflow
    private async Task<IdentificationResult> SimulateCompleteWorkflow(string videoFile, bool renameFile)
    {
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();
        var fileRenameService = _serviceProvider.GetRequiredService<IFileRenameService>();

        // Simulate episode identification (normally from subtitle analysis)
        var identificationResult = new IdentificationResult
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            MatchConfidence = 0.95
        };

        // Generate filename suggestion for high confidence matches
        if (identificationResult.MatchConfidence >= 0.9)
        {
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = identificationResult.Series!,
                Season = identificationResult.Season!,
                Episode = identificationResult.Episode!,
                EpisodeName = "Pilot", // Would come from database
                FileExtension = Path.GetExtension(videoFile),
                MatchConfidence = identificationResult.MatchConfidence
            };

            var filenameResult = filenameService.GenerateFilename(filenameRequest);

            if (filenameResult.IsValid)
            {
                identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;

                // Perform rename if --rename flag is set
                if (renameFile)
                {
                    var renameRequest = new FileRenameRequest
                    {
                        OriginalPath = videoFile,
                        SuggestedFilename = filenameResult.SuggestedFilename,
                        ForceOverwrite = false
                    };

                    var renameResult = await fileRenameService.RenameFileAsync(renameRequest);

                    identificationResult.FileRenamed = renameResult.Success;
                    identificationResult.OriginalFilename = Path.GetFileName(videoFile);

                    if (renameResult.Success && renameResult.NewPath != null)
                    {
                        _testFilesToCleanup.Add(renameResult.NewPath);
                    }
                }
            }
        }

        return identificationResult;
    }

    // Helper method to create test video files
    private string CreateTestVideoFile(string filename, string content = "test video content")
    {
        var tempDir = Path.GetTempPath();
        var filePath = Path.Combine(tempDir, filename);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        // Cleanup test files
        foreach (var file in _testFilesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        _serviceProvider?.Dispose();
    }
}
