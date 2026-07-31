Prefixes:

* aca-* scripts related to deployment to Azure Container Apps
* docker-compose.* scripts related to running services on a local environment
* k8s-* scripts related to deployment to Kubernetes

## Inventory

Every script in this folder, by what it is for. The App Configuration family is summarised here
and documented in full further down.

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

Detailed in [the section below](#azure-app-configuration-tooling). Exit codes are uniform across
the checks: `0` clean, `1` findings, `2` inputs unusable.

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
| `tests/` | `python -m unittest discover Scripts/tests` |
| `java_config_audit.json` | Hand-maintained audit of the Java bindings. |

## Azure App Configuration tooling

The JSON files under `Config/` are exports of the Azure App Configuration stores,
committed to this **public** repository.

* `export-appconfigs.bat <app-config-name> <output-directory>` produces an export.
* `compare_aac_exports.py <left> <right>` diffs two exports.
* `validate_aac_secrets.py [paths] [--strict]` fails if an export contains a
  credential. Defaults to `Config/*.json`.

### Configuration key inventory

Two tools derive the set of configuration keys the code actually reads, so the catalog can be
checked against reality rather than trusted:

```powershell
# 1. Roslyn symbol dump (file-based app - no project, needs .NET 10 SDK)
dotnet run --file Scripts/dump_config_symbols.cs -- DotNet Scripts/config_symbols.json

# 2. Call-site scan + service attribution + inventory
python Scripts/extract_config_keys.py
```

Producing `Config/config-key-inventory.json` (machine-readable, consumed by the reconciler and
the tagging tool) and `docs/config-key-inventory.md` (the human reference).

Only the markdown is committed. Both JSON files are gitignored: a committed derived file
drifts from the code it describes the moment someone adds a `GetSection` call, which is the
exact failure this tooling exists to detect. Regenerate before using either.

The split is deliberate. Four things cannot be read reliably from text, and Roslyn resolves
all of them from the symbol model:

* Section names are declared both as `const string` and as `public static string`
  (`KafkaConstants.SectionName`, `ServiceRegistry.ConfigSectionName`).
* Six different classes declare a member named `SectionName`. Resolving that without a symbol
  table files one section's keys under another section's name.
* `ExternalBlobStorageSettings` inherits `ConnectionString` and `BlobContainerName` from
  `BlobStorageSettings`.
* `TelemetrySettings.EnableOtelCollector` is a public **field**, not a property.
  `ConfigurationBinder` binds properties only, so the value set for it in all three stores
  can never take effect.

Python keeps the call-site scanning, the `.csproj` ProjectReference walk for service
attribution, and the report generation.

`Scripts/java_config_audit.json` is a hand-maintained audit of the Java bindings - 15
`@ConfigurationProperties` classes, 12 `@Value` sites and the `logback-spring.xml`
`springProperty` reads. Update it when those change; it is small enough not to warrant a
parser.

### Catalog checks

```powershell
# Does app-config.yaml conform to the JSON Schema embedded in itself?
python Scripts/validate_app_config_schema.py

# Does every required: true key have a row in every environment store?
python Scripts/check_required_config.py

# Where do the catalog, the code and the stores disagree?
python Scripts/reconcile_config_catalog.py            # all four buckets
python Scripts/reconcile_config_catalog.py --bucket D # required but absent

python -m unittest discover Scripts/tests             # tests for the matching rules
```

`check_required_config.py` also enforces two invariants on store labels: a label containing
`:` is rejected, because the `<Service>:<Environment>` tier does not resolve
(`ExternalConfigurationExtension.cs:64` concatenates the environment object rather than its
name); and a label absent from `serviceMeta` is rejected, because no service selects it.

It also warns if a store stops pinning `Serilog:WriteTo:<n>:Name`. Serilog addresses sinks
positionally, and every `appsettings.json` ships `[GrafanaLoki, Console]` -- the opposite of
what the catalog's `Serilog:WriteTo:1:Args:uri` assumes. The store's `Name` rows are what make
the pairing correct; remove them and logging silently reverts to the file's ordering.

**`check_required_config.py` is the gate; `reconcile_config_catalog.py` is the investigation.**
They overlap deliberately: the reconciler's "required but absent" bucket asks the same question
the check does. The check stays small and answers one question so it can be trusted as a CI
gate; the reconciler is a four-bucket exploration with heuristics about which schemas are
framework-owned, which is not something a gate should carry. They call the same matching
functions, so they cannot give different answers.

Two shared modules keep the family consistent:

* `config_key_matching.py` — the rules for "is this key present in a store". Java dotted/slash
  notation, array elements, `{Placeholder}` templates, JSON blobs the provider flattens, Spring
  relaxed binding, and label scoping.
* `config_findings.py` — `Finding`, the ERROR/WARN report, the `--strict` exit-code rule, and
  file loading. Three scripts here gate CI, so a single definition means changing `--strict`
  behaviour is one edit rather than three files and a hope that none was missed.

Exit codes are uniform: `0` clean, `1` findings, `2` the inputs could not be read — so a
caller can tell "did not run" from "ran and found problems".

These need **PyYAML**; the secret scanner is stdlib-only.

### Secret scanning

App Configuration does not stop anyone storing a literal credential, so an export
can carry one into permanent git history. `validate_aac_secrets.py` gates that:

```powershell
python Scripts/validate_aac_secrets.py "Config/*.json"
python Scripts/validate_aac_secrets.py "Config/*.json" --strict   # warnings fatal
```

It reports **errors** for values matching a known credential shape (storage
account key, inline password, Mongo URI with credentials, PEM block, JWT, Slack
webhook, ...) and for Key Vault references whose `content_type` is not the
`keyvaultref` type -- App Configuration serves those as a literal JSON string
instead of resolving them, so the service reads `{"uri": "..."}` as its password.

It reports **warnings** for secret-shaped keys holding a plain literal, for
malformed entries, and for duplicate `(key, label)` pairs. Not every such value is
a credential, so these are surfaced for review rather than failing the build.

A few keys are named `...ConnectionString` but hold only a bare `host:port` --
`ConnectionStrings:Redis` and `ResourceCache:Redis:ConnectionString`. Both are
assigned to `ConfigurationOptions.EndPoints` with the password supplied separately
from Key Vault, so they are listed in `ENDPOINT_ONLY_KEYS` and exempt from the
secret-shaped-key warning. They are still checked for credential shapes, and they
warn if given comma-delimited StackExchange.Redis config syntax, which
`EndPoints.Add()` cannot parse. Add to that list only when the code assigns the
value to an endpoint rather than parsing it as a connection string.

Run automatically in two places:

* **CI** -- `.github/workflows/appconfig-secret-scan.yml`, on every PR and push to
  `dev`, `main`, `release/**`, and `hotfix/**`.
* **Pre-commit** -- `.githooks/pre-commit` validates the *staged* content of any
  `Config/*.json`. Enable it per clone with:

  ```powershell
  git config core.hooksPath .githooks
  ```

  Bypass a false positive with `git commit --no-verify`; CI still runs the check.

If a real credential is ever committed, **rotate it** -- deleting the line does
not remove it from git history.
