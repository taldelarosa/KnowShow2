using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Enhanced PGS to Text converter that uses pgsrip when available,
/// with fallback to the original implementation
/// </summary>
public class EnhancedPgsToTextConverter
{
    private readonly ILogger<EnhancedPgsToTextConverter> _logger;
    private readonly PgsRipService _pgsRipService;
    private readonly PgsToTextConverter _fallbackConverter;

    public EnhancedPgsToTextConverter(
        ILogger<EnhancedPgsToTextConverter> logger,
        PgsRipService pgsRipService,
        PgsToTextConverter fallbackConverter)
    {
        _logger = logger;
        _pgsRipService = pgsRipService;
        _fallbackConverter = fallbackConverter;
    }

    /// <summary>
    /// Convert PGS subtitle data to text using the best available method
    /// </summary>
    public async Task<string> ConvertPgsToText(byte[] pgsData, string language = "eng")
    {
        _logger.LogInformation("Converting PGS subtitle data, size: {Size} bytes, language: {Language}",
            pgsData.Length, language);

        if (pgsData.Length == 0)
        {
            return string.Empty;
        }

        // Try pgsrip first (best quality)
        if (await _pgsRipService.IsAvailableAsync())
        {
            try
            {
                var result = await _pgsRipService.ConvertPgsToSrtAsync(pgsData, language);
                if (!string.IsNullOrEmpty(result))
                {
                    _logger.LogInformation("Successfully converted using pgsrip: {Length} characters", result.Length);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "pgsrip conversion failed, falling back to original method");
            }
        }

        // Fallback to original implementation
        _logger.LogInformation("Using fallback conversion method");
        return await _fallbackConverter.ConvertPgsToText(pgsData, language);
    }

    /// <summary>
    /// Convert PGS subtitles from video file using the best available method
    /// </summary>
    public async Task<string> ConvertPgsFromVideoToText(string videoPath, int subtitleTrackIndex, string language = "eng")
    {
        _logger.LogInformation("Converting PGS from video: {VideoPath}, track {TrackIndex}, language: {Language}",
            videoPath, subtitleTrackIndex, language);

        // Try pgsrip first (best quality)
        if (await _pgsRipService.IsAvailableAsync())
        {
            try
            {
                var result = await _pgsRipService.ConvertVideoSubtitlesAsync(videoPath, language);
                if (!string.IsNullOrEmpty(result))
                {
                    _logger.LogInformation("Successfully converted video using pgsrip: {Length} characters", result.Length);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "pgsrip video conversion failed, falling back to original method");
            }
        }

        // Fallback to original implementation
        _logger.LogInformation("Using fallback video conversion method");
        return await _fallbackConverter.ConvertPgsFromVideoToText(videoPath, subtitleTrackIndex, language);
    }

    /// <summary>
    /// Get conversion quality metrics
    /// </summary>
    public async Task<ConversionQualityInfo> GetQualityInfoAsync()
    {
        var info = new ConversionQualityInfo
        {
            PgsRipAvailable = await _pgsRipService.IsAvailableAsync(),
            FallbackAvailable = _fallbackConverter.IsOcrAvailable()
        };

        if (info.PgsRipAvailable)
        {
            info.PgsRipVersion = await _pgsRipService.GetVersionInfoAsync();
            info.PreferredMethod = "pgsrip";
            info.ExpectedAccuracy = "90-95%";
            info.SupportsPreciseTiming = true;
        }
        else
        {
            info.PreferredMethod = "fallback";
            info.ExpectedAccuracy = "60-75%";
            info.SupportsPreciseTiming = false;
        }

        return info;
    }

    /// <summary>
    /// Attempt to install pgsrip if not available
    /// </summary>
    public async Task<bool> TryInstallPgsRipAsync()
    {
        _logger.LogInformation("Attempting to install pgsrip for better PGS conversion quality");
        return await _pgsRipService.TryInstallAsync();
    }

    /// <summary>
    /// Check if OCR tools are available for subtitle conversion
    /// </summary>
    public bool IsOcrAvailable()
    {
        // Check if either pgsrip or fallback OCR is available
        var qualityInfoTask = GetQualityInfoAsync();
        var qualityInfo = qualityInfoTask.GetAwaiter().GetResult();
        return qualityInfo.PgsRipAvailable || qualityInfo.FallbackAvailable;
    }
}

/// <summary>
/// Information about conversion capabilities and quality
/// </summary>
public class ConversionQualityInfo
{
    public bool PgsRipAvailable { get; set; }
    public bool FallbackAvailable { get; set; }
    public string? PgsRipVersion { get; set; }
    public string PreferredMethod { get; set; } = "unknown";
    public string ExpectedAccuracy { get; set; } = "unknown";
    public bool SupportsPreciseTiming { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("PGS Conversion Quality Info:");
        sb.AppendLine($"  Preferred Method: {PreferredMethod}");
        sb.AppendLine($"  Expected Accuracy: {ExpectedAccuracy}");
        sb.AppendLine($"  Precise Timing: {SupportsPreciseTiming}");
        sb.AppendLine($"  pgsrip Available: {PgsRipAvailable}");
        if (PgsRipAvailable && !string.IsNullOrEmpty(PgsRipVersion))
        {
            sb.AppendLine($"  pgsrip Version: {PgsRipVersion}");
        }
        sb.AppendLine($"  Fallback Available: {FallbackAvailable}");
        return sb.ToString();
    }
}
