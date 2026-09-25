using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Indexer.Pipeline;
using DocumentGraph.Indexer.Watcher;
using DocumentGraph.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGraph.Tests;

public class WatcherAndRelationshipTests
{
    private static IndexingPipeline CreateMockPipeline()
    {
        var mockScanner = new Mock<IFileScanner>();
        var mockParser = new Mock<IDocumentParser>();
        var mockChunker = new Mock<IChunker>();
        var mockRepo = new Mock<IDocumentRepository>();

        return new IndexingPipeline(
            mockScanner.Object,
            new[] { mockParser.Object },
            new[] { mockChunker.Object },
            mockRepo.Object,
            NullLogger<IndexingPipeline>.Instance);
    }

    [Fact]
    public void DocumentWatcher_ThrowsOnNonExistentDirectory()
    {
        var pipeline = CreateMockPipeline();

        using var watcher = new DocumentWatcher(
            pipeline,
            new DocumentGraphConfig(),
            NullLogger<DocumentWatcher>.Instance);

        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentDir_" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => watcher.Start(nonExistentPath));
    }

    [Fact]
    public void DocumentWatcher_StartsAndStopsOnValidDirectory()
    {
        var pipeline = CreateMockPipeline();

        using var watcher = new DocumentWatcher(
            pipeline,
            new DocumentGraphConfig(),
            NullLogger<DocumentWatcher>.Instance);

        var tempDir = Path.Combine(Path.GetTempPath(), "WatcherTestDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Should succeed without throwing
            watcher.Start(tempDir);
            watcher.Stop();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void RelationshipGraphNodeAndEdge_PropertiesFunctionCorrectly()
    {
        var node1 = new RelationshipGraphNode
        {
            Id = 101,
            Type = "document",
            Name = "UserService.cs",
            Location = "src/Services/UserService.cs"
        };

        var node2 = new RelationshipGraphNode
        {
            Id = 102,
            Type = "symbol",
            Name = "IUserRepository",
            Location = "src/Data/IUserRepository.cs:12"
        };

        var edge = new RelationshipGraphEdge
        {
            RelationshipType = "REFERENCES",
            Confidence = 0.95f,
            Source = node1,
            Target = node2
        };

        Assert.Equal(101, edge.Source.Id);
        Assert.Equal("UserService.cs", edge.Source.Name);
        Assert.Equal(102, edge.Target.Id);
        Assert.Equal("IUserRepository", edge.Target.Name);
        Assert.Equal("REFERENCES", edge.RelationshipType);
        Assert.Equal(0.95f, edge.Confidence);
    }

    [Fact]
    public void RelatedItemsResult_ContainsCenterAndNeighborhood()
    {
        var center = new RelationshipGraphNode
        {
            Id = 1,
            Type = "document",
            Name = "OrderController.cs",
            Location = "src/Controllers/OrderController.cs"
        };

        var related = new RelatedItemsResult
        {
            Center = center,
            CodeGraphContext = "Symbols: CreateOrder, CancelOrder"
        };

        var clientNode = new RelationshipGraphNode
        {
            Id = 2,
            Type = "document",
            Name = "ApiGateway.cs",
            Location = "src/Gateway/ApiGateway.cs"
        };

        var dbNode = new RelationshipGraphNode
        {
            Id = 3,
            Type = "document",
            Name = "OrderRepository.cs",
            Location = "src/Data/OrderRepository.cs"
        };

        related.Incoming.Add(new RelationshipGraphEdge
        {
            RelationshipType = "CALLS",
            Confidence = 1.0f,
            Source = clientNode,
            Target = center
        });

        related.Outgoing.Add(new RelationshipGraphEdge
        {
            RelationshipType = "USES",
            Confidence = 0.88f,
            Source = center,
            Target = dbNode
        });

        Assert.Equal("OrderController.cs", related.Center.Name);
        Assert.Single(related.Incoming);
        Assert.Single(related.Outgoing);
        Assert.Equal("ApiGateway.cs", related.Incoming[0].Source.Name);
        Assert.Equal("OrderRepository.cs", related.Outgoing[0].Target.Name);
        Assert.Equal("Symbols: CreateOrder, CancelOrder", related.CodeGraphContext);
    }

    [Fact]
    public async Task RelationshipService_MockInterfaceReturnsExpectedResults()
    {
        var mockService = new Mock<IRelationshipService>();

        mockService.Setup(r => r.BuildAllRelationshipsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        mockService.Setup(r => r.BuildRelationshipsForDocumentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var total = await mockService.Object.BuildAllRelationshipsAsync();
        var docRels = await mockService.Object.BuildRelationshipsForDocumentAsync(10);

        Assert.Equal(42, total);
        Assert.Equal(5, docRels);
    }
}
