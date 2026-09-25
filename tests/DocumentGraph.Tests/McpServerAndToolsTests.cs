using System.Text.Json;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using DocumentGraph.Core.Observability;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Mcp.Server;
using DocumentGraph.Mcp.Tools;
using DocumentGraph.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGraph.Tests;

public class McpServerAndToolsTests
{
    [Fact]
    public async Task OpenTool_Fails_WhenNeitherIdNorFileProvided()
    {
        var mockReader = new Mock<IDocumentReader>();
        var tool = new OpenTool(mockReader.Object, null!);

        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("Either 'id' or 'file' must be specified", result.Content[0].Text);
    }

    [Fact]
    public async Task OpenTool_RejectsRestrictedExtensions_ViaPathGuard()
    {
        var mockReader = new Mock<IDocumentReader>();
        var tool = new OpenTool(mockReader.Object, null!);

        var args = JsonDocument.Parse(@"{""file"": ""secrets.env""}").RootElement;
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.IsError);
        Assert.Contains("Security Error", result.Content[0].Text);
    }

    [Fact]
    public async Task OpenTool_InvokesReaderAndReturnsFormattedContent()
    {
        var mockReader = new Mock<IDocumentReader>();
        mockReader.Setup(r => r.ReadAsync(It.IsAny<ReadContentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadContentResult
            {
                Filename = "Architecture.pdf",
                TargetDescription = "page 1",
                Content = "System Architecture Overview",
                EstimatedTokens = 10,
                TotalChunks = 1
            });

        var tool = new OpenTool(mockReader.Object, null!);
        var args = JsonDocument.Parse(@"{""file"": ""Architecture.pdf"", ""page"": 1}").RootElement;

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.IsError);
        Assert.Contains("Architecture.pdf", result.Content[0].Text);
        Assert.Contains("System Architecture Overview", result.Content[0].Text);
    }

    [Fact]
    public async Task OutlineTool_Fails_WhenNoFileOrIdSpecified()
    {
        var mockReader = new Mock<IDocumentReader>();
        var tool = new OutlineTool(mockReader.Object, null!);

        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("Either 'file' or 'id' must be specified", result.Content[0].Text);
    }

    [Fact]
    public async Task OutlineTool_FormatsSectionsAndSymbolsCorrectly()
    {
        var mockReader = new Mock<IDocumentReader>();
        mockReader.Setup(r => r.GetOutlineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentOutline
            {
                Filename = "Service.cs",
                Path = "src/Service.cs",
                Title = "Service",
                DocumentType = "code",
                Sections = [new OutlineItem { Title = "Overview", Depth = 1 }],
                Symbols = [new OutlineSymbol { Name = "AuthService", SymbolType = "Class", LineStart = 1, LineEnd = 20 }]
            });

        var tool = new OutlineTool(mockReader.Object, null!);
        var args = JsonDocument.Parse(@"{""file"": ""Service.cs""}").RootElement;

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.IsError);
        Assert.Contains("# Outline: Service", result.Content[0].Text);
        Assert.Contains("- Overview", result.Content[0].Text);
        Assert.Contains("[Class] AuthService", result.Content[0].Text);
    }

    [Fact]
    public async Task ContextTool_ValidatesRequiredQuery()
    {
        var mockSearch = new Mock<ISearchService>();
        var mockRel = new Mock<IRelationshipService>();
        var tool = new ContextTool(mockSearch.Object, null!, mockRel.Object);

        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("'query' parameter is required", result.Content[0].Text);
    }

    [Fact]
    public async Task McpServer_HandlesInitializeAndToolsList()
    {
        var mockSearchTool = new Mock<IMcpTool>();
        mockSearchTool.Setup(t => t.Name).Returns("search");
        mockSearchTool.Setup(t => t.Description).Returns("Search the knowledge base");
        mockSearchTool.Setup(t => t.InputSchema).Returns(new { type = "object" });

        var obs = new ObservabilityService();
        var server = new McpServer([mockSearchTool.Object], obs);

        // Test initialize request
        var initJson = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""initialize"",""params"":{}}";
        var initResponse = await server.ProcessRequestAsync(initJson);

        Assert.NotNull(initResponse);
        var initSerialized = JsonSerializer.Serialize(initResponse);
        Assert.Contains("DocumentGraph.Mcp", initSerialized);

        // Test tools/list request
        var listJson = @"{""jsonrpc"":""2.0"",""id"":2,""method"":""tools/list"",""params"":{}}";
        var listResponse = await server.ProcessRequestAsync(listJson);

        Assert.NotNull(listResponse);
        var listSerialized = JsonSerializer.Serialize(listResponse);
        Assert.Contains("search", listSerialized);
        Assert.Contains("Search the knowledge base", listSerialized);

        // Test unknown method request
        var unknownJson = @"{""jsonrpc"":""2.0"",""id"":3,""method"":""unknown/method"",""params"":{}}";
        var unknownResponse = await server.ProcessRequestAsync(unknownJson);

        Assert.NotNull(unknownResponse);
        var unknownSerialized = JsonSerializer.Serialize(unknownResponse);
        Assert.Contains("-32601", unknownSerialized);
        Assert.Contains("not recognized", unknownSerialized);
    }

    [Fact]
    public async Task McpServer_ExecutesToolCall_AndTracksObservability()
    {
        var mockTool = new Mock<IMcpTool>();
        mockTool.Setup(t => t.Name).Returns("custom");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpToolCallResult.Text("Execution succeeded"));

        var obs = new ObservabilityService();
        var server = new McpServer([mockTool.Object], obs);

        var callJson = @"{""jsonrpc"":""2.0"",""id"":10,""method"":""tools/call"",""params"":{""name"":""custom"",""arguments"":{}}}";
        var callResponse = await server.ProcessRequestAsync(callJson);

        Assert.NotNull(callResponse);
        var callSerialized = JsonSerializer.Serialize(callResponse);
        Assert.Contains("Execution succeeded", callSerialized);

        var snapshot = obs.GetSnapshot();
        Assert.True(snapshot.McpTools.ContainsKey("custom"));
        Assert.Equal(1, snapshot.McpTools["custom"].Invocations);
        Assert.Equal(0, snapshot.McpTools["custom"].Failures);
    }
}
