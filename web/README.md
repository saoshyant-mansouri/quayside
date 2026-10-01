# Quayside web

The streaming chat frontend for Quayside: Next.js 15 (App Router), React 19,
TypeScript strict, Tailwind v4. It talks to the API only through same-origin
`/api` calls. Next.js rewrites those to the Azure API, so no API hostname is
ever present in client code.

> Quayside is an independent technical demonstration. It is not affiliated
> with, endorsed by, or operated by MSC. Any operational or shipment data it
> shows is synthetic.

## Run locally

```sh
pnpm install
pnpm dev
```

Open <http://localhost:3000>. With no configuration, `/api/*` is proxied to
`http://localhost:8080`, which is where `Quayside.Api` listens in development.

Quality gates:

```sh
pnpm lint
pnpm typecheck
pnpm build
```

## Environment variables

| variable | where | purpose |
|---|---|---|
| `API_URL` | server, read by `next.config.ts` | Origin of the API that `/api/:path*` is rewritten to. Defaults to `http://localhost:8080`. Required on Vercel: the build fails if it is missing there. |
| `NEXT_PUBLIC_USE_MOCK` | build/dev time | `1` points the client at the scripted mock under `/mock-api` instead of `/api`. Anything else, or unset, uses the real API. |

`.env.example` lists both. Copy to `.env.local` to override.

## Mock mode

`NEXT_PUBLIC_USE_MOCK=1 pnpm dev` runs the whole UI without a backend. The mock
lives in `app/mock-api/` and emits the same SSE contract as the real API
(`docs/api-contract.md`) from scripted scenarios chosen by the question:

| question contains | scenario |
|---|---|
| `delete`, `drop`, `update`, `remove` | `sql` event with `rejected` set |
| `port` | tool calls, citations, SQL with a result grid |
| `track`, `mscu` | operational tool with `uncited: 2` grounding |
| `profit` | refusal with no citations |
| anything else | cited RAG answer |

Asking the same question twice returns `cached: true`. Adding `truncate` to the
question ends the stream without a `done` event. The mock deliberately splits
events across chunk boundaries, uses CRLF for one event and multi-line `data:`
for another, so the parser is exercised against awkward input.

The mock routes return 404 unless `NEXT_PUBLIC_USE_MOCK=1`, so a production
build without the flag does not expose them. The directory is `mock-api` and not
`api/_mock` because Next.js treats a leading underscore as a private folder and
would never route it.

## How the streaming works

```
lib/sse.ts          bytes -> SSE messages
lib/api.ts          SSE messages -> typed ChatEvent
lib/chat-state.ts   ChatEvent -> message state (reducer)
components/chat.tsx owns the request, the AbortController, and dispatches
```

`POST` with a body cannot use `EventSource`, so `parseSse` reads
`fetch(...).body` itself:

- It decodes with a streaming `TextDecoder`, so a multi-byte character split
  across two reads is not corrupted.
- It splits on `\r\n`, `\r` or `\n` and keeps the unfinished tail in a buffer, so
  an event cut in half by the network is completed by the next read. A trailing
  `\r` is held back until the next chunk in case it is half of a `\r\n`.
- Several `data:` lines in one event are joined with `\n`. Comment lines
  (`:`) and unknown fields are ignored.
- If the stream ends mid-event, the pending event is still delivered.

`streamChat` turns messages into `ChatEvent`s, skipping unknown event names and
unparseable JSON so a newer API cannot crash an older client.

Message lifecycle, in `chat-state.ts`: a message starts `streaming` and ends in
exactly one of `complete` (a `done` event arrived), `stopped` (the user pressed
Stop, which aborts the fetch), `interrupted` (the stream closed with no `done`)
or `failed` (an `error` event, a non-2xx response, or a network failure).
Partial text is always kept, and an interrupted or stopped answer is labelled as
incomplete rather than silently presented as finished. Unmounting aborts any
in-flight request.

## Markdown and untrusted output

Model output is untrusted. `react-markdown` never renders raw HTML, and the
tree additionally passes through `rehype-sanitize` with `href` limited to
`http`, `https`, `mailto` and the internal `cite:` scheme. Images are disallowed
entirely, which also removes tracking-pixel exfiltration. External links open
with `rel="noopener noreferrer"`. Nothing uses `dangerouslySetInnerHTML`.

Citation markers are made clickable by a small remark plugin
(`lib/citation-markers.ts`) that rewrites `[n]` text nodes into `cite:n` links,
only when `n` matches a source that actually arrived. Code spans are untouched,
and an unmatched marker such as `[9]` stays plain text. While streaming, a
dangling `[` or `[1` at the end of the text is held back so it never flashes as
literal text before becoming a link.

## Accessibility

- Landmarks: `header` and `main`, with the transcript and each answer's sources as labelled regions. The disclaimer is a
  persistent banner, not dismissible.
- The transcript is `role="log"`. A message is `aria-busy` while it streams, so a
  screen reader announces the finished answer once instead of every token.
- Every control is a real `button` or `a`; focus rings come from one
  `:focus-visible` rule. Clicking `[n]` moves focus to the matching source.
- All transitions and animations are CSS and are collapsed by
  `prefers-reduced-motion`. There is no animation library.
- Light and dark come from CSS custom properties switched by
  `prefers-color-scheme`.

## Deploy to Vercel

Create the Vercel project with **Root Directory** set to `web`, framework
preset Next.js, and set these in the project environment (Production and
Preview):

| variable | value |
|---|---|
| `API_URL` | The API's public origin, for example `https://<app>.<env>.francecentral.azurecontainerapps.io`. No trailing `/api`. |

Do not set `NEXT_PUBLIC_USE_MOCK` on Vercel.

Then, from `web/`:

```sh
vercel pull --yes --environment=production
vercel build --prod
vercel deploy --prebuilt --prod
```

`next.config.ts` fails the build on Vercel when `API_URL` is missing, rather
than shipping a site that proxies to localhost. Response compression is
disabled in `next.config.ts` so a self-hosted `next start` does not buffer the
event stream; on Vercel, the edge handles compression.

The API must allow the Vercel origin only if it is called cross-origin. It is
not: the browser always calls `/api` on the Vercel domain, and Vercel proxies
server-side, so no CORS configuration is needed.
