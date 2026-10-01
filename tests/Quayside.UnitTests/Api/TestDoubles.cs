using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Quayside.Api.Offline;
using Quayside.Core;
using Quayside.Core.Documents;
using Quayside.Core.Sql;

namespace Quayside.UnitTests.Api;

public sealed class CountingSqlExecutor : IReadOnlySqlExecutor
{
    private readonly CannedSqlExecutor inner = new();
    private readonly ConcurrentQueue<string> statements = new();

    public Exception? Failure { get; set; }

    public IReadOnlyList<string> Statements => statements.ToArray();

    public Task<SqlResultSet> ExecuteAsync(string sql, CancellationToken ct)
    {
        statements.Enqueue(sql);
        return Failure is { } failure ? Task.FromException<SqlResultSet>(failure) : inner.ExecuteAsync(sql, ct);
    }
}

public sealed class GatedChunkStore(IChunkStore inner, Task gate) : IChunkStore
{
    public async Task<IReadOnlyList<Document>> LoadDocumentsAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        return await inner.LoadDocumentsAsync(ct);
    }

    public async Task<IReadOnlyList<EmbeddedChunk>> LoadChunksAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        return await inner.LoadChunksAsync(ct);
    }

    public Task UpsertAsync(IReadOnlyList<Document> documents, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken ct) =>
        inner.UpsertAsync(documents, chunks, ct);
}

public sealed record LogLine(string Category, LogLevel Level, string Message, Exception? Exception);

public sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<LogLine> lines = new();

    public IReadOnlyList<LogLine> Lines => lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new Capture(categoryName, lines);

    public void Dispose()
    {
    }

    private sealed class Capture(string category, ConcurrentQueue<LogLine> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue(new LogLine(category, logLevel, formatter(state, exception), exception));
    }
}
