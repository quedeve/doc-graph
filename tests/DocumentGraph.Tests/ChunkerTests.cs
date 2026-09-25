using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using DocumentGraph.Indexer.Chunking;
using Xunit;

namespace DocumentGraph.Tests;

public class ChunkerTests
{
    [Fact]
    public void DefaultChunker_SplitsLongTextRespectingMaxTokens()
    {
        var chunker = new DefaultChunker();
        var doc = new ParsedDocument
        {
            Title = "LongDoc",
            DocumentType = "document",
            Sections =
            [
                new DocumentSection
                {
                    Title = "LongSection",
                    Content = string.Join(" ", Enumerable.Repeat("The quick brown fox jumps over the lazy dog.", 100))
                }
            ]
        };

        var options = new ChunkingOptions
        {
            MaxTokensPerChunk = 50,
            OverlapTokens = 10
        };

        var chunks = chunker.Chunk(doc, options);

        Assert.True(chunks.Count > 1, "Expected multiple chunks for long content");
        foreach (var chunk in chunks)
        {
            Assert.True(chunk.TokenCount <= 60, $"Chunk token count {chunk.TokenCount} exceeds expected limit");
            Assert.Equal("LongSection", chunk.SectionTitle);
        }
    }

    [Fact]
    public void CodeChunker_ChunksBySymbols()
    {
        var chunker = new CodeChunker();
        var doc = new ParsedDocument
        {
            Title = "Service",
            DocumentType = "code",
            Symbols =
            [
                new CodeSymbol
                {
                    Name = "AuthenticationService",
                    Kind = SymbolKind.Class,
                    LineStart = 1,
                    LineEnd = 20,
                    Content = "class AuthenticationService { }"
                },
                new CodeSymbol
                {
                    Name = "ValidateToken",
                    Kind = SymbolKind.Method,
                    LineStart = 5,
                    LineEnd = 15,
                    Content = "public bool ValidateToken(string token) { return true; }"
                }
            ]
        };

        var chunks = chunker.Chunk(doc);

        Assert.Equal(2, chunks.Count);
        Assert.Contains(chunks, c => c.Content.Contains("AuthenticationService"));
        Assert.Contains(chunks, c => c.Content.Contains("ValidateToken"));
    }
}
