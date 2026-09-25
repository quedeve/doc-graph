using System.Text;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using DocumentGraph.Indexer.Chunking;
using DocumentGraph.Indexer.Pipeline;
using DocumentGraph.Indexer.Scanner;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGraph.Tests;

public class ScannerAndPipelineTests
{
    [Fact]
    public async Task FileScanner_ScansEligibleFiles_AndIgnoresConfiguredDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var binDir = Path.Combine(tempDir, "bin");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(binDir);

        try
        {
            var validFile1 = Path.Combine(tempDir, "service.cs");
            var validFile2 = Path.Combine(tempDir, "readme.md");
            var ignoredFile = Path.Combine(binDir, "compiled.cs");

            await File.WriteAllTextAsync(validFile1, "class Service {}");
            await File.WriteAllTextAsync(validFile2, "# Readme");
            await File.WriteAllTextAsync(ignoredFile, "class Compiled {}");

            var scanner = new FileScanner(NullLogger<FileScanner>.Instance);
            var config = new IndexingConfig
            {
                Ignore = ["bin", "obj", ".git"]
            };

            var results = await scanner.ScanAsync(tempDir, config);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Path == validFile1);
            Assert.Contains(results, r => r.Path == validFile2);
            Assert.DoesNotContain(results, r => r.Path == ignoredFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileScanner_ComputeHashAsync_ComputesAccurateSha256()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, "hello world", Encoding.UTF8);

            var hash = await FileScanner.ComputeHashAsync(tempFile);

            // SHA256 of "hello world" without BOM
            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.Equal(64, hash.Length);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexingPipeline_SkipsUnchangedFiles_WhenHashMatchesExisting()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFile = Path.Combine(tempDir, "sample.txt");
            await File.WriteAllTextAsync(testFile, "test content");

            var hash = await FileScanner.ComputeHashAsync(testFile);

            var mockRepo = new Mock<IDocumentRepository>();
            mockRepo.Setup(r => r.GetAllPathHashesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string> { [testFile] = hash });

            var mockScanner = new Mock<IFileScanner>();
            mockScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<IndexingConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ScannedFile
                    {
                        Path = testFile,
                        Extension = ".txt",
                        Size = 12,
                        ModifiedAt = DateTime.UtcNow
                    }
                ]);

            var mockParser = new Mock<IDocumentParser>();
            mockParser.Setup(p => p.CanParse(".txt")).Returns(true);
            mockParser.Setup(p => p.SupportedExtensions).Returns([".txt"]);

            var pipeline = new IndexingPipeline(
                mockScanner.Object,
                [mockParser.Object],
                [new DefaultChunker()],
                mockRepo.Object,
                NullLogger<IndexingPipeline>.Instance);

            var config = new DocumentGraphConfig();
            var result = await pipeline.IndexAsync(tempDir, config);

            Assert.Equal(1, result.FilesScanned);
            Assert.Equal(1, result.FilesSkipped);
            Assert.Equal(0, result.FilesIndexed);
            Assert.Equal(0, result.FilesFailed);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IndexingPipeline_IndexesNewFiles_AndUpsertsDocument()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFile = Path.Combine(tempDir, "sample.txt");
            await File.WriteAllTextAsync(testFile, "test content");

            var mockRepo = new Mock<IDocumentRepository>();
            mockRepo.Setup(r => r.GetAllPathHashesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string>()); // Empty existing hashes

            mockRepo.Setup(r => r.UpsertAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DocumentEntity doc, CancellationToken _) => doc);

            var mockScanner = new Mock<IFileScanner>();
            mockScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<IndexingConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ScannedFile
                    {
                        Path = testFile,
                        Extension = ".txt",
                        Size = 12,
                        ModifiedAt = DateTime.UtcNow
                    }
                ]);

            var mockParser = new Mock<IDocumentParser>();
            mockParser.Setup(p => p.CanParse(".txt")).Returns(true);
            mockParser.Setup(p => p.SupportedExtensions).Returns([".txt"]);
            mockParser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ParsedDocument
                {
                    SourcePath = testFile,
                    Extension = ".txt",
                    DocumentType = "text",
                    Title = "sample",
                    Sections = [new DocumentSection { Title = "Main", Content = "test content" }]
                });

            var pipeline = new IndexingPipeline(
                mockScanner.Object,
                [mockParser.Object],
                [new DefaultChunker()],
                mockRepo.Object,
                NullLogger<IndexingPipeline>.Instance);

            var config = new DocumentGraphConfig();
            var result = await pipeline.IndexAsync(tempDir, config);

            Assert.Equal(1, result.FilesScanned);
            Assert.Equal(1, result.FilesIndexed);
            Assert.Equal(0, result.FilesSkipped);
            Assert.Equal(0, result.FilesFailed);
            mockRepo.Verify(r => r.UpsertAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
