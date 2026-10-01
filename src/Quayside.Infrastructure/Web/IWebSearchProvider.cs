using Quayside.Core;

namespace Quayside.Infrastructure.Web;

public interface IWebSearchProvider
{
    HttpRequestMessage CreateRequest(string query, int maxResults);

    IReadOnlyList<WebResult> Parse(string json);
}
