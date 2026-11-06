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
| `Auth:Mode` | `Anonymous` (`Keycloak` in the https profile) | How people sign in; see below |
| `AdminUi:AuthRequired` | `false` | The UI's `authRequired`. Keycloak mode turns it on. Under anonymous access the Admin BFF maps no login endpoint, so `true` on its own sends people to a page that 404s |

The SQL Server password is generated on the first run and kept in the same user secrets
(`Parameters:sql-password`).

### Signing in: the http and https profiles

| Profile | `Auth:Mode` | What happens |
|---|---|---|
| `http` | `Anonymous` | Every service runs with anonymous access, and the Admin UI asks no one to sign in |
| `https` | `Keycloak` | As deployed: a local Keycloak signs people in to the Admin BFF over OpenID Connect, and the backend services require a Link bearer token |

In Keycloak mode:

- Keycloak runs at https://localhost:18443 (admin console at `/admin`, user `admin`). It serves HTTPS
  with the .NET development certificate, which it exports on each start. It needs HTTPS because the
  Admin BFF requires an HTTPS authority outside Development. Trust the certificate once with
  `dotnet dev-certs https --trust`.
- The `link` realm and the `link-admin-bff` client come from `Keycloak/link-realm.json`, imported the
  first time. Keycloak keeps its data in the `link-aspire-keycloak` volume, so users survive restarts.
- Keycloak's admin password and the BFF's client secret are generated on the first run and kept in
  the AppHost's user secrets (`Parameters:keycloak-admin-password`, `Parameters:keycloak-client-secret`).
  No secret is committed: the realm file reads the client secret from `LINK_BFF_CLIENT_SECRET`.
- The **Create user** command appears on `account` (Keycloak mode only). It creates the user in both
  Keycloak and Account. Sign in to the Admin UI at http://localhost:14200 with that username or
  email, and the Admin BFF then finds the Account user by the email claim.
- The backend services require a Link bearer token, as in a deployed environment. The Admin BFF
  attaches one to everything it forwards. The services create system tokens for their calls to
  each other, and the AppHost's own commands send one too. They're all signed with the shared local
  key, as before.
- **The token issuer** (`LinkTokenService:Authority` and `Authentication:Schemas:LinkBearer:Authority`,
  and the Java `authentication.authority`) is Keycloak's realm URL, not `LinkServiceAdmin`. The
  shared validation passes it to ASP.NET's JWT handler as an OpenID Connect authority. That handler
  demands HTTPS outside Development and looks up the authority's metadata, and Keycloak's realm can
  answer both. The tokens are still Link's own: Keycloak signs nothing in that chain.
- **Terminology, Automation UI and the mock DMRP API stay anonymous.** Terminology is anonymous by
  design. Automation UI serves only 503s with authentication on, so it sends tokens to the services
  instead (`UseBearerForServiceCalls`). The mock stands in for an external system.
- **MeasureEval and Validation check a token's signature but not its issuer.** That's how their
  `JwtAuthenticationFilter` works, not something the AppHost sets.

`Auth:Mode` can also be set directly, in user secrets or as an environment variable, to use Keycloak
with the http profile's dashboard.

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
| SQL Server | `tcp:127.0.0.1,14333`, user `sa`, trust the server certificate (see below) |
| Kafka / Kafka UI / REST proxy | `localhost:19092` / http://localhost:19080 / http://localhost:18082 |
| MongoDB (replica set `rs0`) | `mongodb://localhost:37017/?directConnection=true` |
| Redis (no password) | `localhost:16379` |
| Azurite blob | http://127.0.0.1:11000/devstoreaccount1 |
| Loki | http://localhost:13100 |
| Keycloak (https profile only) | https://localhost:18443, realm `link` |

### Connecting to SQL Server from SSMS or another tool

Put `tcp:127.0.0.1,14333` in the **Server name** box itself. Don't put it in Additional Connection
Parameters: the Server name box becomes `Data Source` and overrides it. Use `127.0.0.1`, not
`localhost`. Docker publishes the port on the IPv4 loopback address only, and `localhost` can resolve
to `::1`, where nothing listens. SSMS then falls back to Named Pipes and fails with "Named Pipes
Provider, error: 40". The comma separates the port, and the `tcp:` prefix stops the Named Pipes
fallback.

Sign in with SQL Server Authentication as `sa`, with **Trust server certificate** ticked. The
password is `Parameters:sql-password` in the AppHost's user secrets (`dotnet user-secrets list` in
this folder). The services' databases are `link-account`, `link-tenant` and so on.

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

### Project layout

| Path | What it holds |
|---|---|
| `Program.cs` | Wires every resource together: infrastructure, services, and how sign-in works |
| `Ports.cs` | Every fixed host port |
| `Security/LinkSystemToken.cs` | The Link bearer token the AppHost's own commands send |
| `Security/AuthMode.cs` | `Auth:Mode`: Anonymous (http profile) or Keycloak (https profile) |
| `Security/LinkKeycloak.cs` | Keycloak's URLs and parameters, its admin token, and creating users |
| `Extensions/InfrastructureExtensions.cs` | The containers: SQL Server, Kafka and its topics, Mongo, Redis, Azurite, Loki, HAPI FHIR |
| `Extensions/DotNetServiceExtensions.cs` | How a .NET service is run and configured |
| `Extensions/JavaServiceExtensions.cs` | The Maven build, MeasureEval, Validation, and the Validation migrations command |
| `Extensions/AdminUiExtensions.cs` | The Admin UI under `ng serve` |
| `Extensions/KeycloakExtensions.cs` | Reads `Auth:Mode`, and adds Keycloak with the development certificate |
| `Extensions/SeedDataExtensions.cs` | Seed data and the **Seed data** command |
| `Extensions/AccountUserExtensions.cs` | The **Create user** command |
| `AdminUi/proxy.mjs` | The Admin UI's `ng serve` proxy |
| `Keycloak/link-realm.json` | The `link` realm Keycloak imports |

## Dashboard commands

- **Seed data** (on `account`): creates the local seed data that's missing, currently the `LinkUser`
  role with the `IsLinkAdmin` permission. Link doesn't seed reference data through migrations, so a
  new Account database has no roles. This step also runs automatically each time Account turns
  healthy. It only creates what's missing and never changes existing data, and it goes through
  Account's own API. Add more seed steps in `SeedDataExtensions.cs`.
- **Create user** (on `account`, Keycloak mode only): a form for first name, last name, email,
  username, role and password. The roles are read from Account. It creates the user in Keycloak, to
  sign in with, and in Account, through Account's own `POST /api/account/user`. Each side is tried
  even if the other already has the user, so after a Keycloak or SQL Server reset, running it again
  fills in whichever is missing. It reads the Account user back to check the role was really
  assigned, because Account skips a role it can't find and still answers 201. It isn't offered in the
  http profile: no one signs in there, so there's no user to create.
- **Apply Validation migrations** (on `validation`): runs `database/migrations/V*.sql` in order.

## Things to know

- **Don't run the same service under the AppHost and from your IDE at once.** They use different
  Kafka brokers, so they won't share a consumer group, but they would each process their own copy
  of everything.
- **Data.** SQL Server, MongoDB and Azurite keep their data in Docker volumes (`link-aspire-*`)
  between runs. Kafka, Redis and the HAPI FHIR server start empty each time. To reset everything,
  stop the AppHost and run `docker volume rm link-aspire-sql link-aspire-mongo link-aspire-azurite`.
- **The Admin UI** runs `ng serve` with `AdminUi/proxy.mjs`, which proxies `/api` to the Admin BFF.
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
