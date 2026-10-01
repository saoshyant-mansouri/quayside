
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
