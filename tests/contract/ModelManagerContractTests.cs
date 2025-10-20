using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IModelManager interface.
/// These tests define the expected behavior of any IModelManager implementation.
/// Tests are marked as Skip until implementation exists (TDD RED phase).
/// </summary>
public class ModelManagerContractTests
{
    private readonly string _testModelCachePath;

    public ModelManagerContractTests()
    {
        _testModelCachePath = Path.Combine(Path.GetTempPath(), "episodeidentifier-test-models");
    }

    private IModelManager CreateModelManager()
    {
        // TODO: Replace with actual implementation once ModelManager exists
        throw new NotImplementedException("ModelManager not yet implemented - this is expected in TDD RED phase");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task EnsureModelAvailable_OnFirstRun_DownloadsModel()
    {
        // Arrange
        var modelManager = CreateModelManager();

        // Clean up any cached models for a fresh test
        if (Directory.Exists(_testModelCachePath))
        {
            Directory.Delete(_testModelCachePath, recursive: true);
        }

        // Act
        await modelManager.EnsureModelAvailable();

        // Assert
        var modelInfo = modelManager.GetModelInfo();
        modelInfo.Should().NotBeNull();
        File.Exists(modelInfo!.ModelPath).Should().BeTrue("model file should exist after download");
        File.Exists(modelInfo.TokenizerPath).Should().BeTrue("tokenizer file should exist after download");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task EnsureModelAvailable_WhenModelExists_SkipsDownload()
    {
        // Arrange
        var modelManager = CreateModelManager();

        // First call downloads the model
        await modelManager.EnsureModelAvailable();
        var firstCallInfo = modelManager.GetModelInfo();

        // Act
        // Second call should use cached model
        await modelManager.EnsureModelAvailable();
        var secondCallInfo = modelManager.GetModelInfo();

        // Assert
        secondCallInfo.Should().NotBeNull();
        secondCallInfo!.ModelPath.Should().Be(firstCallInfo!.ModelPath,
            "should use same cached model file");
        secondCallInfo.LastVerified.Should().BeOnOrAfter(firstCallInfo.LastVerified,
            "verification timestamp should be updated or same");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task LoadModel_WithValidModelFile_ReturnsMetadata()
    {
        // Arrange
        var modelManager = CreateModelManager();
        await modelManager.EnsureModelAvailable();

        // Act
        var modelInfo = await modelManager.LoadModel();

        // Assert
        modelInfo.Should().NotBeNull();
        modelInfo.ModelName.Should().Be("all-MiniLM-L6-v2");
        modelInfo.Dimension.Should().Be(384);
        modelInfo.Variant.Should().NotBeNullOrEmpty();
        modelInfo.ModelPath.Should().NotBeNullOrEmpty();
        modelInfo.TokenizerPath.Should().NotBeNullOrEmpty();
        modelInfo.ModelSizeBytes.Should().BeGreaterThan(0);
        modelInfo.Sha256Hash.Should().NotBeNullOrEmpty();
        modelInfo.LastVerified.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromMinutes(1));
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task LoadModel_WhenModelNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var modelManager = CreateModelManager();

        // Clean up any cached models
        if (Directory.Exists(_testModelCachePath))
        {
            Directory.Delete(_testModelCachePath, recursive: true);
        }

        // Act & Assert
        var act = async () => await modelManager.LoadModel();
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*model*file*not*found*");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void GetModelInfo_BeforeModelLoaded_ReturnsNull()
    {
        // Arrange
        var modelManager = CreateModelManager();

        // Act
        var modelInfo = modelManager.GetModelInfo();

        // Assert
        modelInfo.Should().BeNull("no model info available before model is loaded");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void GetModelInfo_AfterModelLoaded_ReturnsCachedMetadata()
    {
        // Arrange
        var modelManager = CreateModelManager();
        modelManager.EnsureModelAvailable().Wait();
        modelManager.LoadModel().Wait();

        // Act
        var modelInfo1 = modelManager.GetModelInfo();
        var modelInfo2 = modelManager.GetModelInfo();

        // Assert
        modelInfo1.Should().NotBeNull();
        modelInfo2.Should().NotBeNull();
        modelInfo1.Should().BeSameAs(modelInfo2,
            "GetModelInfo should return cached instance without re-reading disk");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task VerifyModel_WithValidFile_ReturnsTrue()
    {
        // Arrange
        var modelManager = CreateModelManager();
        await modelManager.EnsureModelAvailable();
        var modelInfo = await modelManager.LoadModel();

        // Act
        var isValid = await modelManager.VerifyModel(modelInfo.ModelPath);

        // Assert
        isValid.Should().BeTrue("model file should pass SHA256 verification");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task VerifyModel_WithCorruptedFile_ReturnsFalse()
    {
        // Arrange
        var modelManager = CreateModelManager();

        // Create a corrupted file
        var corruptedPath = Path.Combine(_testModelCachePath, "corrupted-model.onnx");
        Directory.CreateDirectory(_testModelCachePath);
        await File.WriteAllTextAsync(corruptedPath, "This is not a valid ONNX model file");

        // Act
        var isValid = await modelManager.VerifyModel(corruptedPath);

        // Assert
        isValid.Should().BeFalse("corrupted file should fail SHA256 verification");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task VerifyModel_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var modelManager = CreateModelManager();
        var nonExistentPath = "/path/to/nonexistent/model.onnx";

        // Act & Assert
        var act = async () => await modelManager.VerifyModel(nonExistentPath);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task DeleteCachedModel_RemovesModelFiles()
    {
        // Arrange
        var modelManager = CreateModelManager();
        await modelManager.EnsureModelAvailable();
        var modelInfo = await modelManager.LoadModel();
        var modelPath = modelInfo.ModelPath;

        // Act
        await modelManager.DeleteCachedModel();

        // Assert
        File.Exists(modelPath).Should().BeFalse("model file should be deleted");
        modelManager.GetModelInfo().Should().BeNull("model info should be cleared after deletion");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task DownloadModel_WithValidUrl_SavesFile()
    {
        // Arrange
        var modelManager = CreateModelManager();
        var testUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
        var destinationPath = Path.Combine(_testModelCachePath, "downloaded-model.onnx");

        Directory.CreateDirectory(_testModelCachePath);

        // Act
        await modelManager.DownloadModel(testUrl, destinationPath);

        // Assert
        File.Exists(destinationPath).Should().BeTrue("model file should be downloaded");
        var fileInfo = new FileInfo(destinationPath);
        fileInfo.Length.Should().BeGreaterThan(0, "downloaded file should have content");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task DownloadModel_WithInvalidUrl_ThrowsHttpRequestException()
    {
        // Arrange
        var modelManager = CreateModelManager();
        var invalidUrl = "https://invalid-domain-that-does-not-exist-12345.com/model.onnx";
        var destinationPath = Path.Combine(_testModelCachePath, "model.onnx");

        // Act & Assert
        var act = async () => await modelManager.DownloadModel(invalidUrl, destinationPath);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public async Task EnsureModelAvailable_OnSubsequentRuns_CompletesQuickly()
    {
        // Arrange
        var modelManager = CreateModelManager();

        // First call (may download)
        await modelManager.EnsureModelAvailable();

        // Act - Second call should be fast (uses cache)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await modelManager.EnsureModelAvailable();
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "cached model availability check should be very fast");
    }
}
