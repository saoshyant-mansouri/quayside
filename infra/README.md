# Infrastructure

Terraform for the Quayside Azure topology. It mirrors the conventions of an
earlier Azure deployment on the same subscription: partial backend config,
`local.prefix`, one module per concern, cost discipline first.

## Layout

```
infra/
├── bootstrap/            Run ONCE with local state: resource group
│                         rg-quayside-bootstrap, storage account, tfstate
│                         container.
└── terraform/
    ├── backend.tf        Partial backend config, see "State isolation"
    ├── versions.tf       terraform >= 1.9, azurerm ~> 4.0, azapi ~> 2.0, random ~> 3.0
    ├── providers.tf
    ├── variables.tf
    ├── main.tf           module composition
    ├── outputs.tf
    ├── envs/
    │   ├── dev.backend.hcl     prod.backend.hcl
    │   └── dev.tfvars.example  prod.tfvars.example
    └── modules/
        ├── foundation/       resource group, Log Analytics, Application Insights
        ├── openai/           Azure OpenAI account + two model deployments
        ├── sql/              SQL server + one free-offer serverless database
        ├── container_env/    Container Apps Environment
        ├── api_app/          API container app, user-assigned identity, role assignment
        ├── jobs/             ingestion Container App Job
        └── cost_guard/       budget + action group
```

## Order of operations

The state backend cannot provision itself, so:

1. Run `bootstrap/` once with local state.
2. `terraform init` the main root against the storage account it created.
3. `terraform apply` per environment.

```bash
cd infra/bootstrap
terraform init
terraform apply
terraform output storage_account_name

cd ../terraform
export TFSTATE_STORAGE_ACCOUNT=<storage_account_name from above>

terraform init -reconfigure \
  -backend-config=envs/dev.backend.hcl \
  -backend-config="storage_account_name=$TFSTATE_STORAGE_ACCOUNT"

cp envs/dev.tfvars.example envs/dev.tfvars

export TF_VAR_ghcr_pat=...
export TF_VAR_sql_admin_password=...
export TF_VAR_sql_readonly_password=...

terraform apply -var-file=envs/dev.tfvars
```

Swap `dev` for `prod` to target production. `-reconfigure` matters when
switching environments in the same working directory.

## State isolation (dev vs prod)

`backend.tf` is a partial configuration on purpose: it declares
`backend "azurerm" {}` with no `key`. A single hardcoded key would make dev and
prod share one state file, and a dev apply could then destroy production. The
key lives in `envs/<env>.backend.hcl`.

## Secrets

Never written to a `.tfvars` file. The committed files are `*.tfvars.example`
with placeholders. `envs/*.tfvars` is gitignored. Sensitive values
(`ghcr_pat`, `sql_admin_password`, `sql_readonly_password`) come from
`TF_VAR_*` environment variables, sourced from GitHub secrets in CI.
`sql_admin_login` is also a sensitive variable and may be supplied the same
way.

## The one hard subscription limit

`Azure for Students` allows exactly **one Container Apps Environment per
subscription**, and calcio's `cae-calcio-dev` already occupies it. This is not
folklore from a code comment; it is the quota API:

```
GET /subscriptions/{sub}/providers/Microsoft.App/locations/francecentral/usages
  ManagedEnvironmentCount   currentValue = 1   limit = 1
```

So `existing_container_app_environment_id` is set in `envs/dev.tfvars` and the
`container_env` module is skipped. Quayside's container app and job still live
in `rg-quayside-dev`; only the environment is shared.

The cost of sharing: the environment's log destination is fixed at the
environment level, so raw container stdout lands in calcio's Log Analytics
workspace and shares its 5 GB monthly free grant. Application Insights is
SDK-based rather than environment-based, so Quayside's OpenTelemetry traces,
latency and token metrics are unaffected and still go to its own capped
workspace.

By contrast, **Azure OpenAI is not contested.** The subscription allows one
OpenAI account per region and calcio uses DeepSeek, so no Cognitive Services
account exists at all (`az cognitiveservices account list` is empty). Quayside
takes the slot uncontested.

## Cost-critical constraints

Do not change any of these without re-reading the arithmetic in the
architecture document.

| Constraint | Where | Why |
|---|---|---|
| Free-offer SQL, `GP_S_Gen5` capacity 2, `useFreeLimit = true`, `freeLimitExhaustionBehavior = "AutoPause"`, `min_capacity = 0.5`, `autoPauseDelay = 60` | `modules/sql` | The free offer bills nothing. `AutoPause` makes exhaustion pause the database instead of billing overage. Only one database per subscription can hold the offer |
| **ghcr.io**, not ACR | `modules/api_app`, `modules/jobs` registry block | ACR Basic is about $5/month and would be the largest line item. ghcr.io is free for public images, authenticated with a PAT secret |
| `daily_quota_gb = 0.2`, 30-day retention | `modules/foundation` | Log Analytics free ingestion is 5 GB/month. The cap stops verbose logging silently exceeding it |
| `min_replicas = 1` | `modules/api_app` | Scale-to-zero costs a roughly 20 s cold start for every idle visitor, which contradicts the latency budget. This is the one deliberate non-zero cost |
| Managed identity, `local_auth_enabled = false` | `modules/openai`, `modules/api_app` | The API and the job authenticate to Azure OpenAI as a user-assigned identity holding `Cognitive Services OpenAI User`. API keys are disabled on the account, so no OpenAI key exists anywhere to leak or rotate |
| Native `http_scale_rule` | `modules/api_app` | Supported directly by azurerm 4.x |
| `azurerm_container_app_job` | `modules/jobs` | Jobs exist only while running. Manual trigger by default; set `ingest_schedule_cron` for a UTC schedule |
| Azure OpenAI `GlobalStandard` | `modules/openai` | Pay-per-token, cents per demo. Capacity is a rate limit, not a reservation |

## Azure SQL cold-resume consequence

With auto-pause at 60 minutes, a database idle for an hour pauses and the next
connection takes roughly 30 seconds to resume. Connection strings therefore
carry `Connection Timeout=60`. The hot RAG path does not touch SQL, because
the index is hydrated into memory at API start, so only the first
`query_database` call after a long idle pays the resume. When the monthly free
grant is exhausted the database stays paused for the rest of the month rather
than billing.

The API start-up index hydration also reads SQL, so a restart after the
database has paused will wait on the resume too.

## Why the SQL database is created through azapi

azurerm 4.81 does not expose `use_free_limit` or `free_limit_exhaustion_behavior`
on `azurerm_mssql_database`; `terraform validate` rejects both. The database is
therefore an `azapi_resource` of type `Microsoft.Sql/servers/databases` at
api-version `2024-11-01-preview`, which carries `useFreeLimit` and
`freeLimitExhaustionBehavior` in its create call. Creating it with the
free-offer flags atomically avoids a window where a paid database exists. The
server, firewall rules and everything else stay on azurerm. When azurerm gains
the arguments, replace the `azapi_resource` and drop the azapi provider.

## Read-only SQL login

Terraform cannot create a database-level login. After the first apply, connect
as the AAD administrator and run once:

```sql
CREATE LOGIN quayside_reader WITH PASSWORD = '<sql_readonly_password>';
```

then in the Quayside database:

```sql
CREATE USER quayside_reader FOR LOGIN quayside_reader;
ALTER ROLE db_datareader ADD MEMBER quayside_reader;
```

The login name must match `sql_readonly_login` and the password must match
`sql_readonly_password`. The API receives the read-only connection string as a
separate secret and uses it only to execute generated SQL.

## Container Apps Environment

Azure for Students allows one Container Apps Environment per subscription, and
calcio's `cae-calcio-dev` already occupies it. Set
`existing_container_app_environment_id` to that environment's resource ID and
the `container_env` module is skipped; the Quayside apps are then created in
`rg-quayside-dev` but run inside the shared environment. Leave it `null` on a
subscription with no such limit and Terraform creates `cae-quayside-<env>`.

```bash
az containerapp env show --name cae-calcio-dev --resource-group rg-calcio-dev --query id -o tsv
```

The shared environment logs to calcio's Log Analytics workspace, not to
Quayside's capped one. Application telemetry still goes to Quayside's
Application Insights.

## Application configuration

Terraform injects these into the API app and the ingestion job. They use the
ASP.NET Core `__` section separator.

| Variable | Source |
|---|---|
| `AZURE_CLIENT_ID` | user-assigned identity client ID, so `DefaultAzureCredential` selects it |
| `AzureOpenAI__Endpoint` | OpenAI account endpoint |
| `AzureOpenAI__ChatDeployment`, `AzureOpenAI__EmbeddingDeployment` | deployment names (`gpt-5-mini`, `text-embedding-3-small`) |
| `ConnectionStrings__Sql` | secret `sql-connection-string` |
| `ConnectionStrings__SqlReadOnly` | secret `sql-readonly-connection-string` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | secret `app-insights-connection-string` |
| `Cors__AllowedOrigins__0`, `__1`, ... | `cors_origins` |
| `ASPNETCORE_URLS` | `http://+:8080` (API only) |

The ingestion job runs the same `quayside-api` image with `args = ["ingest"]`
(variable `ingest_args`), so the image's entrypoint must dispatch on that
first argument. Start it manually with:

```bash
az containerapp job start --name caj-quayside-dev-ingest --resource-group rg-quayside-dev
```

Model versions are variables (`chat_model_version`, `embedding_model_version`)
so a bump is a one-line change. Quota for `GlobalStandard` capacity 100 must
exist in francecentral; lower `openai_deployment_capacity` if the subscription
quota is smaller.

## Cost guard

`modules/cost_guard` is a straight port of calcio's: a monthly consumption
budget on `rg-quayside-<env>` and an action group. Forecasted 50 % and actual
80 % and 100 % notifications email `alert_emails`.

The scale-to-zero action does not live in Azure. Azure budget alerts lag spend
by hours, and an Automation runbook cannot be deployed on this subscription
because the Automation-supported regions and the subscription's region policy
do not overlap (the reason calcio documented). Calcio's stop is a scheduled
GitHub Actions workflow that reads the budget and month-to-date cost through
the Cost Management API and, when spend reaches the budget, runs:

```bash
az containerapp update --name ca-quayside-dev-api --resource-group rg-quayside-dev \
  --min-replicas 0 --max-replicas 0
```

That workflow belongs in `.github/workflows/`, outside this directory, and
still has to be written for Quayside. To resume after a stop, re-run
`terraform apply` to restore replica counts. Nothing is deleted.

## Quality gate

```bash
terraform fmt -recursive -check
terraform init -backend=false && terraform validate
```

Run in both `infra/bootstrap` and `infra/terraform`.

## Database sizing, and the ceiling that keeps it free

The database runs at the **maximum capacity that still cannot bill**:
`GP_S_Gen5` with `max_vcores = 4`, 32 GB (the free offer's cap), `minCapacity`
0.5 and `autoPauseDelay` 60 minutes.

Four vCores is a hard ceiling, not a preference. Querying the location
capabilities for every serverless SKU that supports a free limit:

```
GP_S_Gen5_1 / _2 / _4    AutoPause, BillOverUsage
GP_S_Gen5_6 and above    BillOverUsage only
```

From six vCores upward Azure stops offering `AutoPause`, so exhausting the
100,000 free vCore-seconds would start charging rather than stopping. A
`validation` block on `max_vcores` rejects anything outside 1, 2 and 4 so that
ceiling cannot be raised by accident.

Raising the maximum from 2 to 4 costs nothing at rest. Serverless bills per
vCore-second actually consumed, and `minCapacity` stays at 0.5, so an idle
database is unchanged. A higher ceiling only lets bursty work — corpus
ingestion, the first `query_database` after a resume — finish sooner, which
tends to consume *fewer* vCore-seconds for the same work.

## Isolation from the other project on this subscription

The subscription also hosts `calcio`. Everything that holds data or identity is
separate; one piece of shared infrastructure is unavoidable.

| Resource | Quayside | Shared with calcio |
|---|---|---|
| Resource group | `rg-quayside-dev` | no |
| Terraform state | `rg-quayside-bootstrap`, own storage account, own state key | no |
| Azure SQL server and database | own server, own database | no |
| Azure OpenAI account | own account, `local_auth_enabled = false` | no |
| Application Insights | own component | no |
| Log Analytics workspace | own workspace, capped at 0.2 GB/day | no |
| Storage account | own account | no |
| Managed identity | own user-assigned identity, scoped to Quayside's resources only | no |
| **Container Apps Environment** | `cae-calcio-dev` | **yes — unavoidable** |

The environment is shared because Azure for Students permits exactly one per
subscription (`ManagedEnvironmentCount  currentValue=1  limit=1`) and calcio
already holds it. Creating a second one fails; the alternatives were a separate
App Service plan at about $13/month, or relocating a live site.

What sharing actually leaks is narrow: the environment's log destination is
fixed at the environment level, so raw container stdout goes to calcio's Log
Analytics workspace and shares its free grant. It is not a data path. No
Quayside database, model endpoint, credential, secret or identity is reachable
from calcio, and Application Insights is SDK-based rather than
environment-based, so every trace, latency figure and token count Quayside
records still lands in its own workspace.
