# CI/CD

Three GitHub Actions workflows, one Dockerfile, and an Azure DevOps pipeline
that mirrors them. Resource names come from `infra/terraform` and are not
parameters: `rg-quayside-dev`, `ca-quayside-dev-api`, `caj-quayside-dev-ingest`,
`budget-quayside-dev`.

## Workflows

### `ci.yml` - the quality gate

Runs on every push and every pull request. A new push to the same ref cancels
the run in flight.

| Job | What it enforces |
|---|---|
| `dotnet` | .NET 10 SDK; `dotnet restore`, `build -c Release`, `test`, and `dotnet format --verify-no-changes` on `Quayside.slnx`. NuGet cached on the `.csproj` hashes |
| `web` | node 22, pnpm 10 (version read from `web/package.json`); `pnpm install --frozen-lockfile`, `lint`, `typecheck`, `build` in `web/`. pnpm store cached on `web/pnpm-lock.yaml` |
| `terraform` | Terraform 1.9.8; `fmt -check -recursive`, `init -backend=false`, `validate`, matrixed over `infra/terraform` and `infra/bootstrap` |
| `workflows` | `actionlint` over `.github/workflows` |
| `gate` | Fails unless all four above succeeded. Make `CI gate` the single required status check in branch protection |

### `deploy.yml` - build, push, roll out

Runs on push to `main` that touches `src/**`, `data/**`, `Dockerfile`,
`.dockerignore` or the workflow itself, and on manual dispatch. `data/**` is
included because the corpus and schema spec are baked into the image. Runs are
serialised by `concurrency: deploy` and never cancelled mid-rollout.

1. `build-and-push` builds `Dockerfile` for `linux/amd64` and pushes
   `ghcr.io/<owner>/quayside-api:<full sha>` and `:latest` with the repo's
   `GITHUB_TOKEN`. The owner is lowercased, because OCI references must be.
2. `deploy` logs in to Azure with OIDC (no client secret) and runs
   `az containerapp update` on `ca-quayside-dev-api` and
   `az containerapp job update` on `caj-quayside-dev-ingest`, both pinned to
   the SHA tag.
3. It then polls `https://<api fqdn>/api/health` for up to five minutes and
   fails the run if the new revision never answers.

Deploy does not wait for `ci.yml`; it builds the image from the same commit but
does not run the tests. The gate is branch protection: merge through a pull
request that requires `CI gate`.

The Terraform apps carry `ignore_changes` on the container image, so deploys
and `terraform apply` do not fight over it.

### `cost-guard.yml` - the stop half of cost protection

`modules/cost_guard` only emails at 50 / 80 / 100 % of the budget. This
workflow does the stopping, on a 30-minute schedule and on manual dispatch.

1. Reads the budget amount from `budget-quayside-dev` through the Consumption
   API, so the figure is never duplicated here.
2. Reads month-to-date `ActualCost` for `rg-quayside-dev` through the Cost
   Management API.
3. If spend is at or above the budget, runs
   `az containerapp update --name ca-quayside-dev-api --resource-group rg-quayside-dev --min-replicas 0 --max-replicas 0`.
   It skips the update when the app is already at zero.

It fails closed. Calcio's version treats a failed cost query as zero spend and
a missing budget as "nothing to check"; both let a runaway bill through
silently. Here a failed login, a missing budget, a throttled or erroring cost
query (retried four times first) or a non-numeric result all turn the run red.
Only a successful query that returns no rows, which Azure does before the first
charge of a month, counts as zero spend.

To resume after a stop, re-run `terraform apply` in `infra/terraform` to
restore the replica counts. Nothing is deleted. Scheduled workflows run only
from the default branch, and GitHub disables them after 60 days without
repository activity; a red cost-guard run is worth reading.

## Required GitHub configuration

Repository secrets (Settings > Secrets and variables > Actions):

| Secret | Value |
|---|---|
| `AZURE_CLIENT_ID` | Application (client) ID of the Entra app registration used for OIDC |
| `AZURE_TENANT_ID` | Directory (tenant) ID |
| `AZURE_SUBSCRIPTION_ID` | The `Azure for Students` subscription ID |

No repository variables are needed. `GITHUB_TOKEN` is provided automatically.

GHCR: the workflow grants itself `packages: write`. After the first push, open
the package `quayside-api` under the owner's Packages and confirm the repository
has write access (it is linked automatically through the
`org.opencontainers.image.source` label). The Container Apps pull with
`TF_VAR_ghcr_pat`, a classic PAT with `read:packages`, so the package may stay
private. Making it public removes the need for the PAT at runtime.

## Federated credential for OIDC

One Entra app registration with no client secret. The calcio registration can be
reused: add a second federated credential and a second role assignment to it.

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
OWNER=saoshyant-mansouri
REPO=quayside

APP_ID=$(az ad app create --display-name gh-quayside-deploy --query appId -o tsv)
az ad sp create --id "$APP_ID"

az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "quayside-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:'"$OWNER/$REPO"':ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

SCOPE=/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-quayside-dev
az role assignment create --assignee "$APP_ID" --role Contributor --scope "$SCOPE"
az role assignment create --assignee "$APP_ID" --role "Cost Management Reader" --scope "$SCOPE"
```

The subject is exact. Every workflow here runs from `main` (deploy on push, the
cost guard on schedule or dispatch), so a single `ref:refs/heads/main`
credential covers all of them. The resource group must exist first, so run the
Terraform apply before the role assignments. Then set `AZURE_CLIENT_ID` to
`$APP_ID`, plus the tenant and subscription IDs.

## Dockerfile

One image serves both the API and the ingest job.

- Build stage on the full .NET 10 SDK. Project files are copied and restored
  before sources, so the restore layer is cached until a `.csproj` changes;
  publish then runs with `--no-restore`.
- ReadyToRun is on for cold start. Native AOT is not used: Semantic Kernel and
  EF Core are not AOT-safe. The runtime identifier follows the target
  architecture (`linux-x64` or `linux-arm64`), so a local build on Apple
  silicon works as well as the amd64 CI build.
- Runtime stage is `aspnet:10.0-noble-chiseled`, which has no package manager
  and no shell, and runs as the non-root `app` user (uid 1654). It listens on
  8080 through the base image default, matching `ASPNETCORE_URLS` in
  `modules/api_app`.
- The API is published to `/app/api`, the ingest job to `/app/ingest`, and
  `data/` to `/app/data`.
- Dispatch: a chiselled image cannot run a shell script, so a static
  `busybox` binary is copied in and the entrypoint is a short `sh` script
  that `exec`s `Quayside.Ingest.dll` when the first argument is `ingest` and
  `Quayside.Api.dll` otherwise. `modules/jobs` passes `args = ["ingest"]`.
  The only shell in the image is that busybox; nothing else in the image can
  start one. Moving the dispatch into C# would remove it.

Verified with `docker build .` on this repo: ingest dispatch, API on port 8080
and the non-root user all behave as described.

## Azure DevOps: `azure-pipelines.yml`

Provided as the Azure DevOps equivalent of `ci.yml` and `deploy.yml`, because
all three MSC job ads name Azure DevOps. **It has not been run.** It is
well-formed YAML written against the Azure Pipelines schema (`UseDotNet@2`,
`NodeTool@0`, `Cache@2`, `Docker@2`, `AzureCLI@2`, a gated deployment job), but
no Azure DevOps organization has executed it.

If it is ever connected, it expects:

- a variable group `quayside-deploy` with `ghcrOwner` (lowercase),
  `ghcrServiceConnection` and `azureServiceConnection`;
- a Docker Registry service connection for `ghcr.io` (a PAT with
  `write:packages`);
- an Azure Resource Manager service connection using workload identity
  federation, with the same roles as above;
- an environment named `quayside-dev`.

Known differences from GitHub Actions: Azure Pipelines cannot filter a single
stage by path, so Deploy runs on every push to `main` that passes CI, and there
is no cost-guard equivalent.

## Runtime base image: why `-extra`

The runtime stage uses `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`
rather than plain `-chiseled`. The plain chiselled image ships without ICU, so
.NET starts in globalization-invariant mode and `Microsoft.Data.SqlClient`
throws `System.NotSupportedException: Globalization Invariant Mode is not
supported` on the first connection attempt. The ingest job failed this way on
its first real run against Azure SQL.

`-extra` adds ICU and tzdata and keeps the rest of the chiselled surface: still
no shell of its own, still non-root.
