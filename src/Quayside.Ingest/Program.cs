using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quayside.Core;
using Quayside.Core.Documents;
using Quayside.Core.Sql;
using Quayside.Infrastructure;
using Quayside.Infrastructure.Configuration;
using Quayside.Infrastructure.Data;
using Quayside.Infrastructure.Schema;
using Quayside.Infrastructure.Sql;
using Quayside.Ingest.Schema;

namespace Quayside.Ingest;

public static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var isDryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);
        var schemaOnly = args.Contains("--schema-only", StringComparer.OrdinalIgnoreCase);
        var cardsOnly = args.Contains("--cards-only", StringComparer.OrdinalIgnoreCase);
        var corpusOnly = args.Contains("--corpus-only", StringComparer.OrdinalIgnoreCase);
        var all = args.Contains("--all", StringComparer.OrdinalIgnoreCase) || (!migrateOnly && !schemaOnly && !cardsOnly && !corpusOnly);

        var configuration = BuildConfiguration(args);
        var dataPaths = DataPaths.Resolve(configuration[ConfigurationKeys.CorpusDirectory] ?? QuaysideOptions.DefaultCorpusDirectory, AppContext.BaseDirectory);

        if (isDryRun)
        {
            var cards = TableCardFactory.Load(dataPaths.SchemaFile);
            var documents = LoadCorpus(dataPaths.CorpusDirectory);
            var chunks = documents.SelectMany(doc => Chunker.Split(doc)).ToArray();

            Console.WriteLine($"Cards: {cards.Count}");
            Console.WriteLine($"Documents: {documents.Count}");
            Console.WriteLine($"Chunks: {chunks.Length}");
            return 0;
        }

        var services = new ServiceCollection();
        services.AddQuaysideInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        var ct = cts.Token;

        if (all || migrateOnly)
        {
            await provider.MigrateQuaysideDatabaseAsync(ct);
        }

        if (all || schemaOnly)
        {
            var spec = SchemaFile.Load(dataPaths.SchemaFile);
            var ddl = DdlEmitter.Emit(spec);
            var seed = SeedEmitter.Emit(spec);
            var database = provider.GetRequiredService<SqlDatabase>();
            await using var connection = await database.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct, commandTimeout: 300));
            await connection.ExecuteAsync(new CommandDefinition(seed, cancellationToken: ct, commandTimeout: 300));
        }

        if (all || cardsOnly)
        {
            var cards = TableCardFactory.Load(dataPaths.SchemaFile);
            var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            var vectors = await generator.GenerateAsync(cards.Select(c => c.Card), cancellationToken: ct);
            var embeddedCards = cards.Select((card, i) => new EmbeddedTableCard(card, vectors[i].Vector)).ToArray();
            var catalog = provider.GetRequiredService<SqlSchemaCatalog>();
            await catalog.UpsertAsync(embeddedCards, ct);
        }

        if (all || corpusOnly)
        {
            var documents = LoadCorpus(dataPaths.CorpusDirectory);
            var chunks = documents.SelectMany(doc => Chunker.Split(doc)).ToArray();
            var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            var vectors = await generator.GenerateAsync(chunks.Select(c => c.Text), cancellationToken: ct);
            var embeddedChunks = chunks.Select((chunk, i) => new EmbeddedChunk(chunk, vectors[i].Vector)).ToArray();
            var store = provider.GetRequiredService<IChunkStore>();
            await store.UpsertAsync(documents, embeddedChunks, ct);
        }

        return 0;
    }

    private static IConfiguration BuildConfiguration(string[] args) =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json", optional: true)
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

    private static IReadOnlyList<Document> LoadCorpus(string corpusDirectory)
    {
        var documents = new List<Document>();

        var websitePath = Path.Combine(corpusDirectory, "website-pages.json");
        if (File.Exists(websitePath))
        {
            using var stream = File.OpenRead(websitePath);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            var capturedAt = DateTimeOffset.Parse(root.GetProperty("capturedAt").GetString()!, CultureInfo.InvariantCulture);
            if (root.TryGetProperty("pages", out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    var url = page.GetProperty("url").GetString() ?? string.Empty;
                    var text = page.GetProperty("text").GetString() ?? string.Empty;
                    var title = page.GetProperty("title").GetString() ?? url;
                    var id = $"web-{Hash(url)[..16]}";
                    documents.Add(new Document(id, SourceKind.Website, url, title, text, null, capturedAt, Hash(text)));
                }
            }
        }

        var linkedInPath = Path.Combine(corpusDirectory, "linkedin-posts.json");
        if (File.Exists(linkedInPath))
        {
            using var stream = File.OpenRead(linkedInPath);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            var capturedAt = DateTimeOffset.Parse(root.GetProperty("capturedAt").GetString()!, CultureInfo.InvariantCulture);
            if (root.TryGetProperty("posts", out var posts))
            {
                foreach (var post in posts.EnumerateArray())
                {
                    var urn = post.GetProperty("urn").GetString() ?? string.Empty;
                    var url = post.GetProperty("url").GetString() ?? string.Empty;
                    var text = post.GetProperty("text").GetString() ?? string.Empty;
                    var age = post.TryGetProperty("age", out var ageProp) ? ageProp.GetString() : null;
                    var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "LinkedIn post";
                    var title = firstLine.Length <= 90 ? firstLine : firstLine[..90] + "...";
                    var id = $"li-{urn}";
                    documents.Add(new Document(id, SourceKind.LinkedIn, url, title, text, PublishedDate(age, capturedAt), capturedAt, Hash(text)));
                }
            }
        }

        return documents;
    }

    private static string? PublishedDate(string? age, DateTimeOffset capturedAt)
    {
        var match = AgePattern().Match(age ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var published = match.Groups[2].Value switch
        {
            "d" => capturedAt.AddDays(-amount),
            "w" => capturedAt.AddDays(-7 * amount),
            "mo" => capturedAt.AddMonths(-amount),
            "yr" => capturedAt.AddYears(-amount),
            _ => capturedAt,
        };
        return published.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex(@"^(\d+)(d|w|mo|yr)")]
    private static partial Regex AgePattern();
}
