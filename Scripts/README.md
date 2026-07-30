Prefixes:

* aca-* scripts related to deployment to Azure Container Apps
* docker-compose.* scripts related to running services on a local environment
* k8s-* scripts related to deployment to Kubernetes

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
