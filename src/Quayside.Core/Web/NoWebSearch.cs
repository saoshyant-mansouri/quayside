namespace Quayside.Core.Web;

public sealed class NoWebSearch(string reason) : IWebSearch
{
    public string Reason { get; } = reason;

    public Task<IReadOnlyList<WebResult>> SearchAsync(string query, int maxResults, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WebResult>>([]);
}
