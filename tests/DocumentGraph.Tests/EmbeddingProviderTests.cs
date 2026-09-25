using System.Net;
using System.Text.Json;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Search.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentGraph.Tests;

public class EmbeddingProviderTests
{
    private class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task OllamaEmbeddingProvider_EmbedsText_AndExtractsVector()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal("/api/embeddings", req.RequestUri?.AbsolutePath);
            var responseJson = JsonSerializer.Serialize(new
            {
                embedding = new float[] { 0.1f, 0.2f, 0.3f }
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new HttpClient(handler);
        var config = new EmbeddingConfig
        {
            Provider = "ollama",
            Model = "nomic-embed-text",
            Endpoint = "http://localhost:11434",
            Dimension = 3
        };

        var provider = new OllamaEmbeddingProvider(client, config, NullLogger<OllamaEmbeddingProvider>.Instance);

        var vector = await provider.EmbedAsync("test prompt");

        Assert.Equal(3, vector.Length);
        Assert.Equal(0.1f, vector[0]);
        Assert.Equal(0.2f, vector[1]);
        Assert.Equal(0.3f, vector[2]);
        Assert.Equal("ollama", provider.ProviderName);
        Assert.Equal(3, provider.Dimension);
    }

    [Fact]
    public async Task OpenAiCompatibleEmbeddingProvider_EmbedsBatch_AndSendsAuthHeader()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal("/v1/embeddings", req.RequestUri?.AbsolutePath);
            Assert.NotNull(req.Headers.Authorization);
            Assert.Equal("Bearer", req.Headers.Authorization.Scheme);
            Assert.Equal("sk-test-token", req.Headers.Authorization.Parameter);

            var responseJson = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new { embedding = new float[] { 1.0f, 0.0f }, index = 0 },
                    new { embedding = new float[] { 0.0f, 1.0f }, index = 1 }
                }
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new HttpClient(handler);
        var config = new EmbeddingConfig
        {
            Provider = "openai",
            Model = "text-embedding-3-small",
            Endpoint = "http://localhost:8000/v1",
            ApiKey = "sk-test-token",
            Dimension = 2
        };

        var provider = new OpenAiCompatibleEmbeddingProvider(client, config, NullLogger<OpenAiCompatibleEmbeddingProvider>.Instance);

        var vectors = await provider.EmbedBatchAsync(["text one", "text two"]);

        Assert.Equal(2, vectors.Length);
        Assert.Equal(1.0f, vectors[0][0]);
        Assert.Equal(1.0f, vectors[1][1]);
        Assert.Equal("openai", provider.ProviderName);
    }
}
