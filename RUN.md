# DocumentGraph — Run & Usage Guide

This guide explains how to run and use **DocumentGraph Tool**, the **DocumentGraph MCP Server**, and the **REST API**.

---

## 1. Running the CLI Tool (`docgraph`)

The CLI binary is located at [`dist/tool/DocumentGraph.Tool.exe`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/tool/DocumentGraph.Tool.exe).

### 1.1 Index a Directory
Scans and parses all supported document, code, and data files, chunks them, and stores them in PostgreSQL:
```bash
docgraph index ./documents
```
*Supports `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.txt`, `.md`, `.csv`, `.json`, `.xml`, `.cs`, `.ts`, `.js`, `.sql`, `.cshtml`.*

### 1.2 Continuous Watch Mode
Continuously monitors a folder for file additions, modifications, and deletions, updating the index in real time:
```bash
docgraph watch ./documents
```

### 1.3 Search the Knowledge Base
Search using PostgreSQL Full-Text Search, pgvector Semantic Search, or Hybrid Search:
```bash
# Hybrid Search (Default - Reciprocal Rank Fusion)
docgraph search "MFA authentication challenge"

# Full-Text Search (Exact token matching)
docgraph search "MFA authentication" --mode text

# Semantic Search (Natural language meaning)
docgraph semantic-search "How is user session timeout configured?"

# Filter by type or file extension
docgraph search "authentication" --type document --ext .pdf
```

### 1.4 Structured Document Reader
Read targeted sections, pages, sheets, or code symbols without dumping full files:
```bash
# Read a specific page from a PDF
docgraph read Architecture.pdf --page 14

# Read a specific sheet from an Excel workbook
docgraph read Requirements.xlsx --sheet Authentication

# Read a specific code symbol and its body
docgraph read AuthenticationService.cs --symbol ValidateTokenAsync

# Read a line range
docgraph read notes.txt --start 20 --end 45
```

### 1.5 Document Outline
Inspect hierarchical sections and extracted code symbols:
```bash
docgraph outline Architecture.pdf
docgraph outline AuthenticationService.cs
```

### 1.6 Knowledge Graph Queries
Traverse structural hierarchy and cross-document references:
```bash
# Find connected documents and symbols
docgraph related AuthenticationService

# Find references and mentions across all indexed documents
docgraph references ValidateTokenAsync

# Extract and build all structural relationships
docgraph build-relationships
```

### 1.7 Check Status & Telemetry
Display the real-time observability dashboard:
```bash
docgraph status
```

### 1.8 CodeGraph Synchronization
Sync CodeGraph AST index for the workspace:
```bash
docgraph codegraph-sync
```

---

## 2. Running the MCP Server (`DocumentGraph.Mcp`)

The MCP Server binary is located at [`dist/mcp/DocumentGraph.Mcp.exe`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/mcp/DocumentGraph.Mcp.exe).

It communicates over standard I/O (`stdin`/`stdout`) using the **JSON-RPC 2.0 Model Context Protocol**.

### 2.1 Integrating with AI Clients

#### A. Cursor IDE
Add to your project's `.cursor/mcp.json` or global Cursor settings:
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/mcp/DocumentGraph.Mcp.exe",
      "args": [],
      "env": {
        "DOCGRAPH_CONFIG": "c:/Users/Victor/Documents/GIT/outside/SemanticSearch/docgraph.json"
      }
    }
  }
}
```

#### B. Claude Desktop
Add to `%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/mcp/DocumentGraph.Mcp.exe",
      "args": [],
      "env": {
        "DOCGRAPH_CONFIG": "c:/Users/Victor/Documents/GIT/outside/SemanticSearch/docgraph.json"
      }
    }
  }
}
```

#### C. Google Antigravity / Gemini Code Assist
Add to `.gemini/config/mcp_config.json`:
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/mcp/DocumentGraph.Mcp.exe",
      "args": [],
      "env": {
        "DOCGRAPH_CONFIG": "c:/Users/Victor/Documents/GIT/outside/SemanticSearch/docgraph.json"
      }
    }
  }
}
```

### 2.2 Tools Available to AI Agents

| Tool | Purpose | Key Parameters |
| :--- | :--- | :--- |
| `search` | Find candidate chunks via hybrid/fts/semantic search. | `query`, `mode`, `limit`, `extension` |
| `open` | Read document content with slice/page/symbol targeting. | `path`, `page`, `slide`, `sheet`, `symbol`, `start_line`, `end_line` |
| `outline` | Retrieve document outline, sections, and symbols. | `path` |
| `find` | Look up code symbols by name and type. | `name`, `symbol_type`, `limit` |
| `references`| Find mentions and references to a symbol. | `symbol` |
| `related` | Discover incoming and outgoing knowledge graph connections. | `target` |
| `context` | High-level context assembler (token-budget capped). | `query`, `max_tokens`, `mode`, `include_relationships` |

---

## 3. Running the REST API (`DocumentGraph.Api`)

For web application integration or remote HTTP calls:
```bash
dotnet run --project src/DocumentGraph.Api/DocumentGraph.Api.csproj
```
Default URL: `http://localhost:5000`

### Example API Requests:
```bash
# Get Status & Observability Telemetry
curl http://localhost:5000/api/status

# Execute Hybrid Search
curl -X POST http://localhost:5000/api/search \
  -H "Content-Type: application/json" \
  -d '{"query": "authentication", "mode": "hybrid", "limit": 5}'

# Read Sliced Content
curl -X POST http://localhost:5000/api/read \
  -H "Content-Type: application/json" \
  -d '{"pathOrFilename": "Architecture.pdf", "page": 1}'
```
