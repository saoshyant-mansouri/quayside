namespace Quayside.Core.Sql;

public sealed record TableCard(
    string Table,
    string Context,
    string Description,
    string Card,
    IReadOnlyList<string> Neighbours,
    string Ddl);

public sealed record EmbeddedTableCard(TableCard Card, ReadOnlyMemory<float> Embedding);

public sealed record SqlResultSet(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, double ElapsedMs);

public sealed record SqlValidation(bool Accepted, string? Reason, IReadOnlyList<string> TablesTouched);
