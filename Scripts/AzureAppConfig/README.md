# Azure App Configuration tooling

Everything here is run **from the repository root**, not from this folder - the default
paths (`app-config.yaml`, `docs/`, `Scripts/`) are all root-relative.

**The exports are not in this repository.** They are the per-environment values behind the
catalog - the deployed environments' real configuration - so LEGLINK-912 moved them to the
private **`lantanagroup/link-cac`** repository, leaving the catalog and this tooling here.

Every tool that reads them resolves the directory the same way, through
`config_key_matching.default_config_dir`:

1. `--config-dir` if passed (`validate_aac_secrets.py` takes paths positionally instead),
2. otherwise the `LINK_CAC_CONFIG_DIR` environment variable,
3. otherwise `../link-cac/Config` - a sibling clone, which is what makes the no-argument
   invocations below work.

```powershell
# One-time, if link-cac is not cloned beside link-cloud
$env:LINK_CAC_CONFIG_DIR = "D:/src/link-cac/Config"
```

* `export-appconfigs.bat <app-config-name> <output-file>` produces an export.
* `compare_aac_exports.py <left> <right>` diffs two exports.
* `validate_aac_secrets.py [paths] [--strict]` fails if an export contains a
  credential. With no paths it scans the resolved export directory, and exits `2` rather
  than reporting success if that directory holds nothing.

### Configuration key inventory

Two tools derive the set of configuration keys the code actually reads, so the catalog can be
checked against reality rather than trusted:

```powershell
# 1. Roslyn symbol dump (file-based app - no project, needs .NET 10 SDK)
dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet Scripts/AzureAppConfig/config_symbols.json

# 2. Call-site scan + service attribution + inventory
python Scripts/AzureAppConfig/extract_config_keys.py
```

Producing `Scripts/AzureAppConfig/config-key-inventory.json` (machine-readable, consumed by the
reconciler and the tagging tool) and `docs/config-key-inventory.md` (the human reference).

Only the markdown is committed. Both JSON files above -- `config_symbols.json` and
`config-key-inventory.json` -- are gitignored: a committed derived file
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

Only the first of these runs in this repository's CI. Everything that reads the exports runs
in `link-cac` instead - see [Secret scanning](#secret-scanning) for why - so a catalog change
made here is checked against the stores by `link-cac`'s pull request and its daily run, not by
this repository's. **Run `check_required_config.py` locally before merging a change that adds a
`required: true` key**; nothing here will stop you.

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
all hold a **literal** is a credential sitting in a committed file - the same failure
`validate_aac_secrets.py` scans for, caught from the catalog side. `link-cac` being private
does not change the response: rotate the value, because deleting the line does not remove it
from git history.

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
python Scripts/AzureAppConfig/validate_aac_secrets.py            # resolved export directory
python Scripts/AzureAppConfig/validate_aac_secrets.py --strict   # warnings fatal
python Scripts/AzureAppConfig/validate_aac_secrets.py "D:/src/link-cac/Config/app-config.*.json" --strict
```

It reports **errors** for values matching a known credential shape (storage
account key, inline password, Mongo URI with credentials, PEM block, JWT, Slack
webhook, ...) and for Key Vault references whose `content_type` is not the
`keyvaultref` type -- App Configuration serves those as a literal JSON string
instead of resolving them, so the service reads `{"uri": "..."}` as its password.

It reports **warnings** for secret-shaped keys holding a plain literal, for
malformed entries, and for duplicate `(key, label)` pairs. Not every such value is
a credential, so these are surfaced for review rather than failing the build.

`ConnectionStrings:Redis` and `ResourceCache:Redis:ConnectionString` are Redis
connection strings with the password supplied separately from Key Vault. They are
listed in `PASSWORDLESS_CONNECTION_STRING_KEYS`, which permits comma-delimited
StackExchange.Redis connection parameters while exempting these keys from the
secret-shaped-key warning. Inline `password` or `pwd` values remain credential
errors. Add to that list only when the password is sourced separately.

**It runs in `link-cac`, never here.** `.github/workflows/appconfig-checks.yml` in that
repository runs it on every PR and push to `main`, plus daily; it checks *this* repository
out for the script, which is free because this one is public.

It deliberately does **not** run in this repository's CI, for two reasons that apply to any
cross-repo check from here:

* **Actions logs on a public repository are world-readable.** Two of this script's warnings
  quote the offending value -- the secret-shaped-key warning and the malformed-entry warning
  -- so a finding would publish the very value it is complaining about.
* **A read token for `link-cac` in this repository's secrets is readable by anyone with write
  access here**, through a workflow change on a same-repo pull request. That is a larger
  exposure than the check is worth.

Nothing is lost by that: a pull request *here* cannot change `link-cac`'s exports, so the scan
had nothing to catch on this side.

There is no pre-commit hook for the exports either. `.githooks/pre-commit` here still validates
a staged `app-config.yaml` against its schema (enable per clone with
`git config core.hooksPath .githooks`, bypass with `--no-verify`), but nothing under `Config/`
can be staged in this repository any more. `link-cac` has no hook at all - running this
scanner there would need a clone of this repository on every machine - so on that side CI is
the only gate, and it first fires when a pull request opens.

If a real credential is ever committed, **rotate it** -- deleting the line does
not remove it from git history, in either repository.
