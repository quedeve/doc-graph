# DocumentGraph — Build & Publish Guide

This document describes how to compile, build, and publish **DocumentGraph.Mcp** (MCP Server) and **DocumentGraph.Tool** (CLI) into optimized standalone binaries.

---

## 1. Why Publish `.exe` for MCP?

Running pre-compiled executables (`.exe`) is the industry standard for Model Context Protocol servers:
- **Instantaneous Startup**: Starts in ~50ms compared to 2–4 seconds with `dotnet run` (which rebuilds and checks project files on every connection).
- **Clean Standard Output (`stdout`)**: Guarantees zero compiler/restore diagnostic text from leaking into the JSON-RPC communication stream.
- **Self-Contained Deployment**: Can be bundled with all dependencies so target machines don't need the .NET SDK installed.

---

## 2. Standard Framework-Dependent Publish

Produces an optimized `.exe` that uses the installed .NET runtime:

### 2.1 Publish MCP Server
```bash
dotnet publish src/DocumentGraph.Mcp/DocumentGraph.Mcp.csproj -c Release -o dist/mcp
```
Output:
- `dist/mcp/DocumentGraph.Mcp.exe`
- Associated libraries and config files

### 2.2 Publish CLI Tool
```bash
dotnet publish src/DocumentGraph.Tool/DocumentGraph.Tool.csproj -c Release -o dist/tool
```
Output:
- `dist/tool/DocumentGraph.Tool.exe`

### 2.3 Publish REST API Service
```bash
dotnet publish src/DocumentGraph.Api/DocumentGraph.Api.csproj -c Release -o dist/api
```

---

## 3. Self-Contained Single-File Publish (Zero Dependencies)

If you wish to distribute a single `.exe` file that runs on machines without any .NET runtime installed:

### 3.1 Windows x64
```bash
dotnet publish src/DocumentGraph.Mcp/DocumentGraph.Mcp.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o dist/mcp-win-x64
```

### 3.2 Linux x64
```bash
dotnet publish src/DocumentGraph.Mcp/DocumentGraph.Mcp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o dist/mcp-linux-x64
```

### 3.3 macOS ARM64 (Apple Silicon)
```bash
dotnet publish src/DocumentGraph.Mcp/DocumentGraph.Mcp.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o dist/mcp-osx-arm64
```

---

## 4. Automation Script (`publish.cmd` / `publish.sh`)

You can run the following PowerShell command from the repository root to publish all components at once:

```powershell
dotnet publish DocumentGraph.slnx -c Release
```
This builds all projects in the solution under `Release` mode with optimizations enabled.
