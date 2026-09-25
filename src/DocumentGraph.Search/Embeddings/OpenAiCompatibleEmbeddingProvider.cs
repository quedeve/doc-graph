using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Search.Embeddings;

/// <summary>
/// OpenAI-compatible embedding provider (OpenAI, Azure, vLLM, LMStudio, LocalAI).
/// </summary>
public class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingConfig _config;
    private readonly ILogger<OpenAiCompatibleEmbeddingProvider> _logger;

    public string ProviderName => "openai";
    public int Dimension => _config.Dimension;

    public OpenAiCompatibleEmbeddingProvider(
        HttpClient httpClient,
        EmbeddingConfig config,
        ILogger<OpenAiCompatibleEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_config.Endpoint))
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
        }

        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var batch = await EmbedBatchAsync([text], cancellationToken);
        return batch[0];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var request = new OpenAiEmbeddingRequest
        {
            Model = _config.Model,
            Input = texts
        };

        var response = await _httpClient.PostAsJsonAsync("v1/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken: cancellationToken);
        if (body?.Data == null || body.Data.Count == 0)
        {
            throw new InvalidOperationException("OpenAI endpoint returned empty embeddings.");
        }

        return body.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
    }

    private class OpenAiEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public IReadOnlyList<string> Input { get; set; } = [];
    }

    private class OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiEmbeddingItem> Data { get; set; } = [];
    }

    private class OpenAiEmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
