# Link.AspireAppHost

An optional way to run all of link-cloud locally with .NET Aspire: the infrastructure in containers,
every service as a process, and one dashboard for their logs, traces, health and restarts.

It is an alternative to `docker compose`, not a replacement. Nothing else references this project.
Compose, the Dockerfiles and CI are unaffected, and if you never run it, nothing changes for you.

## Running it

```powershell
dotnet run --project DotNet/Link.AspireAppHost --launch-profile http
```

Or choose **Link.AspireAppHost** as the startup project in Visual Studio or Rider. The dashboard opens
at http://localhost:15190. The first start pulls the container images and runs a Maven build, so it
takes a few minutes; later starts are much quicker.

### Prerequisites

- Docker Desktop, running
- The .NET 8 SDK, and authentication to the Azure Artifacts feed (as for `link-cloud.sln`; see
  `DEVELOPMENT.md`)
- Maven on `PATH`, and a JDK 21
- Node and npm on `PATH`, for the Admin UI

### One-time settings

These go in the AppHost's user secrets, so they stay on your machine:

```powershell
cd DotNet/Link.AspireAppHost
# The folder of terminology code groups (one folder each, holding a FHIR .json and a .csv).
dotnet user-secrets set "Terminology:Path" "C:\path\to\terminology-data"
# Only if JAVA_HOME is not a JDK 21. The Java services target 17 and are built and run on 21.
dotnet user-secrets set "Java:Home" "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.8-hotspot"
```

| Setting | Default | Purpose |
|---|---|---|
| `Terminology:Path` | empty: Terminology starts with no code groups | Terminology's data folder |
| `Java:Home` | `JAVA_HOME`, else `java` on `PATH` | The JDK that builds and runs MeasureEval and Validation |
| `Java:Maven` | `mvn` | The Maven command |
| `AdminUi:Enabled` | `true` | `false` leaves the Admin UI out |
| `AdminUi:AuthRequired` | `false` | The UI's `authRequired`. The services run anonymously and the Admin BFF maps no login endpoints, so `true` sends people to a login page that 404s until local authentication is set up |

The SQL Server password is generated on the first run and kept in the same user secrets
(`Parameters:sql-password`).

## What runs, and where

Every port differs from compose's, so both stacks can run at the same time. Each service is on its
compose port plus 10000.

| Resource | URL / port |
|---|---|
| Dashboard | http://localhost:15190 |
| Admin UI (`ng serve`) | http://localhost:14200 |
| Admin BFF | http://localhost:18063 |
| Account / Audit / Census / DataAcquisition | 18060 / 18062 / 18064 / 18065 |
| MeasureEval (Java) | 18067 |
| Normalization / QueryDispatch / Report / Submission / Tenant | 18068 / 18071 / 18072 / 18073 / 18074 |
| Validation (Java) / Terminology / Acquisition worker | 18075 / 18076 / 18078 |
| Automation UI | http://localhost:15256 |
| Mock FHIR server / Mock DMRP API | 16158 / 16159 |
| HAPI FHIR server | http://localhost:16157/fhir |
| SQL Server | `localhost,14333`, user `sa` |
| Kafka / Kafka UI / REST proxy | `localhost:19092` / http://localhost:19080 / http://localhost:18082 |
| MongoDB (replica set `rs0`) | `mongodb://localhost:37017/?directConnection=true` |
| Redis (no password) | `localhost:16379` |
| Azurite blob | http://127.0.0.1:11000/devstoreaccount1 |
| Loki | http://localhost:13100 |

## How the services are configured

Each service runs the way compose runs it, with compose's hostnames swapped for this host's:

- The .NET services run in the `Docker` environment, so they read `appsettings.Docker.json`, the
  settings CI exercises. The Java services run with the `docker` Spring profile.
- The AppHost passes environment variables only for values that name a compose host (`kafka_b`,
  `mssql`, `redis_cache`, `loki`, another service's container name), and for the values compose
  itself passes. Environment variables outrank appsettings files. User secrets are not loaded
  outside Development, so nothing on a developer's machine changes what runs here.
- Kafka is plain (no SASL), and its topics come from `topics.txt`, created on each start.
- Each service gets its own database, created on start and migrated by the service (`AutoMigrate`),
  as under compose. Validation's schema is updated by Hibernate. The dashboard command **Apply
  Validation migrations** also runs `database/migrations/V*.sql`, as the deployment pipeline does.
- Telemetry goes to the Aspire dashboard, and logs go to its console view and to Loki.

The code is laid out as follows: `InfrastructureExtensions.cs` holds the containers, and
`DotNetServiceExtensions.cs` and `JavaServiceExtensions.cs` the per-service settings. `Program.cs`
wires each service, and `AdminUiExtensions.cs` with `admin-ui.proxy.mjs` runs the UI.

## Things to know

- **Don't run the same service under the AppHost and from your IDE at once.** They use different
  Kafka brokers, so they won't share a consumer group, but they would each process their own copy
  of everything.
- **Data.** SQL Server, MongoDB and Azurite keep their data in Docker volumes (`link-aspire-*`)
  between runs. Kafka, Redis and the HAPI FHIR server start empty each time. To reset everything,
  stop the AppHost and run `docker volume rm link-aspire-sql link-aspire-mongo link-aspire-azurite`.
- **The Admin UI** runs `ng serve` with `admin-ui.proxy.mjs`, which proxies `/api` to the Admin BFF.
  It also answers `/assets/app.config.local.json`, which the UI merges over `app.config.json`, with
  the AppHost's overrides (`authRequired`). Nothing is written into `Web/Admin.UI`, and
  `proxy.conf.json` is left alone. If you have your own `src/assets/app.config.local.json`, it is
  served first and wins. Packages are installed (`npm ci`) only when
  `node_modules` is missing.
- **Java telemetry.** MeasureEval and Validation are given the dashboard's OTLP address but not its
  API key: Aspire's usual way of passing the key sets variables the OpenTelemetry Spring starter
  cannot read, and they stop the services from starting. Their traces may not show on the dashboard.
  Their logs still reach the console view and Loki.
- **Not included:** the NHSN app (`nhsn/`), the SFTP server, Grafana, Tempo and Prometheus.
