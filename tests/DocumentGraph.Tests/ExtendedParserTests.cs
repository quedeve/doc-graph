using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentGraph.Parsers.Code;
using DocumentGraph.Parsers.Data;
using DocumentGraph.Parsers.Documents;
using DocumentGraph.Parsers.Text;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocumentGraph.Tests;

public class ExtendedParserTests
{
    [Fact]
    public async Task TextParser_ParsesPlainTextIntoSections()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        try
        {
            var content = "Paragraph 1: DocumentGraph overview.\n\nParagraph 2: Second section detailing features.";
            await File.WriteAllTextAsync(tempFile, content);

            var parser = new TextParser();
            var parsed = await parser.ParseAsync(tempFile);

            Assert.Equal("document", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("DocumentGraph overview", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task JsonParser_ParsesStructuredJsonIntoSections()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            var json = @"{
                ""service"": ""AuthenticationService"",
                ""version"": ""2.1"",
                ""endpoints"": [""/api/login"", ""/api/logout""]
            }";
            await File.WriteAllTextAsync(tempFile, json);

            var parser = new JsonParser();
            var parsed = await parser.ParseAsync(tempFile);

            Assert.Equal("data", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains(parsed.Sections, s => s.Content.Contains("AuthenticationService"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task XmlParser_ParsesNodesAndAttributesIntoSections()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
        try
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <configuration>
                <auth enabled=""true"">
                    <tokenExpiryMinutes>60</tokenExpiryMinutes>
                </auth>
            </configuration>";
            await File.WriteAllTextAsync(tempFile, xml);

            var parser = new XmlParser();
            var parsed = await parser.ParseAsync(tempFile);

            Assert.Equal("data", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains(parsed.Sections, s => s.Content.Contains("tokenExpiryMinutes"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CshtmlParser_ExtractsRazorDirectivesAndCodeBlocks()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cshtml");
        try
        {
            var cshtml = @"@page ""/login""
@model LoginViewModel
@inject IAuthenticationService AuthService

<h1>Login</h1>

@code {
    private string username = """";
}";
            await File.WriteAllTextAsync(tempFile, cshtml);

            var parser = new CshtmlParser();
            var parsed = await parser.ParseAsync(tempFile);

            Assert.Equal("code", parsed.DocumentType);
            Assert.NotEmpty(parsed.Symbols);
            Assert.Contains(parsed.Symbols, s => s.Name == "LoginViewModel");
            Assert.Equal("/login", parsed.Metadata["route"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PptxParser_ParsesPresentationSlidesAndShapes()
    {
        var tempPptx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pptx");
        try
        {
            using (var presentationDoc = PresentationDocument.Create(tempPptx, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
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
                                    new ApplicationNonVisualDrawingProperties(
                                        new PlaceholderShape { Type = PlaceholderValues.Title })),
                                new ShapeProperties(),
                                new TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(new A.Run(new A.Text("System Architecture Overview")))))
                        )));

                var slideIdList = presentationPart.Presentation.AppendChild(new SlideIdList());
                slideIdList.Append(new SlideId
                {
                    Id = 256,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });

                presentationPart.Presentation.Save();
            }

            var parser = new PptxParser();
            var parsed = await parser.ParseAsync(tempPptx);

            Assert.Equal("pptx", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Equal(1, parsed.Sections[0].SlideNumber);
            Assert.Contains("System Architecture Overview", parsed.Sections[0].Title);
        }
        finally
        {
            if (File.Exists(tempPptx)) File.Delete(tempPptx);
        }
    }
}
