namespace Quayside.Api.Tools;

public sealed record OperationalQuery(string Sql, IReadOnlySet<string> Tables, int MaxRows);
