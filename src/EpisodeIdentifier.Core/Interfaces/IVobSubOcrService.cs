using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Performs OCR on VobSub subtitle files using Tesseract.
/// </summary>
public interface IVobSubOcrService
{
    /// <summary>
    /// Extracts text from VobSub files using Tesseract OCR.
    /// </summary>
    /// <param name="idxFilePath">Path to VobSub .idx file. Must exist and be a .idx file.</param>
    /// <param name="subFilePath">Path to VobSub .sub file. Must exist and be a .sub file.</param>
    /// <param name="language">OCR language code (e.g., 'eng', 'spa'). Must be a valid Tesseract language code.</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling.</param>
    /// <returns>Result containing extracted text and confidence metrics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when idxFilePath, subFilePath, or language is null.</exception>
    /// <exception cref="ArgumentException">Thrown when files don't exist or language is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when OCR processing exceeds timeout.</exception>
    Task<VobSubOcrResult> PerformOcrAsync(string idxFilePath, string subFilePath, string language, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if Tesseract OCR is available on system PATH.
    /// </summary>
    /// <returns>True if Tesseract is available, false otherwise.</returns>
    Task<bool> IsTesseractAvailableAsync();

    /// <summary>
    /// Converts user language to Tesseract language code.
    /// </summary>
    /// <param name="language">User-specified language (e.g., 'eng', 'english').</param>
    /// <returns>Tesseract language code (e.g., 'eng').</returns>
    string GetOcrLanguageCode(string language);
}
