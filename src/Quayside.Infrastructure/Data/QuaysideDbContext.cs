using Microsoft.EntityFrameworkCore;

namespace Quayside.Infrastructure.Data;

public sealed class QuaysideDbContext(DbContextOptions<QuaysideDbContext> options) : DbContext(options)
{
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<ChunkEntity> Chunks => Set<ChunkEntity>();
    public DbSet<AnswerCacheEntity> AnswerCache => Set<AnswerCacheEntity>();
    public DbSet<SchemaCardEntity> SchemaCards => Set<SchemaCardEntity>();
    public DbSet<QueryLogEntity> QueryLog => Set<QueryLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Url).HasMaxLength(800);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.PublishedLabel).HasMaxLength(64);
            entity.Property(e => e.ContentHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
            entity.HasIndex(e => e.Url).HasDatabaseName("IX_Documents_Url");
        });

        modelBuilder.Entity<ChunkEntity>(entity =>
        {
            entity.ToTable("Chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(192);
            entity.Property(e => e.DocumentId).HasMaxLength(128);
            entity.Property(e => e.Embedding).HasColumnType(VectorSpec.ColumnType);
            entity.HasIndex(e => new { e.DocumentId, e.Ordinal }).HasDatabaseName("IX_Chunks_DocumentId_Ordinal");
            entity.HasOne(e => e.Document).WithMany(d => d.Chunks).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnswerCacheEntity>(entity =>
        {
            entity.ToTable("AnswerCache");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuestionHash).HasMaxLength(32).IsFixedLength();
            entity.Property(e => e.Question).HasMaxLength(2000);
            entity.Property(e => e.QuestionEmbedding).HasColumnType(VectorSpec.ColumnType);
            entity.HasIndex(e => e.QuestionHash).IsUnique().HasDatabaseName("IX_AnswerCache_QuestionHash");
        });

        modelBuilder.Entity<SchemaCardEntity>(entity =>
        {
            entity.ToTable("SchemaCards");
            entity.HasKey(e => e.TableName);
            entity.Property(e => e.TableName).HasMaxLength(128);
            entity.Property(e => e.Context).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Embedding).HasColumnType(VectorSpec.ColumnType);
        });

        modelBuilder.Entity<QueryLogEntity>(entity =>
        {
            entity.ToTable("QueryLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasMaxLength(32);
            entity.Property(e => e.Question).HasMaxLength(2000);
            entity.Property(e => e.Outcome).HasMaxLength(64);
            entity.Property(e => e.Detail).HasMaxLength(2000);
            entity.HasIndex(e => e.At).HasDatabaseName("IX_QueryLog_At");
        });
    }
}
