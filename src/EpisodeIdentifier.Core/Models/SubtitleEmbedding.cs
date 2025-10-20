namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents a 384-dimensional semantic embedding vector for subtitle content.
/// Embeddings enable semantic similarity matching that is robust to OCR errors,
/// unlike traditional fuzzy hashing approaches.
/// </summary>
public record SubtitleEmbedding
{
    /// <summary>
    /// 384-dimensional embedding vector (float32 array).
    /// Generated from cleaned subtitle text using all-MiniLM-L6-v2 model.
    /// </summary>
    public float[] Vector { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0-1.0) when comparing to another embedding.
    /// Higher values indicate more similar content. Null if not computed.
    /// </summary>
    public double? Similarity { get; init; }

    /// <summary>
    /// Source text that was embedded (typically CleanText).
    /// </summary>
    public string SourceText { get; init; }

    public SubtitleEmbedding(float[] vector, string sourceText)
    {
        if (vector == null || vector.Length != 384)
        {
            throw new ArgumentException("Embedding vector must be exactly 384 dimensions", nameof(vector));
        }

        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        Similarity = null;
    }

    /// <summary>
    /// Serialize embedding to byte array for SQLite BLOB storage.
    /// Converts 384 float32 values to 1536 bytes (384 × 4).
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[Vector.Length * sizeof(float)];
        Buffer.BlockCopy(Vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Deserialize embedding from SQLite BLOB byte array.
    /// Expects exactly 1536 bytes (384 float32 values).
    /// </summary>
    public static float[] FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 384 * sizeof(float))
        {
            throw new ArgumentException("Byte array must be exactly 1536 bytes (384 floats)", nameof(bytes));
        }

        var vector = new float[384];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    /// <summary>
    /// Calculate cosine similarity between two embeddings.
    /// Returns value in range [0.0, 1.0] where 1.0 = identical, 0.0 = completely different.
    /// Formula: cosine_sim(a, b) = dot(a, b) / (||a|| × ||b||)
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length != 384)
        {
            throw new ArgumentException("Embeddings must be 384 dimensions");
        }

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < 384; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
