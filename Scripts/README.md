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
