using System.Text.Json;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Mcp.Protocol;
using DocumentGraph.Mcp.Tools;
using DocumentGraph.Search.Services;
using Moq;
using Xunit;

namespace DocumentGraph.Tests;

public class McpProtocolTests
{
    [Fact]
    public void JsonRpcResponse_SuccessSerializesCorrectly()
    {
        var id = JsonDocument.Parse("123").RootElement;
        var resp = JsonRpcResponse.Success(id, new { status = "ok" });

        var json = JsonSerializer.Serialize(resp);

        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"result\":{\"status\":\"ok\"}", json);
        Assert.DoesNotContain("\"error\":", json);
    }

    [Fact]
    public void JsonRpcResponse_ErrorSerializesCorrectly()
    {
        var id = JsonDocument.Parse("456").RootElement;
        var resp = JsonRpcResponse.Fail(id, -32601, "Method not found");

        var json = JsonSerializer.Serialize(resp);

        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"id\":456", json);
        Assert.Contains("\"code\":-32601", json);
        Assert.Contains("\"message\":\"Method not found\"", json);
        Assert.DoesNotContain("\"result\":", json);
    }

    [Fact]
    public void McpToolCallResult_TextCreatesProperPayload()
    {
        var result = McpToolCallResult.Text("Sample output text", isError: false);

        Assert.False(result.IsError);
        Assert.Single(result.Content);
        Assert.Equal("text", result.Content[0].Type);
        Assert.Equal("Sample output text", result.Content[0].Text);
    }

    [Fact]
    public async Task SearchTool_ValidatesRequiredQueryParameter()
    {
        var mockSearch = new Mock<ISearchService>();
        var tool = new SearchTool(mockSearch.Object);

        Assert.Equal("search", tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));

        // Missing query argument
        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("required", result.Content[0].Text);
    }

    [Fact]
    public async Task FindTool_ValidatesRequiredNameParameter()
    {
        var mockRepo = new Mock<IDocumentRepository>();
        var tool = new FindTool(mockRepo.Object);

        Assert.Equal("find", tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));

        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("'name' parameter is required", result.Content[0].Text);
    }

    [Fact]
    public async Task ReferencesTool_ValidatesRequiredSymbolParameter()
    {
        var mockRel = new Mock<IRelationshipService>();
        var tool = new ReferencesTool(mockRel.Object);

        Assert.Equal("references", tool.Name);

        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("'symbol' parameter is required", result.Content[0].Text);
    }

    [Fact]
    public async Task RelatedTool_ValidatesRequiredTargetParameter()
    {
        var mockRel = new Mock<IRelationshipService>();
        var tool = new RelatedTool(mockRel.Object);

        Assert.Equal("related", tool.Name);

        var emptyArgs = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(emptyArgs);

        Assert.True(result.IsError);
        Assert.Contains("'target' parameter is required", result.Content[0].Text);
    }
}
