using DocumentGraph.Parsers.Code;
using DocumentGraph.Parsers.Data;
using DocumentGraph.Parsers.Text;
using Xunit;

namespace DocumentGraph.Tests;

public class ParserTests
{
    private static readonly string TestDataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/data"));

    [Fact]
    public async Task CSharpParser_ExtractsClassesInterfacesAndMethods()
    {
        var parser = new CSharpParser();
        var filePath = Path.Combine(TestDataDir, "SampleAuth.cs");
        Assert.True(File.Exists(filePath), $"Test file not found: {filePath}");

        var parsed = await parser.ParseAsync(filePath);

        Assert.Equal("SampleAuth", parsed.Title);
        Assert.Equal(".cs", parsed.Extension);
        Assert.Equal("code", parsed.DocumentType);
        Assert.NotEmpty(parsed.Symbols);

        var iface = parsed.Symbols.FirstOrDefault(s => s.Name == "IAuthenticationService");
        Assert.NotNull(iface);

        var cls = parsed.Symbols.FirstOrDefault(s => s.Name == "AuthenticationService");
        Assert.NotNull(cls);

        var method = cls.Children.FirstOrDefault(s => s.Name == "ValidateTokenAsync");
        Assert.NotNull(method);
        Assert.True(method.LineStart > 0);
        Assert.True(method.LineEnd >= method.LineStart);
    }

    [Fact]
    public async Task TypeScriptParser_ExtractsInterfacesAndClasses()
    {
        var parser = new TypeScriptJsParser();
        var filePath = Path.Combine(TestDataDir, "sampleAuth.ts");
        Assert.True(File.Exists(filePath), $"Test file not found: {filePath}");

        var parsed = await parser.ParseAsync(filePath);

        Assert.Equal("sampleAuth", parsed.Title);
        Assert.Equal(".ts", parsed.Extension);
        Assert.NotEmpty(parsed.Symbols);

        Assert.Contains(parsed.Symbols, s => s.Name == "UserProfile");
        Assert.Contains(parsed.Symbols, s => s.Name == "TokenVerifier");
        Assert.Contains(parsed.Symbols, s => s.Name == "parseJwt");
    }

    [Fact]
    public async Task MarkdownParser_ExtractsHeadingsAsSections()
    {
        var parser = new MarkdownParser();
        var filePath = Path.Combine(TestDataDir, "architecture.md");
        Assert.True(File.Exists(filePath));

        var parsed = await parser.ParseAsync(filePath);

        Assert.Contains("Architecture", parsed.Title);
        Assert.True(parsed.Sections.Count >= 3);
        Assert.Contains(parsed.Sections, s => s.Title.Contains("Authentication Overview"));
        Assert.Contains(parsed.Sections, s => s.Title.Contains("MFA Flow"));
    }

    [Fact]
    public async Task CsvParser_ExtractsRowsAsSections()
    {
        var parser = new CsvParser();
        var filePath = Path.Combine(TestDataDir, "sample_data.csv");
        Assert.True(File.Exists(filePath));

        var parsed = await parser.ParseAsync(filePath);

        Assert.NotEmpty(parsed.Sections);
        Assert.Contains(parsed.Sections, s => s.Content.Contains("alice"));
    }

    [Fact]
    public async Task SqlParser_ExtractsTablesAndProcedures()
    {
        var parser = new SqlParser();
        var filePath = Path.Combine(TestDataDir, "sample_schema.sql");
        Assert.True(File.Exists(filePath));

        var parsed = await parser.ParseAsync(filePath);

        Assert.NotEmpty(parsed.Symbols);
        Assert.Contains(parsed.Symbols, s => s.Name == "users");
        Assert.Contains(parsed.Symbols, s => s.Name == "deactivate_user");
    }

    [Fact]
    public async Task DocxParser_ParsesDocumentWithParagraphs()
    {
        var tempDocx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        try
        {
            using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(tempDocx, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                    new DocumentFormat.OpenXml.Wordprocessing.Body(
                        new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                new DocumentFormat.OpenXml.Wordprocessing.Text("Authentication Architecture Overview")))));
                mainPart.Document.Save();
            }

            var parser = new DocumentGraph.Parsers.Documents.DocxParser();
            var parsed = await parser.ParseAsync(tempDocx);

            Assert.Equal("docx", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("Authentication Architecture Overview", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempDocx)) File.Delete(tempDocx);
        }
    }

    [Fact]
    public async Task XlsxParser_ParsesSpreadsheetSheetsAndRows()
    {
        var tempXlsx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        try
        {
            using (var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(tempXlsx, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = doc.AddWorkbookPart();
                workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();

                var worksheetPart = workbookPart.AddNewPart<DocumentFormat.OpenXml.Packaging.WorksheetPart>();
                var sheetData = new DocumentFormat.OpenXml.Spreadsheet.SheetData();

                var row = new DocumentFormat.OpenXml.Spreadsheet.Row();
                row.Append(new DocumentFormat.OpenXml.Spreadsheet.Cell
                {
                    DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString,
                    InlineString = new DocumentFormat.OpenXml.Spreadsheet.InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text("Endpoint"))
                });
                row.Append(new DocumentFormat.OpenXml.Spreadsheet.Cell
                {
                    DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString,
                    InlineString = new DocumentFormat.OpenXml.Spreadsheet.InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text("/api/auth/login"))
                });
                sheetData.Append(row);

                worksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(sheetData);

                var sheets = workbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
                sheets.Append(new DocumentFormat.OpenXml.Spreadsheet.Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Authentication"
                });

                workbookPart.Workbook.Save();
            }

            var parser = new DocumentGraph.Parsers.Documents.XlsxParser();
            var parsed = await parser.ParseAsync(tempXlsx);

            Assert.Equal("xlsx", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Equal("Authentication", parsed.Sections[0].SheetName);
            Assert.Contains("/api/auth/login", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempXlsx)) File.Delete(tempXlsx);
        }
    }

    [Fact]
    public async Task PdfParser_ParsesPagesAndMetadata()
    {
        var tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        try
        {
            var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
            page.AddText("Authentication Specification v1.0", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);

            var bytes = builder.Build();
            await File.WriteAllBytesAsync(tempPdf, bytes);

            var parser = new DocumentGraph.Parsers.Documents.PdfParser();
            var parsed = await parser.ParseAsync(tempPdf);

            Assert.Equal("pdf", parsed.DocumentType);
            Assert.NotEmpty(parsed.Sections);
            Assert.Contains("Authentication Specification", parsed.Sections[0].Content);
        }
        finally
        {
            if (File.Exists(tempPdf)) File.Delete(tempPdf);
        }
    }
}
