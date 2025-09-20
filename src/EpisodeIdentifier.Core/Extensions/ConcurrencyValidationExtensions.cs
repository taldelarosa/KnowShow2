using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Constants;

namespace EpisodeIdentifier.Core.Extensions;

/// <summary>
/// Extension methods for validating and handling concurrency configuration values.
/// Provides centralized validation logic with consistent error handling and logging.
/// </summary>
public static class ConcurrencyValidationExtensions
{
    /// <summary>
    /// Validates and clamps a MaxConcurrency value to the acceptable range.
    /// Logs warnings for out-of-range values and applies safe fallback behavior.
    /// </summary>
    /// <param name="value">The MaxConcurrency value to validate.</param>
    /// <param name="logger">Optional logger for validation warnings.</param>
    /// <param name="context">Optional context description for logging (e.g., "Configuration Load", "Hot-Reload").</param>
    /// <returns>A valid MaxConcurrency value within the acceptable range.</returns>
    public static int ValidateAndClampConcurrency(this int value, ILogger? logger = null, string context = "")
    {
        if (value >= ConfigurationDefaults.Concurrency.MIN && value <= ConfigurationDefaults.Concurrency.MAX)
        {
            // Value is valid, no changes needed
            return value;
        }

        // Value is invalid, apply clamping and log warning
        var clampedValue = Math.Clamp(value, ConfigurationDefaults.Concurrency.MIN, ConfigurationDefaults.Concurrency.MAX);

        var contextPrefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";

        logger?.LogWarning(
            "{Context}Invalid MaxConcurrency value {InvalidValue} is outside valid range ({Range}), " +
            "clamping to {ClampedValue}",
            contextPrefix, value, ConfigurationDefaults.Concurrency.RANGE_DESCRIPTION, clampedValue);

        return clampedValue;
    }

    /// <summary>
    /// Validates MaxConcurrency value and returns detailed validation result.
    /// Useful for scenarios where you need to know if validation failed without applying automatic fixes.
    /// </summary>
    /// <param name="value">The MaxConcurrency value to validate.</param>
    /// <returns>Validation result with success status and error message if applicable.</returns>
    public static ConcurrencyValidationResult ValidateConcurrency(this int value)
    {
        if (value >= ConfigurationDefaults.Concurrency.MIN && value <= ConfigurationDefaults.Concurrency.MAX)
        {
            return ConcurrencyValidationResult.Success(value);
        }

        return ConcurrencyValidationResult.Failure(value,
            $"MaxConcurrency value {value} is outside valid range ({ConfigurationDefaults.Concurrency.RANGE_DESCRIPTION})");
    }

    /// <summary>
    /// Gets the default MaxConcurrency value for backward compatibility scenarios.
    /// </summary>
    /// <returns>The default MaxConcurrency value.</returns>
    public static int GetDefaultConcurrency() => ConfigurationDefaults.Concurrency.DEFAULT;

    /// <summary>
    /// Checks if a MaxConcurrency value is within the valid range without modification.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is valid, false otherwise.</returns>
    public static bool IsValidConcurrency(this int value)
    {
        return value >= ConfigurationDefaults.Concurrency.MIN && value <= ConfigurationDefaults.Concurrency.MAX;
    }
}

/// <summary>
/// Result type for MaxConcurrency validation operations.
/// Provides detailed information about validation success/failure.
/// </summary>
public class ConcurrencyValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful.
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Gets the validated concurrency value.
    /// </summary>
    public int Value { get; private set; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    private ConcurrencyValidationResult(bool isValid, int value, string? errorMessage = null)
    {
        IsValid = isValid;
        Value = value;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="value">The valid concurrency value.</param>
    /// <returns>Successful validation result.</returns>
    public static ConcurrencyValidationResult Success(int value)
    {
        return new ConcurrencyValidationResult(true, value);
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="value">The invalid concurrency value.</param>
    /// <param name="errorMessage">Description of the validation error.</param>
    /// <returns>Failed validation result.</returns>
    public static ConcurrencyValidationResult Failure(int value, string errorMessage)
    {
        return new ConcurrencyValidationResult(false, value, errorMessage);
    }
}
