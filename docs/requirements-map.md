# Requirements traceability

Every row maps a line from one of the three MSC job descriptions in
[`job-ads.md`](job-ads.md) to where this repository answers it. Rows marked
*planned* are not built yet; see **Status** in the root README.

## AI Engineer

| Requirement | Where |
|---|---|
| Generative-AI conversational interface | `src/Quayside.Api` SSE chat endpoint, `web/` streaming UI — *planned / built* |
| Orchestrate RAG and function calling | Semantic Kernel auto function calling over six typed tools — *planned* |
| RAG over large document collections | Hybrid retrieval (SIMD cosine + BM25, RRF, MMR) over 190 captured documents — *planned* |
| SQL-to-LLM, 200+ tables | 226-table spec in `data/schema/`; schema-card retrieval + one-hop FK expansion + ScriptDom validation — *spec built, pipeline planned* |
| Tooling for action execution via APIs | `track_container`, `find_schedules`, `get_vessel`, `get_port` over the demo schema — *planned* |
| LLM frameworks (Semantic Kernel) | `Microsoft.SemanticKernel` 1.80.1 in `src/Quayside.Infrastructure` |
| Azure platform and key cloud services | Container Apps, Azure SQL, Azure OpenAI, Log Analytics, App Insights, managed identity — `infra/` |
| CI/CD using Azure DevOps | `azure-pipelines.yml` alongside GitHub Actions — *planned* |
| Microsoft Copilot Studio (low-code) | Not built. Stated plainly rather than faked. |

## Developer

| Requirement | Where |
|---|---|
| C# with .NET 8+ | .NET 10 across six projects |
| RESTful APIs / backend services | `src/Quayside.Api`, minimal API |
| SQL Server: schema design, T-SQL | 226 tables, 451 FKs, generated idempotent DDL, indexed FK columns |
| Query optimisation / performance | FK indexing, row caps, 5 s command timeout, in-memory index to keep SQL off the hot path |
| Unit and integration testing | `tests/Quayside.UnitTests` (xUnit) — *planned* |
| Azure DevOps CI/CD | `azure-pipelines.yml` — *planned* |
| Git workflow | Grouped, explained commits on `main` |
| Dependency injection / IoC | Constructor injection throughout; no service locator |
| async/await, Task-based patterns | Async with `CancellationToken` plumbed to the edges |
| WPF desktop | **Not built.** WPF cannot be compiled on macOS, so it would have been a CI-only artefact nobody could demo. Called out rather than hidden. |

## Full Stack Developer

| Requirement | Where |
|---|---|
| Software architecture practices | Inward-only dependencies, enforced as an I/O rule and asserted by a test |
| Microsoft .NET C# | As above |
| SQL Server 2012+ | Azure SQL Database |
| React | React 19 in `web/` |
| JavaScript / CSS | TypeScript strict, Tailwind 4, no animation library |
| Entity Framework | EF Core 10 for migrations and writes; Dapper where EF cannot express `VECTOR_DISTANCE` |
| Delivery of distributed enterprise applications | Container Apps + Vercel, same-origin API proxy, no hostname in client code |
| DevOps implementation | Terraform, GitHub Actions, cost guard, capped log ingestion |
| MVC / WCF / Angular / Kendo UI | Not used. These are legacy-stack items; the equivalent competence is shown in the modern stack above. |
