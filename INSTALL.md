# DocumentGraph — Installation Guide

This guide describes how to install and configure **DocumentGraph** (CLI Tool, MCP Server, and REST API).

---

## 1. Prerequisites

### 1.1 Runtime & SDK
- **.NET 10 SDK** (or .NET 10 Desktop/Server Runtime)
  - Download from: [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
  - Verify installation:
    ```bash
    dotnet --version
    ```

### 1.2 Database (PostgreSQL + pgvector)
DocumentGraph utilizes PostgreSQL as its unified relational and vector database.
- **PostgreSQL 15+**
- **pgvector extension**:
  - Windows: Available via EDB installer or compiled DLL.
  - Linux (Ubuntu/Debian): `sudo apt-get install postgresql-16-pgvector`
  - Docker:
    ```bash
    docker run -d --name docgraph-db \
      -e POSTGRES_PASSWORD=postgres \
      -e POSTGRES_DB=documentgraph \
      -p 5432:5432 \
      pgvector/pgvector:pg16
    ```

### 1.3 Local Embeddings (Optional, for Semantic Search)
- **Ollama** (e.g. `nomic-embed-text` or `bge-m3`):
  ```bash
  ollama pull nomic-embed-text
  ```
- Or any OpenAI-compatible API endpoint.

---

## 2. Database Initialization

Connect to PostgreSQL and enable the vector extension:
```sql
CREATE DATABASE documentgraph;
\c documentgraph
CREATE EXTENSION IF NOT EXISTS vector;
```

---

## 3. Configuration Setup (`docgraph.json`)

DocumentGraph is configured via `docgraph.json`. You can generate a default configuration file by running:

```bash
dist/tool/DocumentGraph.Tool.exe init
```

Or manually create `docgraph.json` in your workspace or project root:

```json
{
  "database": {
    "connectionString": "Host=localhost;Port=5432;Database=documentgraph;Username=postgres;Password=postgres;"
  },
  "search": {
    "defaultLimit": 10,
    "ftsWeight": 1.0,
    "semanticWeight": 1.0,
    "enableHybridSearch": true
  },
  "embedding": {
    "provider": "ollama",
    "model": "nomic-embed-text",
    "endpoint": "http://localhost:11434",
    "dimension": 768
  },
  "indexing": {
    "maxParallelism": 4,
    "maxFileSizeMb": 50,
    "chunkSize": 500,
    "chunkOverlap": 50
  },
  "security": {
    "enforceAllowedRoots": false,
    "allowedRoots": []
  }
}
```

---

## 4. Deploying Binaries

1. Copy [`dist/tool/`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/tool/) to your preferred utility folder (e.g., `C:\Tools\docgraph\`).
2. Add the directory to your system `PATH` environment variable so `docgraph` is accessible from any terminal.
3. Keep [`dist/mcp/DocumentGraph.Mcp.exe`](file:///c:/Users/Victor/Documents/GIT/outside/SemanticSearch/dist/mcp/DocumentGraph.Mcp.exe) in a persistent location for MCP clients.
