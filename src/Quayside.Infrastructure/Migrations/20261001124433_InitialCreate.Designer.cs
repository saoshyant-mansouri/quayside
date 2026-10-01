using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Quayside.Infrastructure.Data;

#nullable disable

namespace Quayside.Infrastructure.Migrations
{
    [DbContext(typeof(QuaysideDbContext))]
    [Migration("20261001124433_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Quayside.Infrastructure.Data.AnswerCacheEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

                    b.Property<string>("Answer")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("CitationsJson")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("HitCount")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset?>("LastHitAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Question")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<SqlVector<float>>("QuestionEmbedding")
                        .HasColumnType("vector(1536)");

                    b.Property<byte[]>("QuestionHash")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("binary(32)")
                        .IsFixedLength();

                    b.HasKey("Id");

                    b.HasIndex("QuestionHash")
                        .IsUnique()
                        .HasDatabaseName("IX_AnswerCache_QuestionHash");

                    b.ToTable("AnswerCache", (string)null);
                });

            modelBuilder.Entity("Quayside.Infrastructure.Data.ChunkEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(192)
                        .HasColumnType("nvarchar(192)");

                    b.Property<string>("DocumentId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<SqlVector<float>>("Embedding")
                        .HasColumnType("vector(1536)");

                    b.Property<int>("Ordinal")
                        .HasColumnType("int");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TokenCount")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("DocumentId", "Ordinal")
                        .HasDatabaseName("IX_Chunks_DocumentId_Ordinal");

                    b.ToTable("Chunks", (string)null);
                });

            modelBuilder.Entity("Quayside.Infrastructure.Data.DocumentEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<DateTimeOffset>("CapturedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("ContentHash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .IsUnicode(false)
                        .HasColumnType("char(64)")
                        .IsFixedLength();

                    b.Property<string>("PublishedLabel")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<int>("Source")
                        .HasColumnType("int");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasMaxLength(800)
                        .HasColumnType("nvarchar(800)");

                    b.HasKey("Id");

                    b.HasIndex("Url")
                        .HasDatabaseName("IX_Documents_Url");

                    b.ToTable("Documents", (string)null);
                });

            modelBuilder.Entity("Quayside.Infrastructure.Data.QueryLogEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

                    b.Property<DateTimeOffset>("At")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Detail")
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<double>("ElapsedMs")
                        .HasColumnType("float");

                    b.Property<string>("GeneratedSql")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("nvarchar(32)");

                    b.Property<string>("Outcome")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("Question")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<int?>("RowCount")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("At")
                        .HasDatabaseName("IX_QueryLog_At");

                    b.ToTable("QueryLog", (string)null);
                });

            modelBuilder.Entity("Quayside.Infrastructure.Data.SchemaCardEntity", b =>
                {
                    b.Property<string>("TableName")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("Card")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Context")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("Ddl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<SqlVector<float>>("Embedding")
                        .HasColumnType("vector(1536)");

                    b.Property<string>("NeighboursJson")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("TableName");

                    b.ToTable("SchemaCards", (string)null);
                });

            modelBuilder.Entity("Quayside.Infrastructure.Data.ChunkEntity", b =>
                {
                    b.HasOne("Quayside.Infrastructure.Data.DocumentEntity", "Document")
                        .WithMany("Chunks")
                        .HasForeignKey("DocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Document");
                });

            modelBuilder.Entity("Quayside.Infrastructure.Data.DocumentEntity", b =>
                {
                    b.Navigation("Chunks");
                });
#pragma warning restore 612, 618
        }
    }
}
