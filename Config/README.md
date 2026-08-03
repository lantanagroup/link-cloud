# Azure App Configuration exports

The JSON files in this folder are exports of the Azure App Configuration stores. The set
is declared in `/app-config.yaml` under `environments`; the tooling reads it from there,
so adding a store is one catalog line plus its export:

| File | Store | Key Vault |
|---|---|---|
| `app-config.dev.json` | `nhsnlink-ac-dev` | `link-vault` |
| `app-config.qa.json` | `nhnslink-ac-qa` | `nhsnlink-kv-qa` |
| `app-config.test.json` | `nhnslink-ac-test` | `nhsnlink-kv-test` |

> Note the store names for qa and test are spelled `nhnslink`, not `nhsnlink` - only dev
> uses `nhsnlink`. That transposition is in the actual Azure resource names, so
> anything addressing a store must use it verbatim. The Key Vaults spell it the other way
> round (`nhsnlink-kv-*`), and dev's vault does not follow the pattern at all.

A fourth store, `nhnslink-ac-qa2`, exists in Azure but holds no key-values yet. It joins
the table and the catalog's `environments` block once LEGLINK-775 imports into it; until
then enforcing it would report every required key as missing.

They are committed to a **public** repository. Nothing in them may be a credential. See
[Guardrails](#guardrails) below.

This is the companion to `/app-config.yaml`, which is the *catalog* — the authoritative list
of which keys exist and what they mean. These files are the *values* for each environment.
The full narrative version of this document is `docs/design-appconfig.html`.

## File format

Each file is an `az appconfig kv export` result: a single `items` array, one object per row.
This is the exact command that produces one, wrapped by
`Scripts/AzureAppConfig/export-appconfigs.bat`:

```powershell
az appconfig kv export `
    --name nhsnlink-ac-dev `
    --destination file `
    --path Config/app-config.dev.json `
    --format json `
    --profile appconfig/kvset `
    --label "*" `
    --auth-mode login `
    --yes
```

Two of those flags decide whether the result is usable at all. `--profile appconfig/kvset`
produces the `items` shape below, with a label and content type per row; the default profile
writes a nested configuration tree carrying no labels, which cannot be round-tripped.
`--label "*"` exports every label, and is only accepted alongside that profile — omit it and
you get only the rows with no label. Substitute the store name and output path from the table
above; note the spelling differs between dev and the rest.

```json
{
  "items": [
    {
      "key": "ConnectionStrings:DatabaseConnection",
      "value": "{\"uri\":\"https://link-vault.vault.azure.net/secrets/link-report-database-connection-string\"}",
      "label": "Report",
      "content_type": "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8",
      "tags": {}
    }
  ]
}
```

A row is identified by the pair **(key, label)** — the same key may appear several times
under different labels. `content_type` is load-bearing, not decoration: it selects how the
value is interpreted.

| `content_type` | Meaning |
|---|---|
| *(empty)* | Plain string |
| `application/json` | Flattened into child keys — see [JSON blobs](#json-blobs) |
| `application/vnd.microsoft.appconfig.keyvaultref+json` | A Key Vault reference; the provider resolves it to the secret's value |
| `application/vnd.microsoft.appconfig.ff+json` | A feature flag |

A Key Vault reference whose `content_type` is *not* the `keyvaultref` type is served as the
literal JSON text `{"uri": "..."}`, so the service would use that string as its password.
This is silent and the scanner treats it as an error.

## Key notation by runtime

The two runtimes read different rows from the same store, in different notations. This is by
design.

**.NET** reads colon-delimited rows matching `appsettings.json` section paths:

```text
KafkaConnection:BootstrapServers:0
Authentication:Schemas:Cookie:HttpOnly
```

**Java** reads slash-prefixed rows. The Spring Cloud Azure provider appends `*` to the
configured `key-filter` of `/`, giving a server-side filter of `/*`; it then strips the
leading slash and converts remaining slashes to dots:

```text
/spring/datasource/url   ->  spring.datasource.url
/link/report/base-url    ->  link.report.base-url
```

So a Java service never sees the colon-delimited rows at all — they do not match `/*`.

`app-config.yaml` records Java keys in the **dotted Spring form** while the store holds the
**slash form**. Both are correct; the transform between them is total and mechanical.

### JSON blobs

A row with `content_type: application/json` is flattened into child properties by both
providers. One row:

```text
key:   /authentication
value: {"anonymous": false, "authority": "https://dev-demo.nhsnlink.org", "adminEmail": ""}
```

supplies three Spring properties — `authentication.anonymous`, `authentication.authority`
and `authentication.adminEmail`. Searching the export for `authentication.authority` as a
key will not find it.

## Labels

Every service selects **unlabeled rows first, then its own label**. Both sets merge into one
flat dictionary keyed by name alone — the label is a filter, not part of the resulting key —
so a labeled row **overrides** the unlabeled row of the same name, for that service only.
Other services keep seeing the unlabeled value.

The consequences are asymmetric:

- **Adding a labeled row on top of an unlabeled one is safe.** This is the existing pattern
  for `AutoMigrate`, the `CORS:*` family, and the Serilog `component` label.
- **Moving a key from unlabeled to labeled is not.** Deleting the unlabeled row breaks every
  service that does not select that exact label. Only do this when exactly one service
  consumes the key.

Label values are compiled into the services and must match exactly, including case and
spaces. The authoritative mapping is the `serviceMeta` block in `/app-config.yaml`. Three
labels are not simply the service name:

| Service | Label |
|---|---|
| AdminBFF | `LinkAdminBFF` |
| AutomationUI | `Link Automation UI` |
| DataAcquisitionWorker | `DataAcquisitionWorker` (distinct from `DataAcquisition`) |

Because labels are compiled in, a running service cannot be re-pointed at a different label
without a redeploy.

A label containing `:` will never match anything. The `<Service>:<Environment>` tier is not
functional — `ExternalConfigurationExtension.AddExternalConfiguration` concatenates the environment object
rather than its name — so such a label silently resolves to nothing.

## Precedence over environment variables

**A key present in App Configuration beats a container environment variable.** This is the
opposite of the usual expectation.

- **Java** — the Spring Cloud bootstrap property source defaults to
  `overrideSystemProperties=true`, and nothing in this repository overrides it.
- **.NET** — `AddAzureAppConfiguration` runs after the host builder has registered the
  environment-variable source, so App Configuration is appended last and wins.

Setting an env var on a pod to debug a deployed service will be **silently ignored** for any
key the store defines. Change the store instead.

## Java enablement

Both Java services ship `spring.cloud.azure.appconfiguration.enabled: false` in
`bootstrap.yml`. That is the correct default for local and Docker runs, which take
configuration from YAML and environment variables.

Deployed environments set `SPRING_CLOUD_AZURE_APPCONFIGURATION_ENABLED=true` on the pods, so
App Configuration **is** live in every environment listed above. The endpoint and credentials
are supplied at deploy time and do not appear in this repository.

## Working with these files

```powershell
# Export a store. --profile appconfig/kvset is required, not optional:
#   * it produces this items[] format; the default profile emits a nested config tree
#   * --label "*" is only accepted with this profile (or an appconfig destination)
az appconfig kv export -n nhsnlink-ac-dev -d file --path Config/app-config.dev.json `
    --format json --profile appconfig/kvset --label "*" --auth-mode login --yes

# Diff two exports
python Scripts/AzureAppConfig/compare_aac_exports.py Config/app-config.dev.json Config/app-config.qa.json

# Scan for credentials (also runs in CI and in the pre-commit hook)
python Scripts/AzureAppConfig/validate_aac_secrets.py "Config/*.json" --strict

# Validate the catalog against its own schema
python Scripts/AzureAppConfig/validate_app_config_schema.py
```

Without `--profile appconfig/kvset`, a plain `--format json` export omits labels entirely and
writes a nested object rather than the `items` array.

`Scripts/AzureAppConfig/export-appconfigs.bat` predates all of this: it iterates a hardcoded label list and
writes one file per label, and that list is missing `DataAcquisitionWorker`,
`Link Automation UI` and `Terminology`. Using it loses those rows. Prefer the command above.

## Guardrails

`Scripts/AzureAppConfig/validate_aac_secrets.py` runs in two places and fails on any value matching a
credential shape — storage account keys, inline passwords, Mongo URIs with credentials, PEM
blocks, JWTs, Slack webhooks — and on Key Vault references with the wrong `content_type`.

- **CI**: `.github/workflows/appconfig-secret-scan.yml`, on every PR and push to `dev`,
  `main`, `release/**`, `hotfix/**`.
- **Pre-commit**: `.githooks/pre-commit` validates the *staged* content. Enable per clone
  with `git config core.hooksPath .githooks`.

If a credential ever reaches a commit, **rotate it**. Deleting the line does not remove it
from git history, and this repository is public.

## Editing

These exports are downstream of the stores. Editing a file here does not change any running
environment — the store is what services read. LEGLINK-775 will add a pipeline that imports
these files into the stores, which inverts that relationship and makes them the source of
truth.

Three things to know before that lands:

- **Import with `--profile appconfig/kvset`.** The file carries `label`, `content_type` and
  `tags` per row, and that profile is what reads them. With the default profile those
  options are supplied on the command line instead, and the file's own values are ignored.
- **Importing a kvset file with the default profile corrupts the store.** The top-level
  `items` property is treated as a key name and the whole array becomes its value. This has
  already happened once: the dev store carried a row literally named `items` holding 13
  serialised entries.
- **`az appconfig kv import` is additive by default.** Rows deleted from these files are not
  removed from the store unless the import passes `--strict`, which makes the target match
  the source exactly. That also means anything set directly in the portal and not reflected
  here is wiped on the next run — which is the point, but it should be a deliberate choice.
