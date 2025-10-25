using System.Diagnostics;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Implementation of TextRank-based plot-relevant sentence extraction.
/// Uses PageRank algorithm to identify the most important sentences in subtitle text.
/// </summary>
public class TextRankService : ITextRankService
{
    private readonly ILogger<TextRankService> _logger;
    private readonly SentenceSegmenter _segmenter;
    private const double DefaultDampingFactor = 0.85;
    private const double DefaultConvergenceThreshold = 0.0001;
    private const int DefaultMaxIterations = 100;
    private const double DefaultSimilarityThreshold = 0.1;

    public TextRankService(ILogger<TextRankService> logger)
    {
        _logger = logger;
        _segmenter = new SentenceSegmenter();
    }

    /// <inheritdoc/>
    public TextRankExtractionResult ExtractPlotRelevantSentences(
        string subtitleText,
        int sentencePercentage,
        int minSentences,
        int minPercentage)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Handle empty input
            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                return CreateFallbackResult(
                    subtitleText ?? string.Empty,
                    0,
                    0,
                    0,
                    stopwatch.ElapsedMilliseconds,
                    "Empty or whitespace input");
            }

            // Segment into sentences
            var sentences = _segmenter.SegmentIntoSentences(subtitleText);

            // Fallback: Single sentence or insufficient sentences
            if (sentences.Length < minSentences)
            {
                return CreateFallbackResult(
                    subtitleText,
                    sentences.Length,
                    sentences.Length,
                    0.0,
                    stopwatch.ElapsedMilliseconds,
                    $"Below {minSentences} sentence threshold");
            }

            // Calculate TextRank scores
            var scores = CalculateTextRankScores(sentences);

            // Select top sentences by percentage
            int targetCount = Math.Max(1, (int)Math.Ceiling(sentences.Length * sentencePercentage / 100.0));

            // Fallback: Check minimum percentage retention
            double selectionPercentage = (targetCount * 100.0) / sentences.Length;
            if (selectionPercentage < minPercentage)
            {
                return CreateFallbackResult(
                    subtitleText,
                    sentences.Length,
                    sentences.Length,
                    scores.Values.Average(),
                    stopwatch.ElapsedMilliseconds,
                    $"Selection would be below {minPercentage}% minimum");
            }

            // Select top-scoring sentences
            var topIndices = scores
                .OrderByDescending(kvp => kvp.Value)
                .Take(targetCount)
                .Select(kvp => kvp.Key)
                .OrderBy(idx => idx) // Maintain chronological order
                .ToList();

            // Build filtered text maintaining original order
            var filteredSentences = topIndices.Select(idx => sentences[idx]);
            var filteredText = string.Join(" ", filteredSentences);

            // Calculate statistics
            var selectedScores = topIndices.Select(idx => scores[idx]).ToList();
            double avgScore = selectedScores.Any() ? selectedScores.Average() : 0.0;

            stopwatch.Stop();

            return new TextRankExtractionResult
            {
                FilteredText = filteredText,
                TotalSentenceCount = sentences.Length,
                SelectedSentenceCount = targetCount,
                AverageScore = avgScore,
                SelectionPercentage = selectionPercentage,
                FallbackTriggered = false,
                FallbackReason = null,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting plot-relevant sentences");
            stopwatch.Stop();
            return CreateFallbackResult(
                subtitleText,
                0,
                0,
                0.0,
                stopwatch.ElapsedMilliseconds,
                $"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Dictionary<int, double> CalculateTextRankScores(string[] sentences)
    {
        if (sentences == null || sentences.Length == 0)
        {
            return new Dictionary<int, double>();
        }

        if (sentences.Length == 1)
        {
            return new Dictionary<int, double> { { 0, 1.0 } };
        }

        // Build similarity matrix
        var similarityMatrix = BuildSimilarityMatrix(sentences);

        // Apply PageRank algorithm
        var scores = ApplyPageRank(similarityMatrix);

        // Normalize scores to 0-1 range
        var minScore = scores.Values.Min();
        var maxScore = scores.Values.Max();
        var range = maxScore - minScore;

        if (range > 0)
        {
            var normalizedScores = new Dictionary<int, double>();
            foreach (var kvp in scores)
            {
                normalizedScores[kvp.Key] = (kvp.Value - minScore) / range;
            }
            return normalizedScores;
        }

        return scores;
    }

    /// <summary>
    /// Builds a similarity matrix between sentences using bag-of-words cosine similarity.
    /// </summary>
    private Dictionary<int, Dictionary<int, double>> BuildSimilarityMatrix(string[] sentences)
    {
        var matrix = new Dictionary<int, Dictionary<int, double>>();

        // Precompute word vectors for all sentences
        var wordVectors = sentences.Select(CreateWordVector).ToArray();

        for (int i = 0; i < sentences.Length; i++)
        {
            matrix[i] = new Dictionary<int, double>();

            for (int j = 0; j < sentences.Length; j++)
            {
                if (i == j)
                {
                    matrix[i][j] = 0.0; // No self-loops
                    continue;
                }

                var similarity = CalculateCosineSimilarity(wordVectors[i], wordVectors[j]);

                // Only add edges above similarity threshold
                if (similarity >= DefaultSimilarityThreshold)
                {
                    matrix[i][j] = similarity;
                }
                else
                {
                    matrix[i][j] = 0.0;
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// Creates a word frequency vector for a sentence (bag-of-words representation).
    /// </summary>
    private Dictionary<string, int> CreateWordVector(string sentence)
    {
        var words = sentence
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '-', '"', '\'', '(', ')', '[', ']' },
                  StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3) // Filter very short words
            .ToArray();

        var vector = new Dictionary<string, int>();
        foreach (var word in words)
        {
            if (vector.ContainsKey(word))
            {
                vector[word]++;
            }
            else
            {
                vector[word] = 1;
            }
        }

        return vector;
    }

    /// <summary>
    /// Calculates cosine similarity between two word vectors.
    /// </summary>
    private double CalculateCosineSimilarity(Dictionary<string, int> vector1, Dictionary<string, int> vector2)
    {
        if (vector1.Count == 0 || vector2.Count == 0)
        {
            return 0.0;
        }

        // Calculate dot product
        double dotProduct = 0.0;
        foreach (var word in vector1.Keys.Intersect(vector2.Keys))
        {
            dotProduct += vector1[word] * vector2[word];
        }

        // Calculate magnitudes
        double magnitude1 = Math.Sqrt(vector1.Values.Sum(v => v * v));
        double magnitude2 = Math.Sqrt(vector2.Values.Sum(v => v * v));

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0.0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Applies PageRank algorithm to the similarity matrix.
    /// </summary>
    private Dictionary<int, double> ApplyPageRank(Dictionary<int, Dictionary<int, double>> similarityMatrix)
    {
        int nodeCount = similarityMatrix.Count;
        var scores = new Dictionary<int, double>();
        var newScores = new Dictionary<int, double>();

        // Initialize scores uniformly
        double initialScore = 1.0 / nodeCount;
        for (int i = 0; i < nodeCount; i++)
        {
            scores[i] = initialScore;
            newScores[i] = 0.0;
        }

        // Normalize edges (calculate out-degree weights)
        var normalizedMatrix = NormalizeMatrix(similarityMatrix);

        // Iterate PageRank
        for (int iteration = 0; iteration < DefaultMaxIterations; iteration++)
        {
            // Calculate new scores
            for (int i = 0; i < nodeCount; i++)
            {
                double incomingScore = 0.0;

                // Sum contributions from incoming edges
                for (int j = 0; j < nodeCount; j++)
                {
                    if (normalizedMatrix[j].ContainsKey(i) && normalizedMatrix[j][i] > 0)
                    {
                        incomingScore += scores[j] * normalizedMatrix[j][i];
                    }
                }

                // Apply damping factor
                newScores[i] = (1 - DefaultDampingFactor) / nodeCount + DefaultDampingFactor * incomingScore;
            }

            // Check convergence
            double maxDiff = 0.0;
            for (int i = 0; i < nodeCount; i++)
            {
                double diff = Math.Abs(newScores[i] - scores[i]);
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                }
            }

            // Copy new scores to current
            for (int i = 0; i < nodeCount; i++)
            {
                scores[i] = newScores[i];
            }

            // Check if converged
            if (maxDiff < DefaultConvergenceThreshold)
            {
                _logger.LogDebug("PageRank converged after {Iterations} iterations", iteration + 1);
                break;
            }
        }

        return scores;
    }

    /// <summary>
    /// Normalizes the similarity matrix by dividing each edge by the sum of outgoing edges.
    /// </summary>
    private Dictionary<int, Dictionary<int, double>> NormalizeMatrix(Dictionary<int, Dictionary<int, double>> matrix)
    {
        var normalized = new Dictionary<int, Dictionary<int, double>>();

        foreach (var node in matrix.Keys)
        {
            normalized[node] = new Dictionary<int, double>();
            double outDegree = matrix[node].Values.Sum();

            if (outDegree > 0)
            {
                foreach (var edge in matrix[node])
                {
                    normalized[node][edge.Key] = edge.Value / outDegree;
                }
            }
        }

        return normalized;
    }

    /// <summary>
    /// Creates a fallback result that returns the full text without filtering.
    /// </summary>
    private TextRankExtractionResult CreateFallbackResult(
        string originalText,
        int totalSentences,
        int selectedSentences,
        double avgScore,
        long processingTime,
        string reason)
    {
        return new TextRankExtractionResult
        {
            FilteredText = originalText.Trim(),
            TotalSentenceCount = totalSentences,
            SelectedSentenceCount = selectedSentences,
            AverageScore = avgScore,
            SelectionPercentage = totalSentences > 0 ? 100.0 : 0.0,
            FallbackTriggered = true,
            FallbackReason = reason,
            ProcessingTimeMs = processingTime
        };
    }
}
