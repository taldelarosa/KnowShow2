using System.Security.Cryptography;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Manages ONNX model download, caching, and verification.
/// Downloads sentence transformer models from configurable URLs (typically Hugging Face).
/// </summary>
public class ModelManager : IModelManager
{
    private readonly ILogger<ModelManager> _logger;
    private readonly EmbeddingModelConfiguration _modelConfig;
    private ModelInfo? _cachedModelInfo;
    private readonly string _modelCacheDirectory;

    public ModelManager(ILogger<ModelManager> logger, EmbeddingModelConfiguration? modelConfig = null)
    {
        _logger = logger;
        _modelConfig = modelConfig ?? EmbeddingModelConfiguration.Default;

        // Cache directory: ~/.episodeidentifier/models/{ModelName}/
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _modelCacheDirectory = Path.Combine(homeDirectory, ".episodeidentifier", "models", _modelConfig.Name);
    }

    /// <inheritdoc/>
    public async Task EnsureModelAvailable()
    {
        _logger.LogInformation("Ensuring model {ModelName} is available...", _modelConfig.Name);

        var modelPath = GetModelPath();
        var tokenizerPath = GetTokenizerPath();

        // Check if both files exist
        if (File.Exists(modelPath) && File.Exists(tokenizerPath))
        {
            _logger.LogInformation("Model files found in cache: {CacheDir}", _modelCacheDirectory);

            // Verify integrity
            if (await VerifyModel(modelPath))
            {
                _logger.LogInformation("Model verification successful");
                await LoadModel();
                return;
            }

            _logger.LogWarning("Model verification failed. Re-downloading...");
        }

        // Download models
        _logger.LogInformation("Downloading model from configured URL: {Url}", _modelConfig.ModelUrl);
        Directory.CreateDirectory(_modelCacheDirectory);

        await DownloadModel(_modelConfig.ModelUrl, modelPath);
        await DownloadModel(_modelConfig.TokenizerUrl, tokenizerPath);

        // Verify downloaded model
        if (!await VerifyModel(modelPath))
        {
            throw new InvalidOperationException($"Downloaded model failed verification. Expected SHA256: {_modelConfig.ModelSha256}");
        }

        _logger.LogInformation("Model download and verification complete");
        await LoadModel();
    }

    /// <inheritdoc/>
    public async Task<ModelInfo> LoadModel()
    {
        var modelPath = GetModelPath();
        var tokenizerPath = GetTokenizerPath();

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        }

        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer file not found: {tokenizerPath}");
        }

        // Get file info
        var modelFileInfo = new FileInfo(modelPath);
        var modelSizeBytes = modelFileInfo.Length;

        // Calculate SHA256
        string sha256Hash;
        using (var stream = File.OpenRead(modelPath))
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        _cachedModelInfo = new ModelInfo(
            modelName: _modelConfig.Name,
            variant: "onnx",
            dimension: _modelConfig.Dimensions,
            modelPath: modelPath,
            tokenizerPath: tokenizerPath,
            sha256Hash: sha256Hash,
            modelSizeBytes: modelSizeBytes,
            lastVerified: DateTime.UtcNow
        );

        _logger.LogInformation("Loaded model: {ModelDesc}", _cachedModelInfo.GetDescription());
        return _cachedModelInfo;
    }

    /// <inheritdoc/>
    public ModelInfo? GetModelInfo()
    {
        return _cachedModelInfo;
    }

    /// <inheritdoc/>
    public async Task DeleteCachedModel()
    {
        _logger.LogInformation("Deleting cached model from {CacheDir}", _modelCacheDirectory);

        if (Directory.Exists(_modelCacheDirectory))
        {
            Directory.Delete(_modelCacheDirectory, recursive: true);
            _logger.LogInformation("Cached model deleted successfully");
        }

        _cachedModelInfo = null;
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyModel(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found for verification: {modelPath}");
        }

        _logger.LogDebug("Verifying model integrity: {ModelPath}", modelPath);

        try
        {
            // Calculate SHA256 hash
            string actualHash;
            using (var stream = File.OpenRead(modelPath))
            {
                using var sha256 = SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(stream);
                actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            // Skip strict hash verification if "SKIP" or placeholder hash is configured
            // This allows development/testing without the actual hash values
            if (_modelConfig.ModelSha256.Equals("SKIP", StringComparison.OrdinalIgnoreCase) ||
                _modelConfig.ModelSha256.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Model SHA256 verification skipped (configured as: {ConfigValue})", _modelConfig.ModelSha256);
                _logger.LogInformation("Actual model SHA256: {ActualHash}", actualHash);
                return true;
            }

            var isValid = actualHash.Equals(_modelConfig.ModelSha256, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogError("Model verification failed. Expected: {Expected}, Actual: {Actual}",
                    _modelConfig.ModelSha256, actualHash);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying model: {Error}", ex.Message);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DownloadModel(string url, string destinationPath)
    {
        _logger.LogInformation("Downloading from {Url} to {Destination}", url, destinationPath);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10); // Large model files

        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var lastLogTime = DateTime.UtcNow;

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                // Log progress every 5 seconds
                if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 5 && totalBytes > 0)
                {
                    var progressPercent = (double)downloadedBytes / totalBytes * 100;
                    _logger.LogInformation("Download progress: {Progress:F1}% ({Downloaded} / {Total} MB)",
                        progressPercent,
                        downloadedBytes / 1024.0 / 1024.0,
                        totalBytes / 1024.0 / 1024.0);
                    lastLogTime = DateTime.UtcNow;
                }
            }

            _logger.LogInformation("Download complete: {Destination} ({Size} MB)",
                destinationPath,
                downloadedBytes / 1024.0 / 1024.0);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading model: {Error}", ex.Message);

            // Clean up partial download
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error writing model file: {Error}", ex.Message);
            throw;
        }
    }

    private string GetModelPath() => Path.Combine(_modelCacheDirectory, "model.onnx");
    private string GetTokenizerPath() => Path.Combine(_modelCacheDirectory, "vocab.txt");
}
