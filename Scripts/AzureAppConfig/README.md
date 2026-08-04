# Azure App Configuration tooling

Everything here is run **from the repository root**, not from this folder - the default
paths (`app-config.yaml`, `Config/`, `docs/`) are all root-relative.

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
dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet Scripts/AzureAppConfig/config_symbols.json

# 2. Call-site scan + service attribution + inventory
python Scripts/AzureAppConfig/extract_config_keys.py
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

`Scripts/AzureAppConfig/java_config_audit.json` is a hand-maintained audit of the Java
bindings - 15 `@ConfigurationProperties` classes, 12 `@Value` sites and the
`logback-spring.xml` `springProperty` reads. Update it when those change; it is small enough
not to warrant a parser.

### Catalog checks

```powershell
# Does app-config.yaml conform to the JSON Schema embedded in itself?
python Scripts/AzureAppConfig/validate_app_config_schema.py

# Does every required: true key have a row in every environment store?
python Scripts/AzureAppConfig/check_required_config.py

# Where do the catalog, the code and the stores disagree?
python Scripts/AzureAppConfig/reconcile_config_catalog.py            # all four buckets
python Scripts/AzureAppConfig/reconcile_config_catalog.py --bucket D # required but absent

python -m unittest discover Scripts/AzureAppConfig/tests             # tests for the matching rules
```

### Sensitive keys and Key Vault

`sensitive: true` in the catalog is tied to a fact in the stores rather than to judgement: a
row whose `content_type` is the `keyvaultref` type is the environment declaring that value a
secret. `check_required_config.py` holds the two in step in both directions.

The direction that matters is the second one. An entry marked `sensitive: true` whose stores
all hold a **literal** is a credential sitting in a file committed to a public repository -
the same failure `validate_aac_secrets.py` scans for, caught from the catalog side. If it
fires, rotate the value; deleting the line does not remove it from git history.

A Key Vault backed key with no catalog entry warns rather than fails. A value held in Key
Vault is per-environment with no safe default, which is exactly the catalog's admission rule,
so the usual answer is to add it - but the catalog stays curated, so that is a human call.

Mixed backing is deliberately not flagged: if any store resolves the key from Key Vault, the
flag is correct, even where another environment holds a local development value.

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
framework-owned, which is not something a gate should carry.

They share the matching functions but **do not apply the same strictness, deliberately**. The
reconciler layers `relax()` on top — Spring's relaxed binding, which folds case and strips `-`
and `_`. It is answering "does anything read this key?", where a spelling mismatch produces a
false "dead key" report, so leniency is the safer error. The gate answers "will this service
bind this row?", where the same leniency is a false pass: .NET keys are case-insensitive but
hyphen- and underscore-**sensitive**, so `Foo:BarBaz` and `Foo:Bar-Baz` are different keys and
only one of them is really provisioned.

Labels are never relaxed anywhere. App Configuration matches them exactly — the provider issues
`Select("*", "LinkAdminBFF")` verbatim — so folding them would claim a service can see a row it
cannot fetch, and would stop `check_required_config.py` catching the label typos it exists to
catch.

The two agree on every catalog entry against all three stores today; that is a property of the
current data, not a guarantee the code makes.

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
python Scripts/AzureAppConfig/validate_aac_secrets.py "Config/*.json"
python Scripts/AzureAppConfig/validate_aac_secrets.py "Config/*.json" --strict   # warnings fatal
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
