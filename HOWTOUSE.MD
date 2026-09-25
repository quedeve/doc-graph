# DocumentGraph — Complete User Manual & Technical Documentation

Comprehensive documentation for **DocumentGraph**: the local document indexing, search, slicing, and knowledge graph engine. This manual covers all CLI commands, configuration options, indexing lifecycles, search algorithms, AI Agent (MCP) integration, REST APIs, and troubleshooting.

---

## Table of Contents
1. [System Overview & Architecture](#1-system-overview--architecture)
2. [Prerequisites & Binary Locations](#2-prerequisites--binary-locations)
3. [Configuration Reference (`docgraph.json`)](#3-configuration-reference-docgraphjson)
4. [The Mandatory End-to-End Workflow](#4-the-mandatory-end-to-end-workflow)
5. [Complete CLI Command Reference](#5-complete-cli-command-reference)
   - [`init`](#51-docgraph-init)
   - [`index`](#52-docgraph-index)
   - [`embed`](#53-docgraph-embed)
   - [`build-relationships`](#54-docgraph-build-relationships)
   - [`status`](#55-docgraph-status)
   - [`search`](#56-docgraph-search)
   - [`semantic-search`](#57-docgraph-semantic-search)
   - [`read`](#58-docgraph-read)
   - [`outline`](#59-docgraph-outline)
   - [`related`](#510-docgraph-related)
   - [`references`](#511-docgraph-references)
   - [`watch`](#512-docgraph-watch)
   - [`reindex`](#513-docgraph-reindex)
   - [`codegraph-sync`](#514-docgraph-codegraph-sync)
6. [Supported File Formats & Extracted Features](#6-supported-file-formats--extracted-features)
7. [AI Agent Integration (MCP Server)](#7-ai-agent-integration-mcp-server)
8. [REST API Reference](#8-rest-api-reference)
9. [Troubleshooting & Frequently Asked Questions](#9-troubleshooting--frequently-asked-questions)

---

## 1. System Overview & Architecture

**DocumentGraph** bridges the gap between structured source code and unstructured documentation. It operates as a local service connecting two complementary interfaces over a local PostgreSQL + pgvector database:

```text
+---------------------------------------------------------------------------------------+
|                                    Clients / AI Agents                                |
|                 (Claude Desktop, Cursor, Antigravity, VS Code, PowerShell)            |
+---------------------------------------------------------------------------------------+
                           |                                        |
               JSON-RPC 2.0 (stdio)                           CLI / REST API
                           v                                        v
+------------------------------------+    +---------------------------------------------+
|    DocumentGraph.Mcp (.exe)        |    |         DocumentGraph.Tool (.exe)           |
| - search, open, outline, find      |    | - Scanner & multi-format parsers            |
| - references, related, context     |    | - Token chunking & embedding pipeline       |
| - Budgeted token retrieval         |    | - Hybrid search (RRF FTS + Vector)          |
| - Isolated stderr logging          |    | - Knowledge graph edge builder              |
+------------------------------------+    | - Live file system watcher                  |
                  |                       +---------------------------------------------+
                  +-------------------+--------------------+
                                      |
                                      v
+---------------------------------------------------------------------------------------+
|                               Storage Substrate                                       |
| - PostgreSQL 15+ (Documents, Sections, Chunks, Symbols, Relationships, Tags)           |
| - pgvector (Cosine distance index with HNSW / IVFFlat vector acceleration)            |
| - Local Embedding Provider (Ollama / OpenAI-compatible / vLLM / LocalAI)              |
+---------------------------------------------------------------------------------------+
```

---

## 2. Prerequisites & Binary Locations

### 2.1 System Requirements
- **OS**: Windows 10/11, Windows Server, macOS, or Linux.
- **Runtime**: .NET 10.0 runtime (or self-contained binary).
- **Database**: PostgreSQL 15+ with the `pgvector` extension enabled (`CREATE EXTENSION IF NOT EXISTS vector;`).
- **Embedding Provider**: Local [Ollama](https://ollama.ai) (`ollama pull nomic-embed-text`) or any OpenAI-compatible endpoint.

### 2.2 Precompiled Binaries
The solution includes release-compiled executables in the `dist/` directory:
- **CLI Tool**: [`dist/tool/DocumentGraph.Tool.exe`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/tool/DocumentGraph.Tool.exe)
- **MCP Server**: [`dist/mcp/DocumentGraph.Mcp.exe`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/mcp/DocumentGraph.Mcp.exe)

#### Setting Up a Terminal Alias (PowerShell)
```powershell
Set-Alias docgraph C:\Users\Victor\Documents\GIT\outside\SemanticSearch\dist\tool\DocumentGraph.Tool.exe
```
*(Or add `C:\Users\Victor\Documents\GIT\outside\SemanticSearch\dist\tool` to your system `PATH`)*.

---

## 3. Configuration Reference (`docgraph.json`)

Running `docgraph init` creates `docgraph.json` in your repository root. You can adjust all operational parameters:

```json
{
  "database": {
    "host": "localhost",
    "port": 5432,
    "database": "documentgraph",
    "username": "postgres",
    "password": "your_secure_password",
    "sslMode": "Prefer",
    "maxPoolSize": 20
  },
  "embedding": {
    "provider": "Ollama",
    "model": "nomic-embed-text",
    "endpoint": "http://localhost:11434",
    "dimension": 768,
    "apiKey": ""
  },
  "chunking": {
    "maxTokens": 512,
    "overlapTokens": 64,
    "strategy": "Paragraph"
  },
  "search": {
    "defaultLimit": 10,
    "hybridRrfK": 60,
    "ftsWeight": 1.0,
    "vectorWeight": 1.0
  },
  "indexing": {
    "includeExtensions": [],
    "ignore": [
      "bin",
      "obj",
      ".git",
      ".vs",
      "node_modules",
      "dist",
      ".idea"
    ],
    "maxFileSizeMb": 50,
    "followSymlinks": false
  }
}
```

### Switching to an OpenAI or Cloud Provider
To use OpenAI or vLLM instead of Ollama, update the `embedding` section:
```json
"embedding": {
  "provider": "OpenAI",
  "model": "text-embedding-3-small",
  "endpoint": "https://api.openai.com/v1",
  "dimension": 1536,
  "apiKey": "sk-your-openai-api-key"
}
```

---

## 4. The Mandatory End-to-End Workflow

> [!IMPORTANT]
> **You MUST index your documents before running search commands.**  
> DocumentGraph searches indexed chunks stored in PostgreSQL. If you search an empty database without indexing first, it will return `No results found.`

### Step-by-Step Initialization

```powershell
# Step 1: Initialize local configuration
docgraph init

# Step 2: Index all documents, presentations, spreadsheets, and source code
docgraph index .

# Step 3: Compute vector embeddings for all newly created chunks
docgraph embed

# Step 4: Extract and build the Knowledge Graph relationships
docgraph build-relationships

# Step 5: Verify that the database is populated
docgraph status
```

---

## 5. Complete CLI Command Reference

### 5.1 `docgraph init`
Generates a default `docgraph.json` configuration file in the current working directory.
```powershell
docgraph init
```

---

### 5.2 `docgraph index`
Scans a directory, parses files into semantic sections and symbols, computes SHA-256 hashes, and upserts them into PostgreSQL.
```powershell
docgraph index [path]
```
- `[path]` *(optional, default: `.`)*: Directory to scan and index.
- **Incremental Skipping**: Files whose SHA-256 hash matches the database are skipped automatically.

```powershell
# Examples:
docgraph index .
docgraph index C:\Projects\MyRepo
docgraph index docs\architecture
```

---

### 5.3 `docgraph embed`
Generates vector embeddings for any un-embedded chunks using the configured embedding provider.
```powershell
docgraph embed [--batch-size <n>]
```
- `--batch-size <n>` *(optional, default: `32`)*: Batch size per request.

```powershell
# Examples:
docgraph embed
docgraph embed --batch-size 64
```

---

### 5.4 `docgraph build-relationships`
Analyzes all indexed documents to build deterministic and reference graph relationships:
- **`CONTAINS`**: Document contains Section; Section contains Chunk; Class contains Method.
- **`REFERENCES` / `MENTIONS`**: Chunks or documents mentioning known code symbols or document filenames.
```powershell
docgraph build-relationships
```

---

### 5.5 `docgraph status`
Displays database statistics, document counts, chunk counts, embedded counts, and relationship metrics.
```powershell
docgraph status
```
*Sample Output:*
```
Index Statistics:
  Total Documents:     45
  Total Sections:      132
  Total Chunks:        410
  Embedded Chunks:     410 (100%)
  Relationships:       1,420
  Symbols:             268
```

---

### 5.6 `docgraph search`
The primary search command. Combines PostgreSQL Full-Text Search (BM25 lexical ranking) with pgvector cosine similarity via Reciprocal Rank Fusion (RRF).

```powershell
docgraph search "<query>" [options]
```

#### Arguments
- `<query>` *(string, required)*: The search phrase or question.

#### Options & Flags
| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `--mode <mode>` | `string` | `hybrid` | Search algorithm: `hybrid` (recommended), `text` (or `fts`), `semantic`. |
| `--limit <n>` | `int` | `10` | Maximum results to return. |
| `--type <type>` | `string` | *(all)* | Filter by category: `document`, `code`, `data`, `text`. |
| `--ext <ext>` | `string` | *(all)* | Filter by extension (e.g. `.pdf`, `.docx`, `.doc`, `.xlsx`, `.pptx`, `.ppt`, `.cs`, `.sql`). |
| `--path <prefix>` | `string` | *(all)* | Filter by folder path prefix (e.g. `src/Auth` or `docs/specs`). |

#### Search Recipes
```powershell
# 1. Hybrid search across all documents and code
docgraph search "OAuth2 token expiration and refresh flow"

# 2. Pure Full-Text (keyword) search for exact symbol or error name
docgraph search "InvalidTokenSignatureException" --mode text

# 3. Pure Semantic vector search for conceptual answers
docgraph search "How does the system handle database disaster recovery?" --mode semantic

# 4. Search exclusively within PDF specifications
docgraph search "Service Level Agreement response times" --ext .pdf

# 5. Search Word documents (.docx, .doc)
docgraph search "Architecture Overview" --ext .docx --limit 5

# 6. Search PowerPoint slide decks (.pptx, .ppt)
docgraph search "Cloud deployment strategy" --ext .pptx

# 7. Search only source code files
docgraph search "ValidateJwtToken" --type code

# 8. Search within a specific folder
docgraph search "connection timeout" --path src/DocumentGraph.Storage
```

---

### 5.7 `docgraph semantic-search`
Convenience command to perform direct vector cosine similarity search without lexical FTS blending.
```powershell
docgraph semantic-search "<query>" [--limit <n>] [--type <type>] [--ext <ext>] [--path <prefix>]
```

```powershell
# Example:
docgraph semantic-search "How are passwords encrypted before persistence?"
```

---

### 5.8 `docgraph read`
Performs surgical structured reading into an indexed document without loading the entire file into memory or context.

```powershell
docgraph read <file> [options]
```

#### Options & Flags
| Option | Type | Description |
| :--- | :--- | :--- |
| `--page <n>` | `int` | Read a specific page of a PDF document (1-indexed). |
| `--slide <n>` | `int` | Read a specific slide from a PowerPoint presentation (`.pptx`, `.ppt`). |
| `--sheet <name>` | `string` | Read a specific sheet from an Excel spreadsheet (`.xlsx`, `.xls`). |
| `--section <title>` | `string` | Read a specific section or heading in a Word or Markdown document. |
| `--symbol <name>` | `string` | Extract the declaration and body of a code symbol (class, method, interface). |
| `--start <n>` | `int` | Starting line number (1-indexed). |
| `--end <n>` | `int` | Ending line number (inclusive). |

#### Examples
```powershell
# 1. Read Page 2 of a PDF specification
docgraph read docs/SecuritySpec.pdf --page 2

# 2. Read Slide 4 of an architecture presentation
docgraph read slides/Q3Review.pptx --slide 4

# 3. Read the 'Users' sheet from an Excel spreadsheet
docgraph read data/DatabaseSchema.xlsx --sheet Users

# 4. Read the "Authentication" section from a Word document
docgraph read docs/Architecture.docx --section "Authentication"

# 5. Extract a specific C# method
docgraph read src/Services/TokenService.cs --symbol ValidateToken

# 6. Read a line range (lines 45 to 80)
docgraph read src/Program.cs --start 45 --end 80
```

---

### 5.9 `docgraph outline`
Displays the structural table of contents, sections, slide numbers, spreadsheet sheets, and code symbols for any indexed file.
```powershell
docgraph outline <file>
```

```powershell
# Examples:
docgraph outline docs/SystemArchitecture.pdf
docgraph outline src/Controllers/OrderController.cs
docgraph outline presentations/TechnicalRoadmap.pptx
docgraph outline data/FinancialReport.xlsx
```

---

### 5.10 `docgraph related`
Queries the Knowledge Graph to reveal all connected documents, sections, and symbols in an entity's neighborhood. Automatically bridges with **CodeGraph** when dynamic call graph hops are available.
```powershell
docgraph related <target>
```
- `<target>`: Document name, file path, or symbol name.

```powershell
# Examples:
docgraph related IUserRepository
docgraph related Architecture.pdf
docgraph related OrderController.cs
```

---

### 5.11 `docgraph references`
Finds everywhere a code symbol is referenced or discussed across all non-code documentation (PDFs, Word docs, spreadsheets, slides, schemas) and source code.
```powershell
docgraph references <symbol>
```

```powershell
# Examples:
docgraph references GenerateToken
docgraph references AppDbContext
docgraph references OrderStatus
```

---

### 5.12 `docgraph watch`
Runs a live background file watcher with 500ms debouncing. Automatically detects file creates, edits, renames, and deletions and incrementally reindexes them in PostgreSQL.
```powershell
docgraph watch [path]
```

```powershell
# Example:
docgraph watch .
```

---

### 5.13 `docgraph reindex`
Forces an immediate re-index of a single specific file, regardless of whether its SHA-256 hash changed.
```powershell
docgraph reindex <file>
```

```powershell
# Example:
docgraph reindex docs/Architecture.docx
```

---

### 5.14 `docgraph codegraph-sync`
Synchronizes the workspace's CodeGraph index (`.codegraph`), updating code symbols and dynamic dispatch call graphs.
```powershell
docgraph codegraph-sync
```

---

## 6. Supported File Formats & Extracted Features

DocumentGraph handles 14 distinct file formats across code, documents, presentations, spreadsheets, and tabular data:

| Category | Extension | Parser Component | Extracted Features & Semantic Structure |
| :--- | :--- | :--- | :--- |
| **Documents** | `.pdf` | [`PdfParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/PdfParser.cs) | Page-by-page text extraction, page numbers, title, author, metadata. |
| | `.docx` | [`DocxParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/DocxParser.cs) | Heading hierarchy (`Heading 1-6`), paragraphs, formatted table rows. |
| | `.doc` | [`DocxParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/DocxParser.cs) + [`LegacyOfficeExtractor`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/LegacyOfficeExtractor.cs) | OpenXML parsing with UTF-16LE / ASCII text stream recovery for legacy Word 97–2004 binary files. |
| | `.md` | [`MarkdownParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Text/MarkdownParser.cs) | Markdown AST heading hierarchy (`#`, `##`), fenced code blocks, frontmatter. |
| | `.txt` | [`TextParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Text/TextParser.cs) | Paragraph and newline boundary chunking, line number tracking. |
| **Presentations** | `.pptx` | [`PptxParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/PptxParser.cs) | Slide titles, slide body text, speaker notes, graphic frames, table cells. |
| | `.ppt` | [`PptxParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/PptxParser.cs) + [`LegacyOfficeExtractor`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/LegacyOfficeExtractor.cs) | OpenXML parsing with binary stream slide chunking for legacy PowerPoint 97–2003 files. |
| **Spreadsheets** | `.xlsx` | [`XlsxParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/XlsxParser.cs) | Multi-sheet parsing, sheet names, tabular row/column data, shared string lookup. |
| | `.xls` | [`XlsxParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/XlsxParser.cs) + [`LegacyOfficeExtractor`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/LegacyOfficeExtractor.cs) | OpenXML parsing with binary text extraction fallback for legacy Excel 97–2003 workbooks. |
| **Source Code** | `.cs` | [`CSharpParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Code/CSharpParser.cs) | Roslyn AST: classes, structs, interfaces, methods, signatures, doc comments, line spans. |
| | `.ts`, `.js` | [`TypeScriptJsParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Code/TypeScriptJsParser.cs) | Functions, classes, export signatures, JSDoc comments. |
| | `.cshtml` | [`CshtmlParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Code/CshtmlParser.cs) | Razor `@model` symbol extraction, `@page` route extraction, HTML structure. |
| | `.sql` | [`SqlParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Code/SqlParser.cs) | `CREATE TABLE` schema definitions, column types, primary/foreign keys, stored procedures. |
| **Data** | `.json`, `.xml` | [`JsonXmlParsers`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Data/JsonXmlParsers.cs) | Structured key-value pairs, nested node hierarchies, attribute parsing. |
| | `.csv` | [`CsvParser`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Data/CsvParser.cs) | Column headers, typed row records, pipe-delimited text representations. |

---

## 7. AI Agent Integration (MCP Server)

DocumentGraph runs as an MCP (Model Context Protocol) server over standard input/output (`stdio`), completely isolating logs to `stderr` so JSON-RPC communication is never corrupted.

### 7.1 Client Configuration Snippets

#### Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "C:\\Users\\Victor\\Documents\\GIT\\outside\\SemanticSearch\\dist\\mcp\\DocumentGraph.Mcp.exe",
      "args": []
    }
  }
}
```

#### Cursor (`cursor_mcp.json`)
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "C:\\Users\\Victor\\Documents\\GIT\\outside\\SemanticSearch\\dist\\mcp\\DocumentGraph.Mcp.exe",
      "args": []
    }
  }
}
```

#### Antigravity / Gemini CLI (`antigravity_gemini_config.json`)
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "C:\\Users\\Victor\\Documents\\GIT\\outside\\SemanticSearch\\dist\\mcp\\DocumentGraph.Mcp.exe",
      "args": [],
      "transport": "stdio"
    }
  }
}
```

### 7.2 Available MCP Tools

| Tool Name | Parameters | Purpose |
| :--- | :--- | :--- |
| **`search`** | `query` *(req)*, `limit`, `mode`, `documentType`, `extension` | Search documentation, specs, presentations, and code using hybrid RRF. |
| **`open`** | `path` *(req)*, `page`, `slide`, `sheet`, `section`, `symbol`, `start_line`, `end_line` | Surgically read a specific document slice without loading whole files. |
| **`outline`** | `path` *(req)* | Retrieve structural table of contents, sections, slide list, sheets, or code symbols. |
| **`find`** | `name` *(req)*, `extension`, `documentType`, `limit` | Locate files by name or type in the index. |
| **`references`** | `symbol` *(req)* | Find everywhere a code symbol is referenced or discussed across all documentation. |
| **`related`** | `target` *(req)*, `includeCodeGraph` | Inspect incoming and outgoing graph edges around an entity. |
| **`context`** | `query` *(req)*, `budgetTokens`, `includeRelationships` | Synthesizes an optimal prompt context block combining relevant chunks and graph edges within a token budget. |

---

## 8. REST API Reference

DocumentGraph includes a lightweight, high-performance REST API.

### Starting the API Server
```powershell
dotnet run --project src/DocumentGraph.Api
# Listens on http://localhost:5000
```

### 8.1 Search (`POST /api/search`)
```bash
curl -X POST http://localhost:5000/api/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "authentication flow",
    "limit": 5,
    "mode": "hybrid",
    "extension": ".pdf"
  }'
```

### 8.2 Open / Read Slice (`POST /api/open`)
```bash
curl -X POST http://localhost:5000/api/open \
  -H "Content-Type: application/json" \
  -d '{
    "path": "docs/architecture.pdf",
    "page": 2
  }'
```

### 8.3 Document Outline (`GET /api/outline`)
```bash
curl "http://localhost:5000/api/outline?file=docs/architecture.pdf"
```

### 8.4 Related Graph Neighborhood (`GET /api/related`)
```bash
curl "http://localhost:5000/api/related?target=IUserRepository"
```

### 8.5 Symbol References (`GET /api/references`)
```bash
curl "http://localhost:5000/api/references?symbol=ValidateToken"
```

### 8.6 System Status (`GET /api/status`)
```bash
curl http://localhost:5000/api/status
```

---

## 9. Troubleshooting & Frequently Asked Questions

### Q1: `docgraph search` returns "No results found"
- **Cause**: The workspace has not been indexed yet, or vector embeddings have not been generated.
- **Solution**: Run the mandatory setup steps:
  ```powershell
  docgraph index .
  docgraph embed
  docgraph build-relationships
  ```

### Q2: PostgreSQL Connection Error: "Failed to connect to 127.0.0.1:5432"
- **Cause**: PostgreSQL service is stopped or credentials in `docgraph.json` are incorrect.
- **Solution**:
  1. Verify PostgreSQL is running: `Get-Service postgresql*` (on Windows) or `sudo systemctl status postgresql` (on Linux).
  2. Verify credentials in `docgraph.json`.
  3. Ensure the `vector` extension is enabled:
     ```sql
     CREATE DATABASE documentgraph;
     \c documentgraph;
     CREATE EXTENSION IF NOT EXISTS vector;
     ```

### Q3: Ollama Connection Error: "Connection refused (localhost:11434)"
- **Cause**: Ollama daemon is not running or the model is missing.
- **Solution**:
  1. Start Ollama: `ollama serve`
  2. Ensure the model is downloaded: `ollama pull nomic-embed-text`
  3. Test embedding generation: `curl http://localhost:11434/api/embeddings -d '{"model":"nomic-embed-text","prompt":"test"}'`

### Q4: How are legacy `.doc`, `.ppt`, and `.xls` files handled?
- **Answer**: DocumentGraph attempts OpenXML parsing first (supporting modern XML files that use legacy extensions). If binary format is detected, DocumentGraph automatically invokes [`LegacyOfficeExtractor`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/src/DocumentGraph.Parsers/Documents/LegacyOfficeExtractor.cs) to safely extract UTF-16LE and ASCII text streams, tag the file with `legacy_format: true`, and index all readable sections.

### Q5: How do I re-index after pulling Git updates?
- Simply run:
  ```powershell
  docgraph index .
  docgraph embed
  docgraph build-relationships
  ```
  Unchanged files are skipped instantly using SHA-256 hash caching.
