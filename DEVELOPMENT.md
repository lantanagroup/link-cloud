# Building, running and testing Link Cloud

Commands for the local docker-compose stack, the .NET and Java builds, the test suites and EF migrations. Referenced from `AGENTS.md`.


### Local stack (required for E2E and most integration work)

```powershell
docker compose up --wait --wait-timeout 300   # bring up the stack and wait for health
docker compose down -v --remove-orphans       # tear everything down (resets volumes)
```

`--wait` implies detached mode, so `-d` is redundant with it. Give it a timeout: without one it waits indefinitely for a service that is never coming up. CI uses `Scripts/check_health.sh` instead — it dumps each unhealthy container's logs to `service-logs/` on timeout, which matters when the runner is gone by the time anyone looks.

Service ports are listed at the top of `docker-compose.yml` (e.g. fhir 6157, admin-bff 8063, kafka 9092, kafka-ui 9095, loki 3100, grafana 3000, azurite 10000, mssql 1433, mongo 17017). The root `.env` provides default credentials used by compose.

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
