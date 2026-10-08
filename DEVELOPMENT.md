# Building, running and testing Link Cloud

Commands for the local docker-compose stack, the .NET and Java builds, the test suites and EF migrations. Referenced from `AGENTS.md`.


### Local stack (required for E2E and most integration work)

```powershell
docker compose up --wait --wait-timeout 300   # bring up the stack and wait for health
docker compose down -v --remove-orphans       # tear everything down (resets volumes)
```

`--wait` implies detached mode, so `-d` is redundant with it. Give it a timeout: without one it waits indefinitely for a service that is never coming up. CI uses `Scripts/check_health.sh` instead — it dumps each unhealthy container's logs to `service-logs/` on timeout, which matters when the runner is gone by the time anyone looks.

Service ports are listed at the top of `docker-compose.yml` (e.g. fhir 6157, admin-bff 8063, kafka 9092, kafka-ui 9095, loki 3100, grafana 3000, azurite 10000, mssql 1433, mongo 17017). The root `.env` provides default credentials used by compose.

### Azure Artifacts (Thetis)

Automation, Automation.UI, and MockFhirServer restore `LantanaGroup.Thetis.*` from Azure Artifacts feed `Shared_BOTW_Feed`. Repo `nuget.config` lists that source and does not store a token. Do not write a PAT or access token into `nuget.config`. `packageSourceMapping` keeps every other package on nuget.org.

`docker compose` inside this repo passes a short-lived Azure CLI token to those images only as the BuildKit secret `feed_accesstoken`. One-time setup from the repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1 -ProfilePath "$PROFILE"
```

The calling shell expands `"$PROFILE"` before Windows PowerShell starts, so PowerShell 7 updates the PowerShell 7 profile. A direct run of the script, with no `-ProfilePath`, still uses that process's own profile.

Git Bash:

```bash
bash ./Scripts/docker-compose.feed-token-install.sh
```

The installer adds one line to the shell profile:

```powershell
. "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token
```

```bash
. "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token
```

The profile line loads only when the execution policy allows local scripts. Run `Get-ExecutionPolicy -List`. If the effective policy is `Restricted` or `AllSigned` and `MachinePolicy` and `UserPolicy` are undefined, run `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`. If group policy sets `MachinePolicy` or `UserPolicy`, leave it: `-ExecutionPolicy Bypass` does not override that lock. Fetch the token with `powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token.ps1` where local scripts are allowed, then dot-source `$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1` before `docker compose`.

Open a new shell in the repo and run `docker compose` as usual. The wrapper fetches a token into the gitignored file `.azure-artifacts.env` when that file is missing or the token has less than 10 minutes left, then passes `AZURE_ARTIFACTS_PAT` only to the docker process. The parent shell does not keep the variable. On Windows, including Git Bash, the PowerShell script writes the file and limits its ACL to the current user before the token is stored. If that restriction fails, the file is removed and the command exits. The token is not printed.

If Azure CLI is missing, `Scripts/docker-compose.feed-token.ps1` downloads Microsoft's per-user ZIP (`https://aka.ms/installazurecliwindowszipx64`) into `%LOCALAPPDATA%\AzureCLI` and adds its `bin` directory to the user PATH. That ZIP does not need an administrator. If the ZIP install fails, the script tries `winget install --exact --id Microsoft.AzureCLI` without `--scope user` and says that installer needs an administrator. If winget is missing, the install fails, or sign-in is cancelled, the command prints that reason and stops. A `docker compose` command in this repo then stops before Docker starts and tells you to run the installer lines above. You can also install Azure CLI from https://aka.ms/installazurecliwindows and run the fetch script again, or finish `az login`.

Visual Studio can still restore on the host after you sign into the `lantanagroup` Azure DevOps org. Add `Shared_BOTW_Feed` under Tools > Options > NuGet Package Manager > Package Sources with `https://pkgs.dev.azure.com/lantanagroup/nhsnlink/_packaging/Shared_BOTW_Feed/nuget/v3/index.json` if it is not already listed. That path does not write credentials into the tracked `nuget.config`.

CI is unchanged. GitHub Actions sets `AZURE_ARTIFACTS_PAT` and bakes `docker-compose.yml`, which supplies the `feed_accesstoken` secret. The Automation image pipeline passes `--secret id=feed_accesstoken,env=SYSTEM_ACCESSTOKEN`.

### .NET

```powershell
dotnet build link-cloud.sln                                                 # whole solution
dotnet build DotNet/Account/Account.csproj                                  # one service
dotnet test  DotNet/ServiceTests/ServiceTests.csproj                        # all .NET unit + integration tests
dotnet test  DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~Tenant   # one area
```

`ServiceTests` contains both unit tests (no infra) and integration tests (Testcontainers spins up SQL Server + Azurite — Docker must be running). xUnit collections keep integration tests serialized while unit tests run in parallel within the same invocation.

### Backend E2E (requires the docker-compose stack already up and healthy)

```powershell
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj                                          # all suites
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter FullyQualifiedName~AdhocReportTest
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter Category=ApiStabilityTest       # CI uses Category=
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --logger "console;verbosity=detailed"
```

Endpoints are read from env vars (see `Tests/BackendE2ETests/README.md` and `TestConfig.cs`); defaults match the local docker-compose ports. Each test seeds deterministic FHIR data and validates with **strict prediction-vs-actual reconciliation** — generated input drives an exact expected count for every downstream layer (manifest, ABS NDJSON, Report/DA/Normalization/Validation DBs), and a deviation in either direction fails the run.

### Java

```bash
cd Java
mvn clean test                                          # build + unit-test all modules (CI does this)
mvn -pl measureeval -am clean package                   # one module + its deps (the `shared` lib)
mvn -P cli -pl measureeval -am clean package            # build measureeval as a CLI jar (FileSystemInvocation main)
```

### Admin UI

```powershell
cd Web/Admin.UI
npm install
npm start                                               # ng serve on :4200, proxied via proxy.conf.json
npm test                                                # ng test (karma + jasmine)
npm run build
```

### EF Core migrations

Entity changes that persist via EF Core **must** ship a migration that supports both upgrade *and* downgrade. Migrations live alongside each service (e.g. `DotNet/Account/Migrations/`):

```powershell
dotnet ef migrations add <Name> --project DotNet/Account/Account.csproj
dotnet ef database update     --project DotNet/Account/Account.csproj
```
