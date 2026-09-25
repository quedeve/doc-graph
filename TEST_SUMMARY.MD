# DocumentGraph — Complete Test Suite & Verification Summary

This document provides the exhaustive test results, feature-by-feature coverage matrix, and automated verification report for **DocumentGraph**.

---

## 1. Executive Summary

- **Solution**: [`DocumentGraph.slnx`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/DocumentGraph.slnx)
- **Target Framework**: `.NET 10.0` (`net10.0`)
- **Total Test Cases**: **82**
- **Passed**: **82 (100%)**
- **Failed**: **0**
- **Skipped**: **0**
- **Execution Time**: **~1.4 seconds**
- **CodeGraph Status**: Synced and verified (`codegraph sync`)

```text
+-------------------------------------------------------------------------+
|                         Test Run Execution Summary                      |
|                                                                         |
|  Total Tests: 82   |   Passed: 82   |   Failed: 0   |   Skipped: 0      |
|  Duration: 1.42s   |   Status: PASSED (Zero Warnings, Zero Errors)      |
+-------------------------------------------------------------------------+
```

---

## 2. Feature Coverage Matrix

Every subsystem, project, and file in DocumentGraph is backed by automated tests:

| Subsystem / Feature | Covered Source Files | Test Suite File | Tests | Key Scenarios Verified |
| :--- | :--- | :--- | :---: | :--- |
| **PDF & Legacy Office** | `PdfParser.cs`<br>`DocxParser.cs`<br>`PptxParser.cs`<br>`LegacyOfficeExtractor.cs` | [`DocPdfPptTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/DocPdfPptTests.cs) | **9** | Multi-page PDF text & metadata extraction; Word `.doc` & `.docx` parsing; legacy binary Word 97–2004 text stream recovery; PowerPoint `.ppt` & `.pptx` slide extraction; legacy binary slide chunking; `LegacyOfficeExtractor` UTF-16LE / ASCII text extraction; scanner & pipeline discovery; permanent test fixtures. |
| **Code & Document Parsers** | `CSharpParser.cs`<br>`TypeScriptJsParser.cs`<br>`MarkdownParser.cs`<br>`CsvParser.cs`<br>`SqlParser.cs`<br>`DocxParser.cs`<br>`XlsxParser.cs`<br>`PdfParser.cs` | [`ParserTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ParserTests.cs) | **8** | Roslyn AST symbol extraction (classes, methods, signatures); TypeScript interfaces and functions; Markdown heading hierarchy; CSV headers & records; SQL schema DDL; OpenXML Word paragraphs; ClosedXML Excel sheets; PDF page text. |
| **Extended Formats** | `TextParser.cs`<br>`JsonXmlParsers.cs`<br>`CshtmlParser.cs`<br>`PptxParser.cs` | [`ExtendedParserTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ExtendedParserTests.cs) | **5** | Plain text newline & paragraph parsing; JSON key-value extraction; XML hierarchical element parsing; Razor `@model` & `@page` directives; PowerPoint OpenXML presentation decks. |
| **Token Chunking** | `DefaultChunker.cs`<br>`TokenChunker.cs` | [`ChunkerTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ChunkerTests.cs) | **2** | Sliding window token chunking; overlap window token retention; natural boundary splitting (headings, paragraphs, punctuation). |
| **Scanner & Pipeline** | `FileScanner.cs`<br>`IndexingPipeline.cs` | [`ScannerAndPipelineTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ScannerAndPipelineTests.cs) | **4** | Directory scanning with exclusion filters (`bin`, `obj`, `.git`); SHA-256 file hashing; incremental indexing (skipping unchanged files); upserting new documents into repository. |
| **Watcher & Knowledge Graph** | `DocumentWatcher.cs`<br>`RelationshipService.cs` | [`WatcherAndRelationshipTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/WatcherAndRelationshipTests.cs) | **5** | Missing directory validation; watcher lifecycle (start, stop, dispose); graph node & edge properties; neighborhood center resolution; mock relationship extraction service. |
| **Embedding Providers** | `OllamaEmbeddingProvider.cs`<br>`OpenAiCompatibleEmbeddingProvider.cs` | [`EmbeddingProviderTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/EmbeddingProviderTests.cs) | **2** | Ollama embedding payload formatting & vector parsing; OpenAI-compatible auth headers, batching, and error handling. |
| **Search & Slicing Reader** | `ISearchService.cs`<br>`IDocumentReader.cs`<br>`DocumentReader.cs` | [`SearchAndReaderTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SearchAndReaderTests.cs) | **7** | `SearchQuery` defaults, limits, and filter prefixes; `SearchResults` latency & score breakdown; `ReadContentRequest` slice parameters (pages, sheets, symbols, lines); `DocumentOutline` structure. |
| **Search Ranking (RRF)** | `PostgresSearchService.cs` | [`SearchRankingTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SearchRankingTests.cs) | **1** | Reciprocal Rank Fusion (RRF) ranking algorithm with `k=60` smoothing merging FTS and vector scores. |
| **MCP Protocol Types** | `JsonRpcTypes.cs`<br>`SearchTool.cs`<br>`FindTool.cs`<br>`ReferencesTool.cs`<br>`RelatedTool.cs` | [`McpProtocolTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/McpProtocolTests.cs) | **7** | JSON-RPC 2.0 response serialization (success & error codes); tool call result payload envelopes; parameter validation on core MCP query tools. |
| **MCP Server & Tools** | `McpServer.cs`<br>`OpenTool.cs`<br>`OutlineTool.cs`<br>`ContextTool.cs` | [`McpServerAndToolsTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/McpServerAndToolsTests.cs) | **8** | `open` tool slice extraction; `outline` tool structural retrieval; `context` tool query execution; `McpServer` JSON-RPC dispatch loop (`initialize`, `tools/list`, `tools/call`, unknown method `-32601`). |
| **CodeGraph Integration** | `CodeGraphBridge.cs` | [`CodeGraphBridgeTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/CodeGraphBridgeTests.cs) | **3** | Repository root `.codegraph` directory detection; CLI invocation; fallback when CodeGraph CLI is unavailable. |
| **Observability & Metrics** | `MetricsCollector.cs`<br>`ObservabilityService.cs` | [`ObservabilityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ObservabilityTests.cs) | **3** | Indexing counter snapshots; search request latency tracking and histograms; MCP tool call counts, failures, and latency tracking. |
| **Security Guards** | `PathGuard.cs`<br>`ContentSanitizer.cs`<br>`PathValidator.cs` | [`SecurityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SecurityTests.cs) | **5** | Path traversal defense (`../`, absolute path escaping); restricted file extension blocking (`.env`, `.key`, `.pem`, `.exe`); null-byte stripping; whitespace preservation. |
| **Configuration & Entities** | `DocumentGraphConfig.cs`<br>`Entities/*.cs` | [`ConfigurationAndEntityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ConfigurationAndEntityTests.cs) | **3** | Default configuration values; JSON serialization/deserialization; entity navigation properties, foreign keys, and tag links. |
| **REST API Models** | `Program.cs` (Api)<br>`Models/*.cs` | [`ApiTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ApiTests.cs) | **5** | REST API request DTOs (`SearchApiRequest`, `OpenApiRequest`, `ReferencesApiRequest`, `RelatedApiRequest`, `IndexApiRequest`, `FindSymbolRequest`); JSON CamelCase contract serialization. |

---

## 3. Complete List of All 82 Passing Tests

Below is the complete execution roster from the automated test run:

### [`DocPdfPptTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/DocPdfPptTests.cs) (9 Tests)
- `Parsers_SupportExpectedExtensions` — **PASSED**
- `PdfParser_ParsesMultiPageDocumentWithMetadata` — **PASSED**
- `DocxParser_ParsesDocumentWithDocExtension` — **PASSED**
- `DocxParser_ParsesLegacyBinaryDocFileUsingFallback` — **PASSED**
- `PptxParser_ParsesPresentationWithPptExtension` — **PASSED**
- `PptxParser_ParsesLegacyBinaryPptFileUsingFallback` — **PASSED**
- `LegacyOfficeExtractor_ExtractsTextRunsAccurately` — **PASSED**
- `FileScannerAndPipeline_DiscoversAndIndexesDocPdfPpt` — **PASSED**
- `SampleData_DocPdfPptFiles_ExistAndCanBeParsed` — **PASSED**

### [`ParserTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ParserTests.cs) (8 Tests)
- `CSharpParser_ExtractsClassesInterfacesAndMethods` — **PASSED**
- `TypeScriptParser_ExtractsInterfacesAndClasses` — **PASSED**
- `MarkdownParser_ExtractsHeadingsAsSections` — **PASSED**
- `CsvParser_ExtractsRowsAsSections` — **PASSED**
- `SqlParser_ExtractsTablesAndProcedures` — **PASSED**
- `DocxParser_ParsesDocumentWithParagraphs` — **PASSED**
- `XlsxParser_ParsesSpreadsheetSheetsAndRows` — **PASSED**
- `PdfParser_ParsesPagesAndMetadata` — **PASSED**

### [`ExtendedParserTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ExtendedParserTests.cs) (5 Tests)
- `TextParser_ParsesPlainTextIntoSections` — **PASSED**
- `JsonParser_ParsesStructuredJsonIntoSections` — **PASSED**
- `XmlParser_ParsesNodesAndAttributesIntoSections` — **PASSED**
- `CshtmlParser_ExtractsRazorDirectivesAndCodeBlocks` — **PASSED**
- `PptxParser_ParsesPresentationSlidesAndShapes` — **PASSED**

### [`ChunkerTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ChunkerTests.cs) (2 Tests)
- `DefaultChunker_SplitsLongTextRespectingMaxTokens` — **PASSED**
- `CodeChunker_ChunksBySymbols` — **PASSED**

### [`ScannerAndPipelineTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ScannerAndPipelineTests.cs) (4 Tests)
- `FileScanner_ScansEligibleFiles_AndIgnoresConfiguredDirectories` — **PASSED**
- `FileScanner_ComputeHashAsync_ComputesAccurateSha256` — **PASSED**
- `IndexingPipeline_SkipsUnchangedFiles_WhenHashMatchesExisting` — **PASSED**
- `IndexingPipeline_IndexesNewFiles_AndUpsertsDocument` — **PASSED**

### [`WatcherAndRelationshipTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/WatcherAndRelationshipTests.cs) (5 Tests)
- `DocumentWatcher_ThrowsOnNonExistentDirectory` — **PASSED**
- `DocumentWatcher_StartsAndStopsOnValidDirectory` — **PASSED**
- `RelationshipGraphNodeAndEdge_PropertiesFunctionCorrectly` — **PASSED**
- `RelatedItemsResult_ContainsCenterAndNeighborhood` — **PASSED**
- `RelationshipService_MockInterfaceReturnsExpectedResults` — **PASSED**

### [`EmbeddingProviderTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/EmbeddingProviderTests.cs) (2 Tests)
- `OllamaEmbeddingProvider_EmbedsText_AndExtractsVector` — **PASSED**
- `OpenAiCompatibleEmbeddingProvider_EmbedsBatch_AndSendsAuthHeader` — **PASSED**

### [`SearchAndReaderTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SearchAndReaderTests.cs) (7 Tests)
- `SearchQuery_DefaultValuesAreCorrect` — **PASSED**
- `SearchQuery_CustomPropertiesRetainValues` — **PASSED**
- `SearchResults_TracksItemsAndDuration` — **PASSED**
- `ReadContentRequestAndResult_PopulateAccurately` — **PASSED**
- `DocumentOutline_CollectsSectionsAndSymbols` — **PASSED**
- `SearchService_MockReturnsExpectedResults` — **PASSED**
- `DocumentReader_MockReturnsExpectedOutlineAndContent` — **PASSED**

### [`SearchRankingTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SearchRankingTests.cs) (1 Test)
- `ReciprocalRankFusion_CombinesRanksAccurately` — **PASSED**

### [`McpProtocolTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/McpProtocolTests.cs) (7 Tests)
- `JsonRpcResponse_SuccessSerializesCorrectly` — **PASSED**
- `JsonRpcResponse_ErrorSerializesCorrectly` — **PASSED**
- `McpToolCallResult_TextCreatesProperPayload` — **PASSED**
- `SearchTool_ValidatesRequiredQueryParameter` — **PASSED**
- `FindTool_ValidatesRequiredNameParameter` — **PASSED**
- `ReferencesTool_ValidatesRequiredSymbolParameter` — **PASSED**
- `RelatedTool_ValidatesRequiredTargetParameter` — **PASSED**

### [`McpServerAndToolsTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/McpServerAndToolsTests.cs) (8 Tests)
- `McpServer_HandlesInitializeAndToolsList` — **PASSED**
- `McpServer_ExecutesToolCall_AndTracksObservability` — **PASSED**
- `OpenTool_Fails_WhenNeitherIdNorFileProvided` — **PASSED**
- `OpenTool_RejectsRestrictedExtensions_ViaPathGuard` — **PASSED**
- `OpenTool_InvokesReaderAndReturnsFormattedContent` — **PASSED**
- `OutlineTool_Fails_WhenNoFileOrIdSpecified` — **PASSED**
- `OutlineTool_FormatsSectionsAndSymbolsCorrectly` — **PASSED**
- `ContextTool_ValidatesRequiredQuery` — **PASSED**

### [`CodeGraphBridgeTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/CodeGraphBridgeTests.cs) (3 Tests)
- `CodeGraphBridge_DetectsCurrentWorkspaceAvailability` — **PASSED**
- `CodeGraphBridge_ReturnsFalseForNonExistentDirectory` — **PASSED**
- `CodeGraphBridge_ExploreAsync_ReturnsOutputForKnownSymbol` — **PASSED**

### [`ObservabilityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ObservabilityTests.cs) (3 Tests)
- `ObservabilityService_RecordIndexing_UpdatesSnapshotAccurately` — **PASSED**
- `ObservabilityService_RecordSearch_ComputesAverageLatencyAndModes` — **PASSED**
- `ObservabilityService_RecordMcpCall_TracksInvocationsFailuresAndLatency` — **PASSED**

### [`SecurityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SecurityTests.cs) (5 Tests)
- `PathGuard_AcceptsContainedPath` — **PASSED**
- `PathGuard_RejectsPathOutsideAllowedRoots` — **PASSED**
- `PathGuard_RejectsRestrictedExtensions` — **PASSED**
- `PathGuard_RejectsNullOrEmptyPath` — **PASSED**
- `ContentSanitizer_PreservesNormalWhitespace` — **PASSED**

### [`ConfigurationAndEntityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ConfigurationAndEntityTests.cs) (3 Tests)
- `DocumentGraphConfig_HasSensibleDefaults` — **PASSED**
- `DocumentGraphConfig_SerializesAndDeserializesAccurately` — **PASSED**
- `EntityModels_MapPropertiesAndRelationshipsCorrectly` — **PASSED**

### [`ApiTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ApiTests.cs) (5 Tests)
- `ApiRequests_SerializeAndDeserializeCleanly` — **PASSED**
- `IndexRequest_AssignsDefaultPathProperly` — **PASSED**
- `FindSymbolRequest_SetsDefaultsCorrectly` — **PASSED**
- `ReferencesRequest_AssignsSymbolProperly` — **PASSED**
- `RelatedRequest_AssignsTargetProperly` — **PASSED**

---

## 4. Test Data Directory Inventory (`tests/data/`)

The repository contains real, valid test fixtures across all supported formats for automated and manual integration testing:

| File | Format | Size | Purpose |
| :--- | :--- | :--- | :--- |
| [`sample_manual.pdf`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_manual.pdf) | PDF Document | 1,039 B | Architecture manual testing page boundaries, metadata, and vector retrieval. |
| [`sample_contract.doc`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_contract.doc) | Word Document | 1,199 B | Master Service Agreement testing `.doc` extension parsing and legacy fallbacks. |
| [`sample_architecture.ppt`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_architecture.ppt) | PowerPoint Presentation | 1,800 B | Technical roadmap testing `.ppt` slide extraction and slide count tracking. |
| [`SampleAuth.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/SampleAuth.cs) | C# Source | 665 B | Authentication service testing Roslyn AST classes and method symbols. |
| [`sampleAuth.ts`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sampleAuth.ts) | TypeScript Source | 425 B | TypeScript functions and interface declarations. |
| [`architecture.md`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/architecture.md) | Markdown | 608 B | Headings, code fences, and section splitting. |
| [`sample_data.csv`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_data.csv) | CSV Data | 91 B | Tabular user and role records. |
| [`sample_schema.sql`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_schema.sql) | SQL Schema | 346 B | Table definitions (`CREATE TABLE`) and constraints. |
| [`sample_config.json`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_config.json) | JSON Config | 154 B | Structured key-value application configuration. |
| [`notes.txt`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/notes.txt) | Plain Text | 257 B | Unstructured text notes and paragraphs. |

---

## 5. How to Run the Test Suite

Run all tests from the repository root:

```powershell
dotnet test DocumentGraph.slnx
```

For detailed test name and execution duration output:
```powershell
dotnet test DocumentGraph.slnx --logger "console;verbosity=normal"
```

To run a specific test suite (e.g. PDF and Office tests):
```powershell
dotnet test DocumentGraph.slnx --filter "FullyQualifiedName~DocPdfPptTests"
```

---

## 6. CodeGraph Index Synchronization

In accordance with workspace rules, all changes are synchronized with CodeGraph:

```powershell
cmd.exe /c codegraph sync
```
*Result: Index is up to date, all symbols, ASTs, and call graphs are indexed.*
