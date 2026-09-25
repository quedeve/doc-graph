using DocumentGraph.Core.Interfaces;
using Moq;
using Xunit;

namespace DocumentGraph.Tests;

public class SearchAndReaderTests
{
    [Fact]
    public void SearchQuery_DefaultValuesAreCorrect()
    {
        var query = new SearchQuery();

        Assert.Equal(string.Empty, query.QueryText);
        Assert.Equal(10, query.Limit);
        Assert.Null(query.DocumentType);
        Assert.Null(query.Extension);
        Assert.Null(query.PathPrefix);
    }

    [Fact]
    public void SearchQuery_CustomPropertiesRetainValues()
    {
        var query = new SearchQuery
        {
            QueryText = "authentication oauth2 flow",
            Limit = 25,
            DocumentType = "code",
            Extension = ".cs",
            PathPrefix = "C:/Projects/Auth"
        };

        Assert.Equal("authentication oauth2 flow", query.QueryText);
        Assert.Equal(25, query.Limit);
        Assert.Equal("code", query.DocumentType);
        Assert.Equal(".cs", query.Extension);
        Assert.Equal("C:/Projects/Auth", query.PathPrefix);
    }

    [Fact]
    public void SearchResults_TracksItemsAndDuration()
    {
        var results = new SearchResults
        {
            TotalCount = 2,
            Duration = TimeSpan.FromMilliseconds(45.5),
            Items =
            [
                new SearchResultItem
                {
                    DocumentId = 1,
                    ChunkId = 10,
                    Filename = "Security.cs",
                    Path = "/src/Security.cs",
                    DocumentType = "code",
                    Location = "lines 10-25",
                    SectionTitle = "Token Validation",
                    Preview = "public bool ValidateToken(string token)",
                    Score = 0.92f,
                    ScoreComponents = new Dictionary<string, float>
                    {
                        ["fts"] = 0.88f,
                        ["semantic"] = 0.95f
                    }
                },
                new SearchResultItem
                {
                    DocumentId = 2,
                    ChunkId = 15,
                    Filename = "Architecture.md",
                    Path = "/docs/Architecture.md",
                    DocumentType = "document",
                    Location = "Section 2",
                    Preview = "All API calls require bearer token",
                    Score = 0.81f
                }
            ]
        };

        Assert.Equal(2, results.TotalCount);
        Assert.Equal(TimeSpan.FromMilliseconds(45.5), results.Duration);
        Assert.Equal(2, results.Items.Count);
        Assert.Equal("Security.cs", results.Items[0].Filename);
        Assert.Equal(0.92f, results.Items[0].Score);
        Assert.Equal(0.88f, results.Items[0].ScoreComponents["fts"]);
        Assert.Equal(0.95f, results.Items[0].ScoreComponents["semantic"]);
    }

    [Fact]
    public void ReadContentRequestAndResult_PopulateAccurately()
    {
        var request = new ReadContentRequest
        {
            PathOrFilename = "Database.sql",
            Section = "Schema",
            Symbol = "CreateUsersTable",
            LineStart = 50,
            LineEnd = 100
        };

        Assert.Equal("Database.sql", request.PathOrFilename);
        Assert.Equal("Schema", request.Section);
        Assert.Equal("CreateUsersTable", request.Symbol);
        Assert.Equal(50, request.LineStart);
        Assert.Equal(100, request.LineEnd);

        var result = new ReadContentResult
        {
            Path = "C:/SQL/Database.sql",
            Filename = "Database.sql",
            DocumentType = "sql",
            Title = "Database Schema",
            TargetDescription = "Symbol CreateUsersTable (lines 50-100)",
            Content = "CREATE TABLE users (id INT PRIMARY KEY, name TEXT);",
            TotalChunks = 1,
            EstimatedTokens = 12,
            Metadata = new Dictionary<string, object> { ["tables_count"] = 5 }
        };

        Assert.Equal("C:/SQL/Database.sql", result.Path);
        Assert.Equal("Database.sql", result.Filename);
        Assert.Equal("sql", result.DocumentType);
        Assert.Equal(12, result.EstimatedTokens);
        Assert.Equal(5, result.Metadata["tables_count"]);
    }

    [Fact]
    public void DocumentOutline_CollectsSectionsAndSymbols()
    {
        var outline = new DocumentOutline
        {
            Path = "/src/Controller.cs",
            Filename = "Controller.cs",
            DocumentType = "code",
            Title = "Controller",
            Sections =
            [
                new OutlineItem
                {
                    Title = "Class Definition",
                    Depth = 1
                }
            ],
            Symbols =
            [
                new OutlineSymbol
                {
                    Name = "GetItems",
                    SymbolType = "Method",
                    Signature = "public IActionResult GetItems()",
                    LineStart = 15,
                    LineEnd = 30
                }
            ]
        };

        Assert.Single(outline.Sections);
        Assert.Equal("Class Definition", outline.Sections[0].Title);
        Assert.Single(outline.Symbols);
        Assert.Equal("GetItems", outline.Symbols[0].Name);
        Assert.Equal("Method", outline.Symbols[0].SymbolType);
        Assert.Equal(15, outline.Symbols[0].LineStart);
        Assert.Equal(30, outline.Symbols[0].LineEnd);
    }

    [Fact]
    public async Task SearchService_MockReturnsExpectedResults()
    {
        var mockSearch = new Mock<ISearchService>();
        var sampleResults = new SearchResults
        {
            TotalCount = 1,
            Items = [new SearchResultItem { Filename = "Test.cs", Score = 0.99f }]
        };

        mockSearch.Setup(s => s.HybridSearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleResults);

        var query = new SearchQuery { QueryText = "unit test" };
        var actual = await mockSearch.Object.HybridSearchAsync(query);

        Assert.Single(actual.Items);
        Assert.Equal("Test.cs", actual.Items[0].Filename);
        Assert.Equal(0.99f, actual.Items[0].Score);
    }

    [Fact]
    public async Task DocumentReader_MockReturnsExpectedOutlineAndContent()
    {
        var mockReader = new Mock<IDocumentReader>();

        mockReader.Setup(r => r.GetOutlineAsync("Doc.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentOutline
            {
                Filename = "Doc.md",
                Title = "Documentation",
                Sections = [new OutlineItem { Title = "Introduction", Depth = 1 }]
            });

        mockReader.Setup(r => r.ReadAsync(It.Is<ReadContentRequest>(req => req.PathOrFilename == "Doc.md"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadContentResult
            {
                Filename = "Doc.md",
                Content = "# Introduction\nWelcome to DocumentGraph",
                TotalChunks = 1
            });

        var outline = await mockReader.Object.GetOutlineAsync("Doc.md");
        Assert.NotNull(outline);
        Assert.Equal("Documentation", outline.Title);
        Assert.Single(outline.Sections);

        var content = await mockReader.Object.ReadAsync(new ReadContentRequest { PathOrFilename = "Doc.md" });
        Assert.NotNull(content);
        Assert.Contains("Welcome to DocumentGraph", content.Content);
    }
}
