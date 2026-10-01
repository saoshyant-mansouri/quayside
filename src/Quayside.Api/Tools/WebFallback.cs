using Quayside.Core;
using Quayside.Core.Web;
using Quayside.Infrastructure.Web;

namespace Quayside.Api.Tools;

public sealed class WebFallback(IWebSearch search, WebSearchOptions? options = null)
{
    public bool Enabled => search is not NoWebSearch;

    public IWebSearch Search => search;

    public int MaxResults => options?.MaxResults ?? WebSearchOptions.DefaultMaxResults;

    public string StatusLine => search is NoWebSearch off
        ? $"Web search fallback disabled: {off.Reason}. Answers come from the captured MSC corpus only."
        : $"Web search fallback enabled: provider {options?.Provider ?? "custom"}, up to {MaxResults} results per search.";
}
