using DocumentGraph.Api;
using Xunit;

namespace DocumentGraph.Tests;

public class ApiTests
{
    [Fact]
    public void SearchRequest_DefaultModeIsHybrid()
    {
        var req = new SearchRequest
        {
            Query = "authentication"
        };

        Assert.Equal("hybrid", req.Mode);
        Assert.Equal(10, req.Limit);
        Assert.Equal("authentication", req.Query);
    }

    [Fact]
    public void FindSymbolRequest_SetsDefaultsCorrectly()
    {
        var req = new FindSymbolRequest
        {
            Name = "MfaService"
        };

        Assert.Equal("MfaService", req.Name);
        Assert.Equal(20, req.Limit);
        Assert.Null(req.SymbolType);
    }

    [Fact]
    public void RelatedRequest_AssignsTargetProperly()
    {
        var req = new RelatedRequest
        {
            Target = "Architecture.pdf"
        };

        Assert.Equal("Architecture.pdf", req.Target);
    }

    [Fact]
    public void ReferencesRequest_AssignsSymbolProperly()
    {
        var req = new ReferencesRequest
        {
            Symbol = "GenerateTokenAsync"
        };

        Assert.Equal("GenerateTokenAsync", req.Symbol);
    }

    [Fact]
    public void IndexRequest_AssignsDefaultPathProperly()
    {
        var req = new IndexRequest
        {
            Path = "C:/Knowledge"
        };

        Assert.Equal("C:/Knowledge", req.Path);
    }

    [Fact]
    public void ApiRequests_SerializeAndDeserializeCleanly()
    {
        var req = new SearchRequest
        {
            Query = "MFA Challenge",
            Mode = "semantic",
            Limit = 15,
            DocumentType = "document",
            Extension = ".pdf",
            PathPrefix = "specs/"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(req);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SearchRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("MFA Challenge", deserialized.Query);
        Assert.Equal("semantic", deserialized.Mode);
        Assert.Equal(15, deserialized.Limit);
        Assert.Equal("document", deserialized.DocumentType);
        Assert.Equal(".pdf", deserialized.Extension);
        Assert.Equal("specs/", deserialized.PathPrefix);
    }
}
