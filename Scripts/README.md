Prefixes:

* aca-* scripts related to deployment to Azure Container Apps
* docker-compose.* scripts related to running services on a local environment
* k8s-* scripts related to deployment to Kubernetes

## Inventory

Every script in this folder, by what it is for. The App Configuration family lives in its own
subfolder and is summarised at the end.

### Local stack

| Script | Purpose |
|---|---|
| `check_health.sh <project> [timeout] [interval]` | Polls docker-compose service health until everything is healthy or the timeout expires. Writes `service-logs/` on failure. |
| `clean.py` | Deletes or drops SQL tables, Mongo collections, Kafka topics and Redis keys across services. `--drop-tables` drops rather than empties; `--bypass-prompt` skips confirmation. |
| `clean-docker.bat` | `clean.py` with the local docker-compose connection settings filled in. |
| `seed.py` | Seeds the HAPI tenant through Admin BFF, using the fixtures in `seed_data/`. |
| `load-sql-data.sql` | Seeds `link-tenant` directly in SQL Server for facility `Test-Hospital`. |
| `load-mongo-data.js` | Loads measure definitions into `link-measureeval` via mongosh. |
| `create-topics-rest.{sh,bat}` | Creates every Kafka topic in `topics.txt` through the Confluent REST Proxy. |
| `validate-topics-rest.bat` | Verifies those topics exist. |
| `update-windows-docker-hosts.ps1` | Adds running container names to the Windows hosts file. Requires an elevated shell. |
| `sftp-setup.sh` | Creates the per-user `data/` directory inside the SFTP container. |

### Build, version and deploy

| Script | Purpose |
|---|---|
| `build_and_push_and_set.py` | Builds and pushes service images to an Azure Container Registry, optionally updating a Kubernetes namespace to the new tag. |
| `set_version.py <major.minor.patch>` | Stamps the version across the `.csproj` files. |
| `set_service_info.py` | Writes commit, build number and product version into the Java YAML and .NET service-info config. |
| `set_kubernetes_services.bat <namespace> <registry> <image>` | Points a Kubernetes namespace at a registry and image. |
| `aca-container-statuses.ps1` | Lists Azure Container App running state and replica bounds. |
| `aca-logs.bat <container> rep\|rev <id>` | Tails Container App logs for a replica or revision. |
| `get_deployed_commit.py <environment>` | Reports the commit currently deployed to `dev-scale`, `scale-test` or `scale-qa`. |
| `list-deploy-changes.py <from> <to>` | Lists the deployment-relevant changes between two git refs. |
| `upload_to_share.py` | Uploads a directory to an Azure File Share. |

### Testing and CI

| Script | Purpose |
|---|---|
| `run-adhoc-reporting-smoke-test.{ps1,sh}` | Runs the `AdhocReportTest` E2E smoke test against a running stack. |
| `get_patient_ids.py --fhir-server-base <url>` | Pulls patient IDs off a FHIR server into a file. |
| `update_pr_coverage.py` | Reports test coverage restricted to the lines a PR changed. |
| `download-swagger-specs.ps1 <version>` | Fetches each service's Swagger/OpenAPI document into `docs/domains/**/openapi.yml`. |

### LINQPad utilities

Run these in LINQPad; they prompt for a path rather than taking arguments.

| Script | Purpose |
|---|---|
| `analyze-bundle.linq` | Reports resource counts and structure statistics for a FHIR bundle. |
| `transactionize-fhir-bundle.linq` | Rewrites bundles to `type: batch` and strips `link`, `meta` and `id` so they can be POSTed. |
| `validation/find-missing-code-systems.linq` | Lists code systems referenced by a validation response but absent from the terminology service. |
| `linux-line-endings.linq` | Converts CRLF to LF, for files that fail in Linux with `$'\r': command not found`. |

### Azure App Configuration

These live in [`AzureAppConfig/`](AzureAppConfig/README.md), which documents them in full -
the catalog checks, the key inventory and the secret scanning. Run them from the repository
root. Exit codes are uniform across the checks: `0` clean, `1` findings, `2` inputs unusable.

| Script | Purpose |
|---|---|
| `export-appconfigs.bat` | Exports a store to the committed `Config/app-config.<env>.json` shape. |
| `compare_aac_exports.py` | Diffs two exports. |
| `validate_aac_secrets.py` | Fails if an export carries a credential. Stdlib only. |
| `validate_app_config_schema.py` | Validates `app-config.yaml` against the schema embedded in itself. |
| `check_required_config.py` | **The CI gate** — every `required: true` key has a row in every store. |
| `reconcile_config_catalog.py` | Where the catalog, the code and the stores disagree, in four buckets. |
| `apply_appconfig_tags.py` | Tags export rows with their consuming services. Writes to `Config/` only, never to Azure. |
| `extract_config_keys.py` | Derives the keys the code actually reads; produces the inventory. |
| `dump_config_symbols.cs` | Roslyn symbol dump feeding the above. Run with `dotnet run --file`. |
| `config_key_matching.py` | Shared rules for "is this key present in a store". |
| `config_findings.py` | Shared `Finding`, severity report, `--strict` rule and file loading. |
| `tests/` | `python -m unittest discover Scripts/AzureAppConfig/tests` |
| `java_config_audit.json` | Hand-maintained audit of the Java bindings. |
