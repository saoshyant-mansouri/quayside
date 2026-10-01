# Quayside.Api

Minimal API host for the chat endpoint. It streams server-sent events at
`/api/chat`, orchestrates retrieval and tool calls, and enforces the grounding
contract before any model text reaches the browser.

## Offline mode

Setting `Quayside:Offline=true` swaps every external dependency for a local
fake so the whole host runs with no Azure resources:

| real | offline |
|---|---|
| Azure OpenAI chat | `ScriptedChatClient` |
| Azure OpenAI embeddings | `HashingEmbeddingGenerator` |
| Azure SQL chunk store, schema catalog, executor | in-memory data and `CannedSqlExecutor` |

Offline mode exercises the plumbing: SSE framing, tool-call events, the
grounding gate, the citation enforcer, the SQL validator, caching and the
conversation limits. It says nothing about retrieval quality.

### What offline mode cannot show

The hashing embedder maps text to vectors by hashing tokens. Its cosine
similarities do not separate relevant text from irrelevant text, and they are
not on the same scale as real embedding cosines. Two consequences follow.

- The grounding threshold `MinTopCosine` is clamped down to
  `HashingEmbeddingGenerator.SuggestedMinTopCosine` (0.12) offline. At the
  production value nothing would ever clear the floor.
- The "Correct refusal" example question, "What was MSC's net profit in 2024?",
  does not refuse offline. Its best chunk scores about 0.222, clears the
  lowered floor, and the scripted model answers from it. That is a property of
  the fake, not of the gate. The example will only refuse once the API runs
  against real Azure OpenAI embeddings and the production threshold.

The offline fakes are deliberately not tuned to make that example refuse.

### Natural-language to SQL offline

`ScriptedChatClient` builds its statement from the tables that appear in the
prompt it receives: it takes the first `CREATE TABLE ops.<name>` block and
selects its leading columns. `SqlGuard` is unchanged and still validates the
statement against the retrieved tables, so both outcomes are demonstrable:
a valid `SELECT TOP (5) ...` that executes against `CannedSqlExecutor` and
returns synthetic rows, and a rejected statement (ask it to delete or update
something). The rows are placeholders labelled synthetic, not real data.

## Web search fallback

The captured corpus stays the primary grounded source. When
`search_knowledge` finds nothing relevant, the model may call `search_web` once
as a fallback. Its results join the turn's source ledger after any corpus
sources, are cited as `[n]` with `source: "web"`, and are the provider's
snippets with the real result URL: no page is fetched. Titles and snippets are
untrusted: they are stripped of markup, control characters and bracketed
markers, bounded in length, restricted to plain http(s) URLs, and handed to the
model between `BEGIN UNTRUSTED WEB CONTENT` and `END UNTRUSTED WEB CONTENT`
lines. Answers that used web sources are never written to the answer cache.

The tool exists only when `IWebSearch` is not `NoWebSearch`. Without a
`WebSearch:ApiKey` (or in offline mode) the kernel has no `search_web` function,
the system prompt does not mention it, and startup logs one line saying the
fallback is disabled and why. `search_web` refuses to run before
`search_knowledge` has, and `search_knowledge` is closed once a web search ran so
citation markers stay stable.

## Configuration keys

| key | default | meaning |
|---|---|---|
| `WebSearch:ApiKey` | none | provider key; absent means the fallback is disabled |
| `WebSearch:Provider` | `tavily` | `tavily` or `brave` |
| `WebSearch:MaxResults` | `5` | results per search, clamped to 1..10 |
| `WebSearch:Enabled` | `true` | set `false` to disable even when a key is present |
