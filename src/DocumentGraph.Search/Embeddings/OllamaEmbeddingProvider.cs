using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Search.Embeddings;

/// <summary>
/// Embedding provider using local Ollama instance (e.g., nomic-embed-text, all-minilm).
/// </summary>
public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingConfig _config;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    public string ProviderName => "ollama";
    public int Dimension => _config.Dimension;

    public OllamaEmbeddingProvider(
        HttpClient httpClient,
        EmbeddingConfig config,
        ILogger<OllamaEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_config.Endpoint))
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new OllamaEmbeddingRequest
        {
            Model = _config.Model,
            Prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
        if (body?.Embedding == null || body.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned an empty embedding.");
        }

        return body.Embedding;
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[i] = await EmbedAsync(texts[i], cancellationToken);
        }
        return results;
    }

    private class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
