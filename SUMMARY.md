# DocumentGraph — Architecture & System Summary

## 1. Executive Summary

**DocumentGraph** is a high-performance local document indexing, search, and knowledge graph system designed to make private engineering documentation as navigable to AI agents as source code is in **CodeGraph**.

It solves a fundamental problem:
> **"Engineering knowledge does not live exclusively in code. It lives in design docs (.docx), architectural specifications (.pdf), spreadsheets (.xlsx), presentations (.pptx), schemas (.sql), notes (.md), and source code (.cs, .ts)."**

DocumentGraph indexes, relates, and serves this entire heterogeneous corpus locally without requiring cloud AI dependencies.

---

## 2. Core Architectural Separation

DocumentGraph strictly separates into two distinct products operating over a shared local PostgreSQL + pgvector substrate:

```text
+-------------------------------------------------------------------+
|                        AI Agent / Client                          |
|             (Claude Desktop, Cursor, Antigravity, VS Code)        |
+-------------------------------------------------------------------+
                                  |
                        JSON-RPC 2.0 (stdio)
                                  v
+-------------------------------------------------------------------+
|               Product 2: DocumentGraph MCP Server                 |
|  - Small set of composable tools (search, open, outline, context) |
|  - Token budgeting & output caps (no whole-file dumps)            |
|  - Intelligent exploration workflows                              |
|  - Stderr isolation (zero stdout pollution)                       |
+-------------------------------------------------------------------+
                                  |
                                  v
+-------------------------------------------------------------------+
|               Product 1: DocumentGraph Core & Tool                |
|  - CLI administrative & search interface                          |
|  - Extensible file parsers (PDF, Office OpenXML, Roslyn AST)      |
|  - Content-aware chunkers (paragraph, heading, code symbol)       |
|  - Search Engine: FTS + pgvector Cosine + RRF Hybrid Ranking      |
|  - Slicing Document Reader (page, slide, sheet, symbol, lines)    |
|  - Knowledge Graph (structural & cross-reference relationships)   |
|  - Security & Path Traversal Guards (PathGuard, Sanitizer)        |
|  - Continuous directory watcher & incremental indexing            |
+-------------------------------------------------------------------+
                                  |
                                  v
+-------------------------------------------------------------------+
|                       Storage Substrate                           |
|  - PostgreSQL 15+ relational schema                               |
|  - pgvector vector similarity index                               |
|  - Local Ollama / OpenAI-compatible embedding providers           |
+-------------------------------------------------------------------+
```

---

## 3. Key Subsystems & Features

### 3.1 Document & Code Parsing Engine
- **Office Formats**: Direct OpenXML DOM parsing for `.docx` (Word headings/paragraphs), `.xlsx` (Excel worksheets/cells), and `.pptx` (PowerPoint slides).
- **PDF**: Accurate layout and page extraction via `PdfPig`.
- **Source Code**: Roslyn C# syntax tree symbol extraction (namespaces, classes, interfaces, methods, properties), TypeScript/JavaScript parsing, SQL DDL tables/procedures, and CSHTML.
- **Data & Text**: Markdown heading hierarchy, CSV rows/columns, JSON, XML, TXT.

### 3.2 Dual Chunking Strategies
- **`DefaultChunker`**: Token-sized chunking honoring paragraph boundaries and sentence punctuation with sliding window overlap.
- **`CodeChunker`**: Symbol-aware chunking preserving syntax declarations and method boundaries.

### 3.3 Search Engine & Hybrid Ranking
- **Full-Text Search (FTS)**: PostgreSQL `websearch_to_tsquery('english', ...)` with `ts_rank_cd` and `ts_headline` snippet extraction.
- **Semantic Vector Search**: pgvector cosine distance (`<=>`) using pluggable embeddings (Ollama / OpenAI).
- **Hybrid Search**: Reciprocal Rank Fusion (RRF $k=60$) combining keyword precision with semantic discovery.

### 3.4 Slicing Document Reader (`DocumentReader`)
Prevents token exhaustion by allowing AI agents and CLI users to slice exact targets:
- By PDF page number
- By Excel worksheet name
- By PowerPoint slide number
- By code symbol name
- By line ranges

### 3.5 Knowledge Graph Engine (`RelationshipService`)
Maps relationships between entities across formats:
- **`CONTAINS`**: Document contains Section; Section contains Chunk; Class contains Method.
- **`DEFINED_IN`**: Symbol defined in Document.
- **`REFERENCES` / `MENTIONS`**: Chunks or documents mentioning symbols declared elsewhere.
- **`CALLS`**: Direct method calls extracted via CodeGraph bridge.

### 3.6 Unified Knowledge Graph (`CodeGraphBridge`)
Bridges DocumentGraph with CodeGraph:
- Links specification documents (e.g. `Architecture.pdf`) directly to active code ASTs and dynamic call graphs.
- Unified neighborhood exploration across documentation and codebase.

### 3.7 Security & Robustness
- **`PathGuard`**: Canonical path validation against directory traversal and blocked file extensions (`.env`, `.key`, `.pem`, `.exe`).
- **`ContentSanitizer`**: Strips null bytes (`\0`) and harmful control characters before database persistence.

### 3.8 Observability & Telemetry
- **`IObservabilityService`**: Real-time tracking of files scanned, indexed, skipped, failed, search latencies, and per-tool MCP invocation counts and failure rates.
- Accessible via `docgraph status` and `GET /api/status`.

---

## 4. Documentation Index

- [`INSTALL.md`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/INSTALL.md) — Prerequisites, database setup, and configuration.
- [`PUBLISH.md`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/PUBLISH.md) — Compilation, release packaging, and self-contained builds.
- [`RUN.md`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/RUN.md) — CLI commands, MCP client integration, and REST API usage.
- [`HOWTOUSE.MD`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/HOWTOUSE.MD) — Complete search command reference, query options, filters, and usage recipes.
- [`TEST_SUMMARY.MD`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/TEST_SUMMARY.MD) — Complete test suite execution results, feature coverage matrix, and logs.
- [`mcp_configs/`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/mcp_configs/) — Drop-in MCP client configurations for Claude Desktop, Cursor, and Antigravity.
