namespace DocumentGraph.Core.Interfaces;

/// <summary>
/// Generates text embeddings for semantic search.
/// Pluggable to support local models (Ollama), OpenAI-compatible endpoints, or other providers.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// The name of this provider (e.g., "ollama", "openai").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// The dimensionality of the embedding vectors this provider produces.
    /// </summary>
    int Dimension { get; }

    /// <summary>
    /// Generate an embedding vector for a single text input.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embedding vectors for multiple text inputs (batch).
    /// Default implementation calls EmbedAsync sequentially.
    /// </summary>
    async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[i] = await EmbedAsync(texts[i], cancellationToken);
        }
        return results;
    }
}
