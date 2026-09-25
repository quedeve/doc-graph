# DocumentGraph — Testing & Verification Summary

This document provides a comprehensive summary of all automated unit and integration tests across the **DocumentGraph** solution.

---

## 1. Test Suite Overview

- **Solution**: [`DocumentGraph.slnx`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/DocumentGraph.slnx)
- **Test Project**: [`tests/DocumentGraph.Tests`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/DocumentGraph.Tests.csproj)
- **Total Tests**: **82 passing**
- **Failures**: 0
- **Skipped**: 0
- **Duration**: ~750 ms
- **CodeGraph Status**: Synced and verified

### Running the Tests

To run the complete test suite from PowerShell / Terminal:

```powershell
dotnet test DocumentGraph.slnx
```

Expected output:
```
Test run for DocumentGraph.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 748 ms
```

---

## 2. Test File & Feature Matrix

| Test File | Project / Subsystem | Key Scenarios & Features Covered |
| :--- | :--- | :--- |
| [`DocPdfPptTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/DocPdfPptTests.cs) | `DocumentGraph.Parsers`<br>`DocumentGraph.Indexer` | • Multi-page PDF text & metadata extraction with [`PdfPig`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/PdfParser.cs)<br>• Word document parsing with `.doc` and `.docx` extensions<br>• Legacy binary `.doc` stream extraction via [`LegacyOfficeExtractor`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/LegacyOfficeExtractor.cs)<br>• PowerPoint presentations with `.ppt` and `.pptx` extensions<br>• Legacy binary `.ppt` slide extraction & chunking<br>• [`FileScanner`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Indexer/Scanner/FileScanner.cs) discovery of `.doc`, `.pdf`, `.ppt`<br>• End-to-end [`IndexingPipeline`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Indexer/Pipeline/IndexingPipeline.cs) processing and database upserts<br>• Fixture validation against permanent files in `tests/data/` |
| [`ParserTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ParserTests.cs) | `DocumentGraph.Parsers` | • C# Roslyn AST symbol extraction (classes, methods, signatures)<br>• TypeScript syntax tree parsing & function extraction<br>• Markdown heading hierarchy & section splitting<br>• CSV table parsing, headers, column metadata<br>• SQL DDL table creation & statement extraction<br>• OpenXML Word (`.docx`) document extraction<br>• ClosedXML multi-sheet Excel (`.xlsx`) extraction<br>• PDF page text & metadata extraction |
| [`ExtendedParserTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ExtendedParserTests.cs) | `DocumentGraph.Parsers` | • Plain Text (`.txt`) paragraph and line splitting<br>• JSON (`.json`) structured key-value parsing<br>• XML (`.xml`) element hierarchy & attribute extraction<br>• Razor/CSHTML (`.cshtml`) `@model` symbols and `@page` routes<br>• PowerPoint OpenXML (`.pptx`) slide deck parsing |
| [`ChunkerTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ChunkerTests.cs) | `DocumentGraph.Indexer` | • Sliding window token chunking with [`TokenChunker`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Indexer/Chunking/DefaultChunker.cs)<br>• Natural boundary splitting (headings, paragraphs, punctuation)<br>• Overlap window token retention across chunks |
| [`ScannerAndPipelineTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ScannerAndPipelineTests.cs) | `DocumentGraph.Indexer` | • Directory traversal and ignore patterns (`bin`, `obj`, `.git`, `.vs`)<br>• SHA-256 hash calculation & change detection<br>• Incremental indexing: skipping unchanged files<br>• Full document indexing & storage upsert |
| [`WatcherAndRelationshipTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/WatcherAndRelationshipTests.cs) | `DocumentGraph.Indexer`<br>`DocumentGraph.Search` | • [`DocumentWatcher`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Indexer/Watcher/DocumentWatcher.cs) initialization, debounce, and directory missing validation<br>• Directory watcher lifecycle (start, stop, dispose)<br>• Knowledge graph edge models (`RelationshipGraphNode`, `RelationshipGraphEdge`)<br>• Neighborhood center resolution (`RelatedItemsResult`)<br>• Mock relationship extraction interface behavior |
| [`EmbeddingProviderTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/EmbeddingProviderTests.cs) | `DocumentGraph.Indexer` | • [`OllamaEmbeddingProvider`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Indexer/Embeddings/OllamaEmbeddingProvider.cs) JSON payload creation & vector parsing<br>• [`OpenAiCompatibleEmbeddingProvider`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Indexer/Embeddings/OpenAiCompatibleEmbeddingProvider.cs) payload formatting<br>• Fallback logic and exception handling |
| [`SearchRankingTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SearchRankingTests.cs) | `DocumentGraph.Search` | • Reciprocal Rank Fusion (RRF) algorithm with `k=60` smoothing<br>• Merging PostgreSQL FTS lexical scores with pgvector semantic scores |
| [`SearchAndReaderTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SearchAndReaderTests.cs) | `DocumentGraph.Core`<br>`DocumentGraph.Search` | • [`SearchQuery`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Core/Interfaces/ISearchService.cs) defaults, limits, and filter prefixes<br>• [`SearchResults`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Core/Interfaces/ISearchService.cs) item scores & component breakdowns<br>• [`ReadContentRequest`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Core/Interfaces/IDocumentReader.cs) structured slice parameters (pages, sheets, symbols, line ranges)<br>• [`DocumentOutline`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Core/Interfaces/IDocumentReader.cs), `OutlineItem`, and `OutlineSymbol` structure |
| [`McpProtocolTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/McpProtocolTests.cs) | `DocumentGraph.Mcp` | • JSON-RPC 2.0 response formatting (success & error codes)<br>• Tool call result envelope (`McpToolCallResult`)<br>• Parameter validation for `search`, `find`, `references`, and `related` tools |
| [`McpServerAndToolsTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/McpServerAndToolsTests.cs) | `DocumentGraph.Mcp` | • [`OpenTool`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Mcp/Tools/OpenTool.cs) slice argument validation & execution<br>• [`OutlineTool`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Mcp/Tools/OutlineTool.cs) document outline extraction<br>• [`ContextTool`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Mcp/Tools/ContextTool.cs) search + relationship synthesis<br>• In-memory [`McpServer.ProcessRequestAsync`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Mcp/Server/McpServer.cs) dispatcher loop (`initialize`, `tools/list`, `tools/call`, `-32601` method not found) |
| [`CodeGraphBridgeTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/CodeGraphBridgeTests.cs) | `DocumentGraph.Core`<br>`DocumentGraph.Search` | • `.codegraph` directory detection at repository root<br>• CLI command invocation and graceful fallback when CodeGraph is unavailable<br>• Exploration output parsing & symbol link extraction |
| [`ObservabilityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ObservabilityTests.cs) | `DocumentGraph.Core` | • Total indexed files & chunks counters<br>• Search request latency tracking & histograms<br>• MCP tool invocation count and error counters |
| [`SecurityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/SecurityTests.cs) | `DocumentGraph.Core` | • Path traversal defense (`../`, absolute path escaping)<br>• SSRF protection blocking loopback (`127.0.0.1`, `localhost`) and private CIDR ranges<br>• Rate limiter token bucket refill and throttling behavior |
| [`ConfigurationAndEntityTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ConfigurationAndEntityTests.cs) | `DocumentGraph.Core` | • [`DocumentGraphConfig`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Core/Configuration/DocumentGraphConfig.cs) defaults & JSON serialization<br>• Database entities (`DocumentEntity`, `SectionEntity`, `ChunkEntity`, `RelationshipEntity`, `TagEntity`)<br>• Foreign key relations and tag links |
| [`ApiTests.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/DocumentGraph.Tests/ApiTests.cs) | `DocumentGraph.Api` | • REST API DTO models (`SearchApiRequest`, `OpenApiRequest`, `ReferencesApiRequest`, `RelatedApiRequest`, `IndexApiRequest`, `ConfigureApiRequest`)<br>• CamelCase JSON contract serialization |

---

## 3. Sample Data Fixtures (`tests/data/`)

The repository includes real sample files in all primary formats for manual and automated verification:

| File | Format | Description |
| :--- | :--- | :--- |
| [`sample_manual.pdf`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_manual.pdf) | PDF Document | Multi-page architecture manual with headings and vector search text |
| [`sample_contract.doc`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_contract.doc) | Word Document | Service level agreement with SLA terms and availability guarantees |
| [`sample_architecture.ppt`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_architecture.ppt) | PowerPoint Presentation | Slide deck with technical roadmap & architecture overview |
| [`SampleAuth.cs`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/SampleAuth.cs) | C# Source | Authentication service class with token validation methods |
| [`sampleAuth.ts`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sampleAuth.ts) | TypeScript Source | Token validation functions and interfaces |
| [`architecture.md`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/architecture.md) | Markdown | System design and section hierarchy |
| [`sample_data.csv`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_data.csv) | CSV Data | Tabular user and role records |
| [`sample_schema.sql`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_schema.sql) | SQL Schema | Table creation DDL statements |
| [`sample_config.json`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/sample_config.json) | JSON Config | Server configuration settings |
| [`notes.txt`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/tests/data/notes.txt) | Plain Text | Unstructured developer notes |

---

## 4. CodeGraph Verification

Every phase is synchronized and verified using CodeGraph:

```powershell
cmd.exe /c codegraph sync
cmd.exe /c codegraph explore "DocPdfPptTests"
```

All 82 tests pass reliably with zero warnings or errors.
