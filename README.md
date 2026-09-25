# DocumentGraph 🌐

> **Local Document Indexing, Hybrid Search, Structured Slicing, and Knowledge Graph Engine for AI Agents & Developers**

**DocumentGraph** makes private engineering documentation (.pdf, .docx, .doc, .pptx, .ppt, .xlsx, .xls, .md, .txt) and database schemas (.sql, .csv, .json, .xml) as discoverable and navigable to AI agents as source code is in **CodeGraph**.

---

## ⚡ Quick Start

### 1. Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 15+](https://www.postgresql.org/) with `pgvector` (`CREATE EXTENSION IF NOT EXISTS vector;`)
- Local [Ollama](https://ollama.ai) (`ollama pull nomic-embed-text`) or any OpenAI-compatible API endpoint

### 2. Build & Setup
```powershell
# Clone and build
git clone https://github.com/quedeve/doc-graph.git
cd doc-graph
dotnet build DocumentGraph.slnx

# Initialize configuration
dotnet run --project src/DocumentGraph.Tool -- init
```

### 3. The 4-Step Indexing Workflow (Run First!)
```powershell
# 1. Index your documents & code
dotnet run --project src/DocumentGraph.Tool -- index .

# 2. Vectorize chunks for semantic search
dotnet run --project src/DocumentGraph.Tool -- embed

# 3. Build knowledge graph edges & cross-references
dotnet run --project src/DocumentGraph.Tool -- build-relationships

# 4. Verify index status
dotnet run --project src/DocumentGraph.Tool -- status
```

### 4. Search & Inspect
```powershell
# Hybrid search (PostgreSQL FTS + pgvector RRF)
dotnet run --project src/DocumentGraph.Tool -- search "OAuth2 token expiration"

# Surgical read without loading whole files
dotnet run --project src/DocumentGraph.Tool -- read docs/Architecture.pdf --page 2

# Continuous background watcher
dotnet run --project src/DocumentGraph.Tool -- watch .
```

---

## 🤖 AI Agent & Model Compatibility (Universal MCP Server)

DocumentGraph is **100% Model-Agnostic and Client-Agnostic**. It implements the open **Model Context Protocol (MCP)** over `stdio` with isolated `stderr` logging:

- **Supported AI Clients**: **Roo Code**, **Zoo Code / Cline**, **Open Code (OpenHands)**, **Claude Desktop**, **Cursor**, **Windsurf**, **Continue.dev**, **Antigravity**.
- **Supported LLM Brains**: **DeepSeek (V3, R1)**, **Anthropic Claude (3.5 Sonnet, Opus)**, **OpenAI (GPT-4o, o1)**, **Qwen 2.5**, **Meta Llama 3**, and any local model.
- **Supported Embedding Engines**: Local offline **Ollama** (`nomic-embed-text`), **vLLM**, **LM Studio**, **LocalAI**, or any standard `/v1/embeddings` endpoint.

Drop-in configuration (works in Roo Code, Cursor, Claude Desktop, etc.):
```json
{
  "mcpServers": {
    "documentgraph": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DocumentGraph.Mcp"]
    }
  }
}
```

### Available MCP Tools:
- **`search`**: Blended hybrid search across documentation and code.
- **`open`**: Surgical slicing (`page`, `slide`, `sheet`, `section`, `symbol`, line windows).
- **`outline`**: Table of contents, slide counts, sheet names, and symbol declarations.
- **`find`**: Filter and locate files in the index.
- **`references`**: Find every design doc, spec, presentation, or code file referencing a symbol.
- **`related`**: Knowledge graph neighborhood traversal (integrated with CodeGraph).
- **`context`**: Budget-constrained context packet synthesis for LLM prompts.

---

## 📚 Documentation Index

- **[`HOWTOUSE.MD`](HOWTOUSE.MD)** — Complete command reference, query syntax, filters, and usage recipes.
- **[`INSTALL.md`](INSTALL.md)** — Installation steps, PostgreSQL setup, pgvector, and Ollama.
- **[`PUBLISH.md`](PUBLISH.md)** — Release build instructions and precompiled binaries.
- **[`RUN.md`](RUN.md)** — Comprehensive CLI, MCP, and REST API guide.
- **[`TEST_SUMMARY.MD`](TEST_SUMMARY.MD)** — Test results (82 passing tests), coverage matrix, and test logs.
- **[`SUMMARY.md`](SUMMARY.md)** — Technical architecture summary.

---

## 🧪 Testing

DocumentGraph includes 82 automated unit and integration tests covering all 14 supported formats:

```powershell
dotnet test DocumentGraph.slnx
```
```
Passed! - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 1.4s
```

---

## 📄 License
MIT License.
