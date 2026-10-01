# API contract

One streaming endpoint carries the whole conversation. The browser reaches it
same-origin at `/api`; Next.js rewrites that to the Azure API in production.

## `POST /api/chat`

Request:

```json
{ "message": "What does MSC say about its fleet decarbonisation?",
  "conversationId": "9f1c…" }
```

`conversationId` may be `null` on the first turn; the response's `done` event
returns the id to send on subsequent turns.

Response is `text/event-stream`. Events, in the order they can occur:

| event | data |
|---|---|
| `tool` | `{"name":"search_knowledge","status":"started"}` then `"completed"` with an optional `detail` object |
| `citations` | `{"citations":[{"n":1,"title":"…","url":"…","source":"linkedin"\|"website"\|"web","publishedAt":"2025-11-04"}]}` |
| `sql` | `{"sql":"SELECT TOP (50) …","columns":["…"],"rows":[["…"]],"rejected":null}` — `rejected` carries the validator's reason instead when the generated statement was refused, in which case `rows` is empty |
| `token` | `{"text":"…"}` — incremental answer text |
| `done` | `{"conversationId":"…","latencyMs":1180,"cached":false,"usage":{"prompt":2140,"completion":260},"grounding":{"cited":6,"uncited":0}}` |
| `error` | `{"message":"…"}` |

`source` says where a cited passage came from. `"linkedin"` and `"website"` are
the captured MSC corpus snapshot. `"web"` means the passage was fetched live
from a third-party search result rather than from the snapshot: `title` and
`url` are the result's own, `publishedAt` is whatever label the search provider
returned or `null`, and the cited text is the provider's snippet, never a
fetched page. Web citations appear only when the optional `search_web` tool ran
as a fallback after `search_knowledge` found nothing relevant, they are always
numbered after any corpus citations, and an answer that used them says so in
plain text. A deployment without a web search key never emits `"web"` and never
emits a `tool` event named `search_web`. Answers that cite `"web"` sources are
not served from the answer cache.

`citations` arrives before the first `token` so the UI can render the sources
panel while the answer streams. `sql` arrives when, and only when, the
`query_database` tool ran.

## `GET /api/health`

```json
{ "status": "ok", "chunks": 2841, "documents": 173,
  "corpusCapturedAt": "2026-10-01T12:00:00Z", "indexWarm": true }
```

## `GET /api/examples`

```json
[ { "label": "Grounded RAG",
    "question": "What is MSC's position on alternative marine fuels?" },
  { "label": "NL to SQL",
    "question": "Which five ports had the most import containers last quarter?" },
  { "label": "Tool call",
    "question": "Track container MSCU1234567." },
  { "label": "Correct refusal",
    "question": "What was MSC's net profit in 2024?" } ]
```

The refusal example is deliberate: the figure is not in the corpus, so the
assistant must say so rather than guess.
