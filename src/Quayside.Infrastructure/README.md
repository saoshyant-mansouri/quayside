
## Embedding batch size and retry delay are quota-shaped

Both defaults were set by a real failure, not by taste.

`text-embedding-3-small` is deployed at capacity 100, which is 100k tokens per
minute. The original 256-input batch carried roughly 180k tokens in a single
request — over the per-minute allowance on its own — and four of those ran
concurrently. Ingesting the corpus failed with `HTTP 429 RateLimitReached`.
A 32-input batch is about 22k tokens, and concurrency 2 paces the requests.

The retry policy honours the service's `Retry-After`, but it clamps to
`maxDelay`. That was 30 seconds while Azure was asking for 60, so every retry
fired early and the attempts were spent without ever waiting long enough.
`maxDelay` is now 90 seconds so a 60-second instruction is obeyed exactly.

Embedding the whole corpus takes a few minutes at this quota. That is the
quota, not the code.

## Web search

`Web/` holds a provider-agnostic `WebSearchClient` over `IHttpClientFactory`
(8 second per-attempt timeout, two retries with `BackoffRetryPolicy.Backoff`
jitter, `Retry-After` honoured and capped at 3 seconds) with `TavilyProvider`
(default, bearer auth, `https://api.tavily.com/search`) and `BraveProvider`
(`X-Subscription-Token`). `AddQuaysideWebSearch` registers the client only when
`WebSearch:ApiKey` is set, `WebSearch:Enabled` is not `false` and the provider is
supported; otherwise it registers `NoWebSearch` carrying the reason. The key is
never placed in a URL, body or error message.
