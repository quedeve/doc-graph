using DocumentGraph.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace DocumentGraph.Storage.PostgreSQL;

/// <summary>
/// EF Core database context for DocumentGraph.
/// Manages documents, sections, chunks, symbols, and relationships.
/// </summary>
public class DocumentGraphDbContext : DbContext
{
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<SectionEntity> Sections => Set<SectionEntity>();
    public DbSet<ChunkEntity> Chunks => Set<ChunkEntity>();
    public DbSet<SymbolEntity> Symbols => Set<SymbolEntity>();
    public DbSet<RelationshipEntity> Relationships => Set<RelationshipEntity>();

    public DocumentGraphDbContext(DbContextOptions<DocumentGraphDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        ConfigureDocument(modelBuilder);
        ConfigureSection(modelBuilder);
        ConfigureChunk(modelBuilder);
        ConfigureSymbol(modelBuilder);
        ConfigureRelationship(modelBuilder);
    }

    private static void ConfigureDocument(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Path).HasColumnName("path").IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Filename).HasColumnName("filename").IsRequired().HasMaxLength(256);
            entity.Property(e => e.Extension).HasColumnName("extension").HasMaxLength(16);
            entity.Property(e => e.MimeType).HasColumnName("mime_type").HasMaxLength(128);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(512);
            entity.Property(e => e.DocumentType).HasColumnName("document_type").HasMaxLength(32);
            entity.Property(e => e.Size).HasColumnName("size");
            entity.Property(e => e.Hash).HasColumnName("hash").HasMaxLength(64);
            entity.Property(e => e.ModifiedAt).HasColumnName("modified_at");
            entity.Property(e => e.IndexedAt).HasColumnName("indexed_at");
            entity.Property(e => e.Parser).HasColumnName("parser").HasMaxLength(64);
            entity.Property(e => e.Language).HasColumnName("language").HasMaxLength(32);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.Hash);
            entity.HasIndex(e => e.DocumentType);
            entity.HasIndex(e => e.Extension);
        });
    }

    private static void ConfigureSection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SectionEntity>(entity =>
        {
            entity.ToTable("document_sections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.SectionIndex).HasColumnName("section_index");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(512);
            entity.Property(e => e.PageNumber).HasColumnName("page_number");
            entity.Property(e => e.SlideNumber).HasColumnName("slide_number");
            entity.Property(e => e.SheetName).HasColumnName("sheet_name").HasMaxLength(128);
            entity.Property(e => e.Depth).HasColumnName("depth");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(e => e.Document).WithMany(d => d.Sections).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Parent).WithMany(s => s.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DocumentId);
        });
    }

    private static void ConfigureChunk(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChunkEntity>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.SectionId).HasColumnName("section_id");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.TokenCount).HasColumnName("token_count");
            entity.Property(e => e.PageNumber).HasColumnName("page_number");
            entity.Property(e => e.SlideNumber).HasColumnName("slide_number");
            entity.Property(e => e.SheetName).HasColumnName("sheet_name").HasMaxLength(128);
            entity.Property(e => e.LineStart).HasColumnName("line_start");
            entity.Property(e => e.LineEnd).HasColumnName("line_end");
            entity.Property(e => e.SectionTitle).HasColumnName("section_title").HasMaxLength(512);
            entity.Property(e => e.SearchVector).HasColumnName("search_vector").HasColumnType("tsvector");
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(e => e.Document).WithMany(d => d.Chunks).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Section).WithMany(s => s.Chunks).HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.SearchVector).HasMethod("gin");

            // pgvector index for embedding search
            entity.HasIndex(e => e.Embedding)
                .HasMethod("ivfflat")
                .HasOperators("vector_cosine_ops");
        });
    }

    private static void ConfigureSymbol(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SymbolEntity>(entity =>
        {
            entity.ToTable("symbols");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
            entity.Property(e => e.SymbolType).HasColumnName("symbol_type").HasMaxLength(32);
            entity.Property(e => e.Namespace).HasColumnName("namespace").HasMaxLength(512);
            entity.Property(e => e.ParentSymbolId).HasColumnName("parent_symbol_id");
            entity.Property(e => e.LineStart).HasColumnName("line_start");
            entity.Property(e => e.LineEnd).HasColumnName("line_end");
            entity.Property(e => e.Signature).HasColumnName("signature").HasMaxLength(1024);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(e => e.Document).WithMany(d => d.Symbols).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ParentSymbol).WithMany(s => s.Children).HasForeignKey(e => e.ParentSymbolId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SymbolType);
        });
    }

    private static void ConfigureRelationship(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RelationshipEntity>(entity =>
        {
            entity.ToTable("relationships");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(32);
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType).HasColumnName("target_type").HasMaxLength(32);
            entity.Property(e => e.RelationshipType).HasColumnName("relationship_type").HasMaxLength(32);
            entity.Property(e => e.Confidence).HasColumnName("confidence");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasIndex(e => new { e.SourceId, e.SourceType });
            entity.HasIndex(e => new { e.TargetId, e.TargetType });
            entity.HasIndex(e => e.RelationshipType);
        });
    }
}
