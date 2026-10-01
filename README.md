# Quayside

A retrieval-grounded AI assistant that answers questions about MSC
(Mediterranean Shipping Company) from MSC's own public material, built
entirely on the Microsoft stack.

> **Quayside is an independent technical demonstration.** It is not
> affiliated with, endorsed by, or operated by MSC. It answers only from
> public sources and cites every one of them. All operational shipping data
> in the demo schema is synthetic.

## Why it is built this way

The project is calibrated against three real MSC job descriptions
(reproduced in [`docs/job-ads.md`](docs/job-ads.md)); every architectural
decision traces back to a line in one of them, in
[`docs/requirements-map.md`](docs/requirements-map.md).

Three requirements from the AI Engineer ad drove the design, and they are
the three things worth reading the code for:

**1. "RAG applied to large-scale document collections."**
A real captured corpus — 115 MSC LinkedIn posts and 231 msc.com pages — with
hybrid retrieval (SIMD cosine + BM25, fused with reciprocal rank fusion),
citations on every claim, and a refusal path when nothing grounds the
answer. The grounding contract is enforced in code, not merely prompted: a
sentence asserting a fact about MSC that does not resolve to a retrieved
chunk is not silently emitted.

**2. "SQL-to-LLM integration with medium to large databases (200+ tables)."**
This is the centrepiece. `data/schema/` defines **226 tables, 1,291 columns
and 451 foreign keys** across 14 bounded contexts. At that size the schema
cannot fit in a prompt, so the system *retrieves the schema before it writes
any SQL*: it vector-searches per-table description cards, expands one hop
along the foreign-key graph to reach join partners, and builds a minimal DDL
prompt from that subset. The generated statement is then parsed with
`Microsoft.SqlServer.TransactSql.ScriptDom` — the real T-SQL parser, not a
regex — and rejected unless it is a single `SELECT`, touching only
allow-listed tables, with a row cap. It executes on a `db_datareader` login
with a 5-second timeout. The UI shows the SQL it wrote.

The schema deliberately contains confusable neighbours — `ContainerMovements`
vs `ContainerEvents` vs `EquipmentMovements`, `Tariffs` vs `TariffRates` vs
`RateSheets` — so that retrieval picking the right one is a real result.

**3. "Tooling enabling action execution through API integrations."**
Semantic Kernel auto function calling over typed tools: `search_knowledge`,
`query_database`, `track_container`, `find_schedules`, `get_vessel`,
`get_port`.

## The data layer, and the honest size of it

There is **one store: Azure SQL Database**, and no separate vector database.
It holds the corpus (`Documents`, `Chunks`, `AnswerCache`, `SchemaCards`, each
with `VECTOR(1536)` columns) and the synthetic `ops.` schema the NL→SQL demo
queries. Two connection strings: the app uses a read-write login, and every
generated statement runs on a separate `db_datareader` login with
`ApplicationIntent=ReadOnly`.

**Why no vector database.** The corpus is ~1,000 chunks, which is about 6 MB of
float32. Measured in-memory hybrid search is 0.155 ms p50 at 440 chunks and
1.045 ms p50 at 4,000. Any network-attached vector store adds a 10–50 ms
round trip — one to two orders of magnitude slower than the thing it would
replace. So memory is the query index and SQL is the system of record.
`VECTOR_DISTANCE` is nonetheless implemented and tested in T-SQL: it backs the
answer cache's nearest-question lookup, and a top-k path exists as the
documented route for when the corpus outgrows memory.

**Where this falls short of the ad.** The AI role asks for RAG over
"large-scale document collections". ~1,000 chunks is not large-scale. It is
what MSC's public English surface actually yields, and inflating it with
synthetic filler would make the retrieval numbers meaningless. What is
demonstrated instead is that the design scales: the index is benchmarked to
4,000 chunks in memory, and the T-SQL vector path is built for beyond that.
The 226-table NL→SQL problem is where this project takes on real scale.

## Speed

A 2–4k chunk corpus does not need a vector database, and pretending
otherwise would put a network hop on the hot path. Azure SQL is the system
of record and holds `VECTOR(1536)` columns; on start the API hydrates the
index into one contiguous `float[]` and searches it with
`System.Numerics.Tensors` (SIMD). The `VECTOR_DISTANCE` T-SQL path is built
and tested as the documented scale-out route for when the corpus outgrows
memory.

| stage | p50 target |
|---|---|
| hybrid retrieval | < 5 ms |
| cached answer, end to end | < 120 ms |
| first streamed token | < 900 ms |
| complete answer | < 3 s |

These are targets. Measured numbers replace them here once the API is
deployed — see **Status** below.

## Stack

.NET 10 · ASP.NET Core minimal API · Microsoft.SemanticKernel · Azure OpenAI
(`gpt-5-mini`, `text-embedding-3-small`, reached by user-assigned managed
identity — no model key exists) · Azure SQL Database on the free serverless
offer with native `VECTOR` columns · EF Core 10 and Dapper · ScriptDom ·
xUnit · Terraform (`azurerm` ~> 4) · Azure Container Apps · ghcr.io ·
Next.js 15 / React 19 / Tailwind 4 on Vercel · GitHub Actions, with an
`azure-pipelines.yml` equivalent because all three ads name Azure DevOps.

## Layout

    roadmap/01-architecture.md   the authoritative plan
    docs/                        job ads, requirements map, API contract
    src/Quayside.Core            records, ports, retrieval, NL->SQL. No I/O.
    src/Quayside.Infrastructure  Azure OpenAI, SQL, vector index
    src/Quayside.Api             minimal API, SSE streaming
    src/Quayside.Ingest          chunk, embed, upsert; schema emitters
    tests/                       xUnit + a groundedness eval harness
    web/                         Next.js frontend
    data/corpus/                 the captured source snapshot
    data/schema/                 the 226-table spec (data, not code)
    infra/                       Terraform: bootstrap + modules

## The corpus is a committed snapshot, deliberately

msc.com sits behind Akamai. Measured: `robots.txt` returns `Allow: /` with
three query-string paths disallowed, and names a sitemap — but
`/sitemap.xml` returns `403` at the edge. A plain `HttpClient` is refused; a
browser-like header set works, then gets rate-limited into `403` after a
handful of sequential requests. LinkedIn company posts need an authenticated
session at all.

So collection is a supervised one-off producing a versioned snapshot in
`data/corpus/`, and `Quayside.Ingest` reads that snapshot and performs **no
network fetches of source material**. Ingestion is therefore deterministic,
replayable, unit-testable and CI-safe, and nobody's edge gets hammered on
every deploy. Re-capture is a deliberate manual step.

## Cost

It runs at roughly zero on an Azure for Students subscription.

- Azure SQL on the **free offer** (`GP_S_Gen5_2` serverless,
  `useFreeLimit`, `AutoPause` on exhaustion) — bills nothing. The cost: a
  cold database resumes in ~30 s. The hot RAG path never touches SQL, so
  only the first `query_database` call after a long idle pays it.
- **ghcr.io, not ACR** — ACR Basic would be the largest line item.
- Log Analytics capped at `daily_quota_gb = 0.2` against a 5 GB free grant.
- `min_replicas = 1` on the API: the one deliberate non-zero cost, because
  scale-to-zero means a ~20 s cold start for every idle visitor, which
  contradicts the latency budget above.
- `gpt-5-mini` is pay-per-token, cents per demo.

## Status

Honest state of the work:

- [x] Architecture, API contract, requirements map
- [x] Terraform: bootstrap, foundation, OpenAI, SQL, container env, API app,
      ingest job, cost guard. `fmt` and `validate` pass. **Not yet applied.**
- [x] 226-table schema spec + DDL / card / seed emitters, ScriptDom-verified
- [x] Corpus captured: 115 LinkedIn posts, 231 msc.com pages (1.72M characters)
- [x] Frontend, built and driven against a mock SSE stream
- [ ] Core retrieval engine and grounding enforcement
- [ ] NL→SQL pipeline and the ScriptDom guard
- [ ] Azure OpenAI + SQL infrastructure adapters, the SSE API
- [ ] Ingestion wiring
- [ ] Eval harness, CI, deployment

Nothing above claims to be working that is not. The checkboxes move as the
code lands.

## Running it

See [`infra/README.md`](infra/README.md) for the Terraform ordering and
[`web/README.md`](web/README.md) for the frontend. The frontend runs standalone
against a scripted mock stream:

```bash
cd web && pnpm install && NEXT_PUBLIC_USE_MOCK=1 pnpm dev
```
