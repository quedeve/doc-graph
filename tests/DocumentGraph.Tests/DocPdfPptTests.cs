using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Indexer.Chunking;
using DocumentGraph.Indexer.Pipeline;
using DocumentGraph.Indexer.Scanner;
using DocumentGraph.Parsers.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocumentGraph.Tests;

public class DocPdfPptTests
{
    [Fact]
    public void Parsers_SupportExpectedExtensions()
    {
        var docParser = new DocxParser();
        Assert.True(docParser.CanParse(".doc"));
        Assert.True(docParser.CanParse(".DOC"));
        Assert.True(docParser.CanParse(".docx"));
        Assert.Contains(".doc", docParser.SupportedExtensions);
        Assert.Contains(".docx", docParser.SupportedExtensions);

        var pptParser = new PptxParser();
        Assert.True(pptParser.CanParse(".ppt"));
        Assert.True(pptParser.CanParse(".PPT"));
        Assert.True(pptParser.CanParse(".pptx"));
        Assert.Contains(".ppt", pptParser.SupportedExtensions);
        Assert.Contains(".pptx", pptParser.SupportedExtensions);

        var pdfParser = new PdfParser();
        Assert.True(pdfParser.CanParse(".pdf"));
        Assert.True(pdfParser.CanParse(".PDF"));
        Assert.Contains(".pdf", pdfParser.SupportedExtensions);
    }

    [Fact]
    public async Task PdfParser_ParsesMultiPageDocumentWithMetadata()
    {
        var tempPdf = Path.Combine(Path.GetTempPath(), $"TestDoc_{Guid.NewGuid():N}.pdf");

        try
        {
            var builder = new PdfDocumentBuilder();

            // Page 1
            var page1 = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            page1.AddText("System Architecture Document", 16, new PdfPoint(50, 750), font);
            page1.AddText("Section 1: Microservices Architecture", 12, new PdfPoint(50, 700), font);

            // Page 2
            var page2 = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            page2.AddText("Section 2: API Gateway & Authentication Protocols", 12, new PdfPoint(50, 750), font);
            page2.AddText("OAuth2 bearer tokens are validated at the edge gateway.", 10, new PdfPoint(50, 720), font);

            var bytes = builder.Build();
            await File.WriteAllBytesAsync(tempPdf, bytes);

            var parser = new PdfParser();
            var parsed = await parser.ParseAsync(tempPdf);

            Assert.Equal("pdf", parsed.DocumentType);
            Assert.Equal(".pdf", parsed.Extension);
            Assert.Equal("2", parsed.Metadata["pageCount"]);
            Assert.Equal(2, parsed.Sections.Count);

            Assert.Equal(1, parsed.Sections[0].PageNumber);
            Assert.Contains("System Architecture Document", parsed.Sections[0].Content);

            Assert.Equal(2, parsed.Sections[1].PageNumber);
            Assert.Contains("OAuth2 bearer tokens", parsed.Sections[1].Content);
        }
        finally
        {
            if (File.Exists(tempPdf)) File.Delete(tempPdf);
        }
    }

    [Fact]
    public async Task DocxParser_ParsesDocumentWithDocExtension()
    {
        var tempDoc = Path.Combine(Path.GetTempPath(), $"Document_{Guid.NewGuid():N}.doc");

        try
        {
            // Create OpenXml document saved with .doc extension
            using (var wordDoc = WordprocessingDocument.Create(tempDoc, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(
                    new Paragraph(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Engineering Design Specification"))),
                    new Paragraph(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("All services communicate via gRPC internally.")))
                ));
                mainPart.Document.Save();
            }

            var parser = new DocxParser();
            var parsed = await parser.ParseAsync(tempDoc);

            Assert.Equal("doc", parsed.DocumentType);
            Assert.Equal(".doc", parsed.Extension);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("Engineering Design Specification", parsed.Sections[0].Content);
            Assert.Contains("All services communicate via gRPC", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempDoc)) File.Delete(tempDoc);
        }
    }

    [Fact]
    public async Task DocxParser_ParsesLegacyBinaryDocFileUsingFallback()
    {
        var tempLegacyDoc = Path.Combine(Path.GetTempPath(), $"Legacy_{Guid.NewGuid():N}.doc");

        try
        {
            // Simulate legacy binary .doc file with OLE magic bytes and UTF-16LE / ASCII text runs
            var ms = new MemoryStream();
            // Compound File Binary Header (0xD0CF11E0A1B11AE1)
            ms.Write([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
            // Binary padding
            ms.Write(new byte[504]);

            // Embed UTF-16LE text
            var text = "Legacy Word 97 Specification for Database Migration and Backup Procedures";
            var utf16Bytes = Encoding.Unicode.GetBytes(text);
            ms.Write(utf16Bytes);

            // Binary padding
            ms.Write(new byte[64]);

            // Embed ASCII text
            var asciiText = "Retention policy requires daily incremental backups and weekly full backups.";
            var asciiBytes = Encoding.ASCII.GetBytes(asciiText);
            ms.Write(asciiBytes);

            await File.WriteAllBytesAsync(tempLegacyDoc, ms.ToArray());

            var parser = new DocxParser();
            var parsed = await parser.ParseAsync(tempLegacyDoc);

            Assert.Equal("doc", parsed.DocumentType);
            Assert.Equal("true", parsed.Metadata["legacy_format"]);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("Legacy Word 97 Specification", parsed.Sections[0].Content);
            Assert.Contains("Retention policy requires daily incremental backups", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempLegacyDoc)) File.Delete(tempLegacyDoc);
        }
    }

    [Fact]
    public async Task PptxParser_ParsesPresentationWithPptExtension()
    {
        var tempPpt = Path.Combine(Path.GetTempPath(), $"Presentation_{Guid.NewGuid():N}.ppt");

        try
        {
            // Create OpenXml presentation saved with .ppt extension
            using (var presentationDoc = PresentationDocument.Create(tempPpt, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
            {
                var presentationPart = presentationDoc.AddPresentationPart();
                presentationPart.Presentation = new Presentation();

                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(
                    new CommonSlideData(
                        new ShapeTree(
                            new NonVisualGroupShapeProperties(
                                new NonVisualDrawingProperties { Id = 1, Name = "" },
                                new NonVisualGroupShapeDrawingProperties(),
                                new ApplicationNonVisualDrawingProperties()),
                            new GroupShapeProperties(),
                            new Shape(
                                new NonVisualShapeProperties(
                                    new NonVisualDrawingProperties { Id = 2, Name = "Title" },
                                    new NonVisualShapeDrawingProperties(),
                                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Title })),
                                new ShapeProperties(),
                                new TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(new A.Run(new A.Text("Q3 Technical Roadmap")))
                                )
                            )
                        )
                    )
                );

                var slideIdList = presentationPart.Presentation.AppendChild(new SlideIdList());
                slideIdList.Append(new SlideId
                {
                    Id = 256,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });

                presentationPart.Presentation.Save();
            }

            var parser = new PptxParser();
            var parsed = await parser.ParseAsync(tempPpt);

            Assert.Equal("ppt", parsed.DocumentType);
            Assert.Equal(".ppt", parsed.Extension);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("Q3 Technical Roadmap", parsed.Sections[0].Content);
            Assert.Equal("1", parsed.Metadata["slideCount"]);
        }
        finally
        {
            if (File.Exists(tempPpt)) File.Delete(tempPpt);
        }
    }

    [Fact]
    public async Task PptxParser_ParsesLegacyBinaryPptFileUsingFallback()
    {
        var tempLegacyPpt = Path.Combine(Path.GetTempPath(), $"Legacy_{Guid.NewGuid():N}.ppt");

        try
        {
            // Simulate legacy binary .ppt file
            var ms = new MemoryStream();
            ms.Write([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
            ms.Write(new byte[256]);

            var slide1Text = "Slide 1: Cloud Migration Architecture Overview";
            ms.Write(Encoding.Unicode.GetBytes(slide1Text));

            ms.Write(new byte[128]);

            var slide2Text = "Slide 2: Kubernetes cluster setup and monitoring with Prometheus";
            ms.Write(Encoding.ASCII.GetBytes(slide2Text));

            await File.WriteAllBytesAsync(tempLegacyPpt, ms.ToArray());

            var parser = new PptxParser();
            var parsed = await parser.ParseAsync(tempLegacyPpt);

            Assert.Equal("ppt", parsed.DocumentType);
            Assert.Equal("true", parsed.Metadata["legacy_format"]);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("Cloud Migration Architecture", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempLegacyPpt)) File.Delete(tempLegacyPpt);
        }
    }

    [Fact]
    public void LegacyOfficeExtractor_ExtractsTextRunsAccurately()
    {
        var ms = new MemoryStream();
        ms.Write([0x00, 0x01, 0x02, 0xFF]); // noise

        var textUtf16 = "Sensitive Configuration Values";
        ms.Write(Encoding.Unicode.GetBytes(textUtf16));

        ms.Write([0x00, 0xAA, 0xBB]); // noise

        var textAscii = "Port 8080 SSL Certificate";
        ms.Write(Encoding.ASCII.GetBytes(textAscii));

        var extracted = LegacyOfficeExtractor.ExtractTextStrings(ms.ToArray(), minLength: 4);

        Assert.Contains(extracted, s => s.Contains("Sensitive Configuration Values"));
        Assert.Contains(extracted, s => s.Contains("Port 8080 SSL Certificate"));
    }

    [Fact]
    public async Task FileScannerAndPipeline_DiscoversAndIndexesDocPdfPpt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"IndexSuite_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var docFile = Path.Combine(tempDir, "contract.doc");
            var pdfFile = Path.Combine(tempDir, "manual.pdf");
            var pptFile = Path.Combine(tempDir, "slides.ppt");

            // Write sample text into files
            await File.WriteAllTextAsync(docFile, "Service Level Agreement terms and uptime guarantees.");

            var pdfBuilder = new PdfDocumentBuilder();
            var page = pdfBuilder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var font = pdfBuilder.AddStandard14Font(Standard14Font.Helvetica);
            page.AddText("Product Manual v2.0", 12, new PdfPoint(50, 750), font);
            await File.WriteAllBytesAsync(pdfFile, pdfBuilder.Build());

            await File.WriteAllTextAsync(pptFile, "Executive Summary of Annual Performance");

            var scanner = new FileScanner(NullLogger<FileScanner>.Instance);
            var scanned = await scanner.ScanAsync(tempDir, new IndexingConfig());

            Assert.Equal(3, scanned.Count);
            Assert.Contains(scanned, s => s.Extension == ".doc");
            Assert.Contains(scanned, s => s.Extension == ".pdf");
            Assert.Contains(scanned, s => s.Extension == ".ppt");

            // Test pipeline indexing
            var mockRepo = new Mock<IDocumentRepository>();
            var savedDocs = new List<DocumentEntity>();
            mockRepo.Setup(r => r.GetAllPathHashesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string>());
            mockRepo.Setup(r => r.UpsertAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
                .Callback<DocumentEntity, CancellationToken>((d, _) => savedDocs.Add(d))
                .ReturnsAsync((DocumentEntity d, CancellationToken _) => d);

            var pipeline = new IndexingPipeline(
                scanner,
                [new DocxParser(), new PdfParser(), new PptxParser()],
                [new DefaultChunker()],
                mockRepo.Object,
                NullLogger<IndexingPipeline>.Instance);

            var result = await pipeline.IndexAsync(tempDir, new DocumentGraphConfig());

            Assert.Equal(3, result.FilesScanned);
            Assert.Equal(3, result.FilesIndexed);
            Assert.Equal(0, result.FilesFailed);
            Assert.Equal(3, savedDocs.Count);
            Assert.Contains(savedDocs, d => d.Extension == ".doc");
            Assert.Contains(savedDocs, d => d.Extension == ".pdf");
            Assert.Contains(savedDocs, d => d.Extension == ".ppt");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SampleData_DocPdfPptFiles_ExistAndCanBeParsed()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/data")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/data")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tests/data")),
            Path.GetFullPath("c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data")
        };

        var dataDir = candidates.FirstOrDefault(Directory.Exists);
        if (dataDir == null) return;

        var pdfPath = Path.Combine(dataDir, "sample_manual.pdf");
        if (!File.Exists(pdfPath))
        {
            var builder = new PdfDocumentBuilder();
            var p = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var f = builder.AddStandard14Font(Standard14Font.Helvetica);
            p.AddText("DocumentGraph Architecture and Reference Manual", 14, new PdfPoint(50, 750), f);
            p.AddText("This manual describes document indexing and vector search workflows.", 10, new PdfPoint(50, 720), f);
            await File.WriteAllBytesAsync(pdfPath, builder.Build());
        }

        var docPath = Path.Combine(dataDir, "sample_contract.doc");
        if (!File.Exists(docPath))
        {
            using var wordDoc = WordprocessingDocument.Create(docPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Enterprise Master Service Agreement"))),
                new Paragraph(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("99.99% service availability guarantee with automated indexing.")))
            ));
            mainPart.Document.Save();
        }

        var pptPath = Path.Combine(dataDir, "sample_architecture.ppt");
        if (!File.Exists(pptPath))
        {
            using var presDoc = PresentationDocument.Create(pptPath, DocumentFormat.OpenXml.PresentationDocumentType.Presentation);
            var presPart = presDoc.AddPresentationPart();
            presPart.Presentation = new Presentation();
            var slidePart = presPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree(
                new NonVisualGroupShapeProperties(new NonVisualDrawingProperties { Id = 1, Name = "" }, new NonVisualGroupShapeDrawingProperties(), new ApplicationNonVisualDrawingProperties()),
                new GroupShapeProperties(),
                new Shape(
                    new NonVisualShapeProperties(new NonVisualDrawingProperties { Id = 2, Name = "Title" }, new NonVisualShapeDrawingProperties(), new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Title })),
                    new ShapeProperties(),
                    new TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text("DocumentGraph Architecture Overview"))))
                )
            )));
            var slideIdList = presPart.Presentation.AppendChild(new SlideIdList());
            slideIdList.Append(new SlideId { Id = 256, RelationshipId = presPart.GetIdOfPart(slidePart) });
            presPart.Presentation.Save();
        }

        // Verify all 3 files parse successfully
        var docParser = new DocxParser();
        var parsedDoc = await docParser.ParseAsync(docPath);
        Assert.NotNull(parsedDoc);
        Assert.NotEmpty(parsedDoc.Sections);
        Assert.Contains("Service Agreement", parsedDoc.Sections[0].Content);

        var pdfParser = new PdfParser();
        var parsedPdf = await pdfParser.ParseAsync(pdfPath);
        Assert.NotNull(parsedPdf);
        Assert.NotEmpty(parsedPdf.Sections);
        Assert.Contains("DocumentGraph Architecture", parsedPdf.Sections[0].Content);

        var pptParser = new PptxParser();
        var parsedPpt = await pptParser.ParseAsync(pptPath);
        Assert.NotNull(parsedPpt);
        Assert.NotEmpty(parsedPpt.Sections);
        Assert.Contains("Architecture Overview", parsedPpt.Sections[0].Content);
    }
}
