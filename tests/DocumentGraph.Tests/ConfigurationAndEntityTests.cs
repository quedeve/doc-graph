using System.Text.Json;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using Xunit;

namespace DocumentGraph.Tests;

public class ConfigurationAndEntityTests
{
    [Fact]
    public void DocumentGraphConfig_HasSensibleDefaults()
    {
        var config = new DocumentGraphConfig();

        Assert.NotNull(config.Database);
        Assert.NotNull(config.Search);
        Assert.NotNull(config.Embedding);
        Assert.NotNull(config.Indexing);
        Assert.NotNull(config.Security);

        Assert.Equal(10, config.Search.DefaultLimit);
        Assert.Equal(0.4f, config.Search.FtsWeight);
        Assert.Equal(0.6f, config.Search.SemanticWeight);
        Assert.Equal(50 * 1024 * 1024, config.Indexing.MaxFileSizeBytes);
        Assert.Equal("none", config.Embedding.Provider);
        Assert.True(config.Security.EnforceAllowedRoots);
    }

    [Fact]
    public void DocumentGraphConfig_SerializesAndDeserializesAccurately()
    {
        var config = new DocumentGraphConfig();
        config.Database.ConnectionString = "Host=myhost;Port=5433;Database=testdb;";
        config.Search.FtsWeight = 1.5f;
        config.Security.AllowedRoots = [@"C:\AllowedPath"];

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(config, options);
        var deserialized = JsonSerializer.Deserialize<DocumentGraphConfig>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal("Host=myhost;Port=5433;Database=testdb;", deserialized.Database.ConnectionString);
        Assert.Equal(1.5f, deserialized.Search.FtsWeight);
        Assert.Single(deserialized.Security.AllowedRoots);
        Assert.Equal(@"C:\AllowedPath", deserialized.Security.AllowedRoots[0]);
    }

    [Fact]
    public void EntityModels_MapPropertiesAndRelationshipsCorrectly()
    {
        var doc = new DocumentEntity
        {
            Id = 1,
            Path = "C:/Docs/Architecture.pdf",
            Filename = "Architecture.pdf",
            DocumentType = "pdf",
            Extension = ".pdf",
            Size = 2048,
            Hash = "abc123hash",
            IndexedAt = DateTime.UtcNow
        };

        var section = new SectionEntity
        {
            Id = 10,
            DocumentId = doc.Id,
            Title = "Authentication Section",
            SectionIndex = 1,
            PageNumber = 14,
            Document = doc
        };

        var chunk = new ChunkEntity
        {
            Id = 100,
            DocumentId = doc.Id,
            SectionId = section.Id,
            ChunkIndex = 1,
            Content = "Authentication token validation details",
            Document = doc,
            Section = section
        };

        var symbol = new SymbolEntity
        {
            Id = 500,
            DocumentId = doc.Id,
            Name = "ValidateTokenAsync",
            SymbolType = "Method",
            LineStart = 45,
            LineEnd = 60,
            Document = doc
        };

        var rel = new RelationshipEntity
        {
            Id = 1000,
            SourceId = chunk.Id,
            SourceType = "Chunk",
            TargetId = symbol.Id,
            TargetType = "Symbol",
            RelationshipType = "REFERENCES",
            Confidence = 0.9f
        };

        doc.Sections.Add(section);
        doc.Chunks.Add(chunk);
        doc.Symbols.Add(symbol);

        Assert.Single(doc.Sections);
        Assert.Single(doc.Chunks);
        Assert.Single(doc.Symbols);
        Assert.Equal(doc.Id, section.DocumentId);
        Assert.Equal(section.Id, chunk.SectionId);
        Assert.Equal("REFERENCES", rel.RelationshipType);
        Assert.Equal(0.9f, rel.Confidence);
    }

    [Fact]
    public void Models_SearchAndReadRequestsInitializeCleanly()
    {
        var query = new SearchQuery
        {
            QueryText = "MFA Authentication",
            Limit = 25,
            DocumentType = "code",
            Extension = ".cs",
            PathPrefix = "src/"
        };

        Assert.Equal("MFA Authentication", query.QueryText);
        Assert.Equal(25, query.Limit);
        Assert.Equal("code", query.DocumentType);

        var readReq = new ReadContentRequest
        {
            PathOrFilename = "Architecture.pdf",
            Page = 5,
            Symbol = "TokenHandler",
            LineStart = 10,
            LineEnd = 25
        };

        Assert.Equal("Architecture.pdf", readReq.PathOrFilename);
        Assert.Equal(5, readReq.Page);
        Assert.Equal("TokenHandler", readReq.Symbol);
        Assert.Equal(10, readReq.LineStart);
        Assert.Equal(25, readReq.LineEnd);
    }
}
