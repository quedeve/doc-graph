using DocumentGraph.Core.Configuration;
using DocumentGraph.Core.Entities;
using DocumentGraph.Core.Interfaces;
using DocumentGraph.Core.Models;
using DocumentGraph.Indexer.Scanner;
using Microsoft.Extensions.Logging;

namespace DocumentGraph.Indexer.Pipeline;

/// <summary>
/// Orchestrates the full indexing pipeline:
/// Scan → Hash → Parse → Chunk → Store
/// Supports incremental indexing via file hash comparison.
/// </summary>
public class IndexingPipeline
{
    private readonly IFileScanner _scanner;
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IEnumerable<IChunker> _chunkers;
    private readonly IDocumentRepository _repository;
    private readonly ILogger<IndexingPipeline> _logger;
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly DocumentGraph.Core.Observability.IObservabilityService? _observability;

    public IndexingPipeline(
        IFileScanner scanner,
        IEnumerable<IDocumentParser> parsers,
        IEnumerable<IChunker> chunkers,
        IDocumentRepository repository,
        ILogger<IndexingPipeline> logger,
        IEmbeddingProvider? embeddingProvider = null,
        DocumentGraph.Core.Observability.IObservabilityService? observability = null)
    {
        _scanner = scanner;
        _parsers = parsers;
        _chunkers = chunkers;
        _repository = repository;
        _logger = logger;
        _embeddingProvider = embeddingProvider;
        _observability = observability;
    }

    /// <summary>
    /// Run the full indexing pipeline for a directory.
    /// </summary>
    public async Task<IndexingResult> IndexAsync(
        string rootPath,
        DocumentGraphConfig config,
        CancellationToken cancellationToken = default)
    {
        var result = new IndexingResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Step 1: Scan for eligible files
        _logger.LogInformation("Scanning {Root}...", rootPath);
        var scannedFiles = await _scanner.ScanAsync(rootPath, config.Indexing, cancellationToken);
        result.FilesScanned = scannedFiles.Count;

        // Step 2: Get existing hashes for incremental indexing
        var existingHashes = await _repository.GetAllPathHashesAsync(cancellationToken);
        var scannedPaths = new HashSet<string>(scannedFiles.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);

        // Step 3: Detect deleted files
        foreach (var existingPath in existingHashes.Keys)
        {
            if (!scannedPaths.Contains(existingPath))
            {
                _logger.LogInformation("Removing deleted file: {Path}", existingPath);
                await _repository.DeleteByPathAsync(existingPath, cancellationToken);
                result.FilesDeleted++;
            }
        }

        // Step 4: Process each file
        var semaphore = new SemaphoreSlim(config.Indexing.MaxParallelism);
        var tasks = scannedFiles.Select(file => ProcessFileAsync(
            file, existingHashes, config, result, semaphore, cancellationToken));

        await Task.WhenAll(tasks);

        sw.Stop();
        result.Duration = sw.Elapsed;
        _observability?.RecordIndexing(
            result.FilesScanned, result.FilesIndexed, result.FilesSkipped,
            result.FilesFailed, result.ChunksCreated, result.EmbeddingsGenerated,
            result.Duration);

        return result;
    }

    /// <summary>
    /// Index or reindex a single specific file by path.
    /// </summary>
    public async Task<bool> IndexSingleFileAsync(
        string filePath,
        DocumentGraphConfig config,
        bool forceReindex = false,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found for indexing: {Path}", fullPath);
            return false;
        }

        var fileInfo = new FileInfo(fullPath);
        var ext = fileInfo.Extension.ToLowerInvariant();

        var parser = _parsers.FirstOrDefault(p => p.CanParse(ext));
        if (parser == null)
        {
            _logger.LogWarning("No parser registered for file extension: {Ext}", ext);
            return false;
        }

        var hash = await FileScanner.ComputeHashAsync(fullPath, cancellationToken);
        if (!forceReindex)
        {
            var existingDoc = await _repository.GetByPathAsync(fullPath, cancellationToken);
            if (existingDoc != null && existingDoc.Hash == hash)
            {
                _logger.LogInformation("File unchanged, skipping: {Path}", fullPath);
                return true;
            }
        }

        _logger.LogInformation("Parsing and indexing: {Path}", fullPath);
        var parsed = await parser.ParseAsync(fullPath, cancellationToken);
        parsed.Hash = hash;
        parsed.FileSize = fileInfo.Length;
        parsed.ModifiedAt = fileInfo.LastWriteTimeUtc;

        var chunker = _chunkers.FirstOrDefault(c => c.CanChunk(parsed.DocumentType));
        var chunks = chunker?.Chunk(parsed) ?? DefaultChunk(parsed);

        var entity = ToEntity(parsed, chunks);

        if (_embeddingProvider != null && entity.Chunks.Count > 0)
        {
            try
            {
                var chunkTexts = entity.Chunks.Select(c => c.Content).ToList();
                var vectors = await _embeddingProvider.EmbedBatchAsync(chunkTexts, cancellationToken);
                for (int i = 0; i < entity.Chunks.Count && i < vectors.Length; i++)
                {
                    entity.Chunks[i].Embedding = new Pgvector.Vector(vectors[i]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embeddings for {Path}", fullPath);
            }
        }

        await _repository.UpsertAsync(entity, cancellationToken);
        _logger.LogInformation("Successfully indexed {Path} ({Chunks} chunks)", fullPath, chunks.Count);
        return true;
    }

    /// <summary>
    /// Remove a file from the index by its path.
    /// </summary>
    public async Task<bool> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        return await _repository.DeleteByPathAsync(fullPath, cancellationToken);
    }

    private async Task ProcessFileAsync(
        ScannedFile file,
        Dictionary<string, string> existingHashes,
        DocumentGraphConfig config,
        IndexingResult result,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Compute hash for incremental check
            var hash = await FileScanner.ComputeHashAsync(file.Path, cancellationToken);

            // Skip if unchanged
            if (existingHashes.TryGetValue(file.Path, out var existingHash) && existingHash == hash)
            {
                Interlocked.Increment(ref result._filesSkipped);
                return;
            }

            // Find a parser for this file
            var parser = _parsers.FirstOrDefault(p => p.CanParse(file.Extension));
            if (parser == null)
            {
                _logger.LogWarning("No parser registered for extension: {Ext}", file.Extension);
                Interlocked.Increment(ref result._filesFailed);
                result.AddError(file.Path, $"No parser for {file.Extension}");
                return;
            }

            // Parse
            _logger.LogDebug("Parsing: {Path}", file.Path);
            ParsedDocument parsed;
            try
            {
                parsed = await parser.ParseAsync(file.Path, cancellationToken);
                parsed.Hash = hash;
                parsed.FileSize = file.Size;
                parsed.ModifiedAt = file.ModifiedAt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse: {Path}", file.Path);
                Interlocked.Increment(ref result._filesFailed);
                result.AddError(file.Path, ex.Message);
                return;
            }

            // Chunk
            var chunker = _chunkers.FirstOrDefault(c => c.CanChunk(parsed.DocumentType));
            var chunks = chunker?.Chunk(parsed) ?? DefaultChunk(parsed);

            // Convert to entity
            var entity = ToEntity(parsed, chunks);

            // Generate embeddings if provider is available
            if (_embeddingProvider != null && entity.Chunks.Count > 0)
            {
                try
                {
                    var chunkTexts = entity.Chunks.Select(c => c.Content).ToList();
                    var vectors = await _embeddingProvider.EmbedBatchAsync(chunkTexts, cancellationToken);
                    for (int i = 0; i < entity.Chunks.Count && i < vectors.Length; i++)
                    {
                        entity.Chunks[i].Embedding = new Pgvector.Vector(vectors[i]);
                    }
                }
                catch (Exception embEx)
                {
                    _logger.LogWarning(embEx, "Failed to generate embeddings for {Path}; continuing without vectors", file.Path);
                }
            }

            await _repository.UpsertAsync(entity, cancellationToken);

            Interlocked.Increment(ref result._filesIndexed);
            Interlocked.Add(ref result._chunksCreated, chunks.Count);
            _logger.LogDebug("Indexed: {Path} ({Chunks} chunks)", file.Path, chunks.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error indexing: {Path}", file.Path);
            Interlocked.Increment(ref result._filesFailed);
            result.AddError(file.Path, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Fallback chunking: treat each section as a single chunk.
    /// </summary>
    private static List<DocumentChunk> DefaultChunk(ParsedDocument document)
    {
        var chunks = new List<DocumentChunk>();
        for (int i = 0; i < document.Sections.Count; i++)
        {
            var section = document.Sections[i];
            if (string.IsNullOrWhiteSpace(section.Content))
                continue;

            chunks.Add(new DocumentChunk
            {
                ChunkIndex = chunks.Count,
                Content = section.Content,
                TokenCount = EstimateTokens(section.Content),
                PageNumber = section.PageNumber,
                SlideNumber = section.SlideNumber,
                SheetName = section.SheetName,
                LineStart = section.LineStart,
                LineEnd = section.LineEnd,
                SectionTitle = section.Title
            });
        }
        return chunks;
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static DocumentEntity ToEntity(ParsedDocument parsed, List<DocumentChunk> chunks)
    {
        var entity = new DocumentEntity
        {
            Path = parsed.SourcePath,
            Filename = Path.GetFileName(parsed.SourcePath),
            Extension = parsed.Extension,
            Title = parsed.Title,
            DocumentType = parsed.DocumentType,
            Size = parsed.FileSize,
            Hash = parsed.Hash,
            ModifiedAt = parsed.ModifiedAt,
            IndexedAt = DateTime.UtcNow,
            Language = parsed.Language,
            Metadata = parsed.Metadata.Count > 0 ? parsed.Metadata : null
        };

        // Convert sections (flatten for storage)
        int sectionIdx = 0;
        FlattenSections(parsed.Sections, entity.Sections, ref sectionIdx, parentId: null);

        // Convert chunks
        foreach (var chunk in chunks)
        {
            entity.Chunks.Add(new ChunkEntity
            {
                ChunkIndex = chunk.ChunkIndex,
                Content = DocumentGraph.Core.Security.ContentSanitizer.SanitizeText(chunk.Content),
                TokenCount = chunk.TokenCount,
                PageNumber = chunk.PageNumber,
                SlideNumber = chunk.SlideNumber,
                SheetName = chunk.SheetName,
                LineStart = chunk.LineStart,
                LineEnd = chunk.LineEnd,
                SectionTitle = chunk.SectionTitle,
                Metadata = chunk.Metadata.Count > 0 ? chunk.Metadata : null
            });
        }

        // Convert symbols (flatten for storage)
        int symbolIdx = 0;
        FlattenSymbols(parsed.Symbols, entity.Symbols, ref symbolIdx, parentId: null);

        return entity;
    }

    private static void FlattenSections(
        List<DocumentSection> sections, List<SectionEntity> entities,
        ref int index, long? parentId)
    {
        foreach (var section in sections)
        {
            var sectionEntity = new SectionEntity
            {
                SectionIndex = index++,
                Title = section.Title,
                PageNumber = section.PageNumber,
                SlideNumber = section.SlideNumber,
                SheetName = section.SheetName,
                Depth = section.Depth,
                Metadata = section.Metadata.Count > 0 ? section.Metadata : null
            };
            entities.Add(sectionEntity);

            if (section.Children.Count > 0)
            {
                FlattenSections(section.Children, entities, ref index, parentId: null);
            }
        }
    }

    private static void FlattenSymbols(
        List<CodeSymbol> symbols, List<SymbolEntity> entities,
        ref int index, long? parentId)
    {
        foreach (var symbol in symbols)
        {
            var symbolEntity = new SymbolEntity
            {
                Name = symbol.Name,
                SymbolType = symbol.Kind.ToString(),
                Namespace = symbol.Namespace,
                Signature = symbol.Signature,
                LineStart = symbol.LineStart,
                LineEnd = symbol.LineEnd,
                Metadata = symbol.Metadata.Count > 0 ? symbol.Metadata : null
            };
            entities.Add(symbolEntity);
            index++;

            if (symbol.Children.Count > 0)
            {
                FlattenSymbols(symbol.Children, entities, ref index, parentId: null);
            }
        }
    }
}

/// <summary>
/// Summary of an indexing run.
/// </summary>
public class IndexingResult
{
    public int FilesScanned { get; set; }
    public int FilesIndexed => _filesIndexed;
    public int FilesSkipped => _filesSkipped;
    public int FilesFailed => _filesFailed;
    public int FilesDeleted { get; set; }
    public int ChunksCreated => _chunksCreated;
    public int EmbeddingsGenerated { get; internal set; }
    public TimeSpan Duration { get; set; }

    // Thread-safe counters
    internal int _filesIndexed;
    internal int _filesSkipped;
    internal int _filesFailed;
    internal int _chunksCreated;

    private readonly object _errorLock = new();
    public List<IndexingError> Errors { get; } = [];

    internal void AddError(string path, string message)
    {
        lock (_errorLock)
        {
            Errors.Add(new IndexingError { Path = path, Message = message });
        }
    }
}

public class IndexingError
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
