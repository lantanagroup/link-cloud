# Link.AspireAppHost

An optional way to run all of link-cloud locally with .NET Aspire: the infrastructure in containers,
every service as a process, and one dashboard for their logs, traces, health and restarts.

It is an alternative to `docker compose`, not a replacement. Nothing else references this project.
Compose, the Dockerfiles and CI are unaffected, and if you never run it, nothing changes for you.

## From nothing to your first ad-hoc report

A new machine, or a stack you have just reset, has no facilities, measures or users. These steps take it
to a generated report. The sections after this one explain each part in more depth.

1. **Install the prerequisites** listed under [Prerequisites](#prerequisites), and start Docker
   Desktop.
2. **Point Terminology at its data.** Get a copy of the terminology data folder from a teammate (one
   sub-folder per code group, each holding a FHIR `.json` and a `.csv`), then:

   ```powershell
   cd DotNet/Link.AspireAppHost
   dotnet user-secrets set "Terminology:Path" "C:\path\to\terminology-data"
   ```

   You can skip this and still get a report, but the measures' value sets are not expanded and
   Validation's terminology checks find nothing. If `JAVA_HOME` is not a JDK 21, set `Java:Home` too
   (see [One-time settings](#one-time-settings)).
3. **Start the AppHost** from the repository root:

   ```powershell
   dotnet run --project DotNet/Link.AspireAppHost --launch-profile http
   ```

   The dashboard opens at http://localhost:15190. The first start pulls container images, runs a Maven
   build and installs the Admin UI's packages, so it takes several minutes. Wait until the services
   show as running and healthy. A dashboard command stays greyed out until its service is healthy.
   Begin with the `http` profile, where nobody has to sign in. The `https` profile signs people in
   through Keycloak (see [Signing in](#signing-in-the-http-and-https-profiles)).
4. **Seed data** (the command on `admin-bff`). This loads the role, Validation's artifacts, categories
   and rubric, and the three measures. Each part also runs by itself when its service turns healthy,
   so the command mostly confirms that it worked: a green notice means it did. Run it again if
   Terminology was not ready the first time, so the measures get their value sets expanded.
5. **Apply Validation migrations** (the command on `validation`). Hibernate creates Validation's
   tables but never changes an existing constraint or index; the migrations bring the database up to
   what a deployed one has. Run it once on a new database, and again after pulling changes that add a
   migration.
6. **Only with the `https` profile: Create user** (the command on `account`). Enter a name, an email,
   a password and the `LinkUser` role, then sign in to the Admin UI as that user.
7. **Create test facility** (the command on `admin-bff`). Choose an id, a measure and the EHR vendor,
   and the **FHIR server** the facility's data comes from:

   | FHIR server | Enter | Patients |
   |---|---|---|
   | The mock that runs in the AppHost | `http://localhost:16158/fhir` | Ten synthetic patients, `mock-patient-0001` to `mock-patient-0010`, with data in January 2026. http://localhost:16158/api/mock-fhir/patients lists them |
   | A deployed FHIR server, or your own | Its base URL. The form's default is QA's `https://ehr-test.nhsnlink.org/fhir` | Whichever patients that server holds |

   The query plans the vendor picks assume that vendor's data (see [Dashboard
   commands](#dashboard-commands)). If the measure list is empty, Seed data has not finished: run it,
   then open the form again.
8. **Generate the report.** Open the Admin UI at http://localhost:14200 and go to **Reports >
   Generate Ad-Hoc Report**:
   - **Facility**: the one you created. **Report Types**: its measure.
   - **Bypass submission**: tick it to stop after validation. Left unticked, the report is submitted
     locally, to Azurite and `%TEMP%\link-aspire\submission`, never to NHSN.
   - **Reporting Cadence** and dates: a period that covers the patients' encounters (January 2026 for
     the mock).
   - **Enter Patient IDs** or **Upload CSV**: patient ids on the facility's FHIR server.
   - Don't use **Use Census** for a first report. It asks Census which patients were admitted in the
     period, and Census has recorded none: the test facility's Census schedule is created disabled.

   Select **Generate Report**, then **View Report** to follow it.
9. **Watch it run.** The dashboard's console view shows each service's output, and Grafana at
   http://localhost:13000 searches all of them (see [Searching the logs](#searching-the-logs)). Search
   for the report id to follow it from acquisition to submission.

The data is kept between runs (see [Things to know](#things-to-know)), so next time only steps 3 and 8
are needed.

## Running it

```powershell
dotnet run --project DotNet/Link.AspireAppHost --launch-profile http
```

Or choose **Link.AspireAppHost** as the startup project in Visual Studio or Rider. The dashboard opens
at http://localhost:15190 (under the `https` profile, also at https://localhost:17190). The first start
pulls the container images and runs a Maven build, so it takes a few minutes; later starts are much
quicker. The Maven build (`java-build` in the dashboard) runs on every start, and MeasureEval and
Validation wait for it.

### Prerequisites

- Docker Desktop, running
- The .NET 8 SDK. Every package comes from nuget.org, so no feed sign-in or token is needed
- Maven on `PATH`, and a JDK 21
- Node and npm on `PATH`, for the Admin UI and the NHSN App UI
- Only if you turn the NHSN App on (it is off by default): the NHSN design system's package file, `nhsn-react-core-2.4.1.tgz`, which
  cannot be in the repository. Put your copy in `nhsn/NHSN-App-UI/packages/`, a folder you create; see
  [The NHSN App](#the-nhsn-app)

### One-time settings

These go in the AppHost's user secrets, so they stay on your machine:

```powershell
cd DotNet/Link.AspireAppHost
# The folder of terminology code groups (one folder each, holding a FHIR .json and a .csv).
dotnet user-secrets set "Terminology:Path" "C:\path\to\terminology-data"
# Only if JAVA_HOME is not a JDK 21. The Java services target 17 and are built and run on 21.
dotnet user-secrets set "Java:Home" "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.8-hotspot"
# Only for the NHSN App UI, and only if you keep the design system's package file outside the
# repository folder instead of in nhsn/NHSN-App-UI/packages/: the file, or the folder that holds it.
dotnet user-secrets set "NhsnApp:ReactCorePackage" "C:\path\to\nhsn-react-core-2.4.1.tgz"
```

| Setting | Default | Purpose |
|---|---|---|
| `Terminology:Path` | empty: Terminology starts with no code groups | Terminology's data folder |
| `Java:Home` | `JAVA_HOME`, else `java` on `PATH` | The JDK that builds and runs MeasureEval and Validation |
| `Java:Maven` | `mvn` | The Maven command |
| `AdminUi:Enabled` | `true` | `false` leaves the Admin UI out |
| `AutomationUi:AutoStart` | `false` | `automation-ui` is in the dashboard, configured but stopped, until you start it there. `true` starts it with everything else. Automation UI runs work of its own once it is up; nothing else depends on it |
| `NhsnApp:Enabled` | `false` | `true` adds the NHSN App BFF and the NHSN App UI. Off by default because the UI needs a one-time manual setup; see [The NHSN App](#the-nhsn-app) |
| `NhsnApp:ReactCorePackage` | empty | Where the NHSN design system's package file is, when it is not already in `nhsn/NHSN-App-UI/packages/`: the file, or the folder that holds it. See [The NHSN App](#the-nhsn-app) |
| `Auth:Mode` | `Anonymous` (`Keycloak` in the https profile) | How people sign in; see below |
| `AdminUi:AuthRequired` | `false` | The UI's `authRequired`. Keycloak mode turns it on. Under anonymous access the Admin BFF maps no login endpoint, so `true` on its own sends people to a page that 404s |
| `Validation:Engine` | `Legacy` | Which engine decides whether a patient passes validation: `Legacy` (HAPI's validator) or `Rubric` (the rubric engine, with the rubric Seed data publishes). Sets Validation's `vaas.bridge.enabled` |
| `Validation:ShadowComparison` | `true` | Also run the other engine on every patient and log whether the two agree. Doubles validation work; `false` runs only the engine above. Sets `vaas.bridge.shadow-enabled` |
| `Validation:ExcludedResourceTypes` | `["MeasureReport"]` | Resource types Validation's legacy validator leaves out of each patient bundle. Validating the MeasureReport costs more than the rest of the patient and its findings aren't used; `[]` validates everything |
| `Replicas:<service>` | `1` | How many copies of a service run; see [Scaling a service out](#scaling-a-service-out) |

The SQL Server password is generated on the first run and kept in the same user secrets
(`Parameters:sql-password`). So is the SFTP server's password, for its `admin` account
(`Parameters:sftp-password`); the dashboard's Parameters tab shows it too.

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

The services' ports belong to Aspire's proxy, which passes requests to the service listening behind it
on a port Aspire picks. That is what lets a service run as several replicas, as a deployed service
scales out behind its ingress (see [Scaling a service out](#scaling-a-service-out)). The infrastructure containers are not proxied: their ports are the ones
Docker publishes, because under heavy connection load the proxy has been seen to stop accepting
connections and not recover.

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
| NHSN App UI (`webpack serve`) / NHSN App BFF | http://localhost:18090 / 18079 |
| Mock FHIR server / Mock DMRP API | http://localhost:16158/fhir (ten patients, January 2026; ids at `/api/mock-fhir/patients`) / 16159 |
| HAPI FHIR server | http://localhost:16157/fhir |
| SQL Server | `tcp:127.0.0.1,14333`, user `sa`, trust the server certificate (see below) |
| Kafka / Kafka UI / REST proxy | `localhost:19092` / http://localhost:19080 / http://localhost:18082 |
| MongoDB (replica set `rs0`) | `mongodb://localhost:37017/?directConnection=true` |
| Redis (no password) | `127.0.0.1:16379` (IPv4: the port is published on 127.0.0.1 only) |
| Azurite blob | http://127.0.0.1:11000/devstoreaccount1 |
| Loki | http://localhost:13100 |
| Grafana (no login) | http://localhost:13000 |
| SFTP server | `127.0.0.1:12222`, user `admin`, generated password (see above), uploads in `data/` (kept between runs) |
| Keycloak (https profile only) | https://localhost:18443, realm `link` |

### The NHSN App

`nhsn-app-bff` and `nhsn-app-ui` are the NHSN App (`nhsn/`): its BFF, and the standalone shell of its UI
that stands in for the CDC NHSN App. Both are off by default, because the UI needs the one-time setup
below. Turn them on with `NhsnApp:Enabled`, in user secrets so the choice stays on your machine:

```powershell
cd DotNet/Link.AspireAppHost
dotnet user-secrets set "NhsnApp:Enabled" "true"
```

#### One-time setup: the design system's package file

The UI installs the NHSN design system (`@nhsn/nhsn-react-core`) from a package file,
`nhsn-react-core-2.4.1.tgz`. The file cannot be in the repository, so each developer gets a copy and
puts it where the UI's `package.json` looks for it:

1. Create the folder `nhsn/NHSN-App-UI/packages/`. Nothing creates it for you: not git, npm or React.
2. Put `nhsn-react-core-2.4.1.tgz` in it, with that exact name.
3. Set `NhsnApp:Enabled` to `true`, as above.
4. Start the AppHost. `nhsn-app-ui-install` runs `npm ci` once, then `nhsn-app-ui` starts.

```
nhsn/NHSN-App-UI/
  packages/
    nhsn-react-core-2.4.1.tgz
```

`packages/` is listed in `nhsn/NHSN-App-UI/.gitignore`, so the file never shows in `git status` and
cannot be committed by accident. Only that folder is ignored: the same file anywhere else in the
repository folder shows up as untracked.

To keep the file outside the repository folder instead, set `NhsnApp:ReactCorePackage` to the file, or
to the folder that holds it (see [One-time settings](#one-time-settings)). The AppHost then creates
`packages/` and copies the file into it before the install. It is copied, not installed from where it
is, because `package.json` and the lock file name it by that path.

Things to know:

- **Without the file, the UI is left out.** `nhsn-app-ui` does not appear in the dashboard, and a warning
  in the AppHost's console log says what is missing. The same happens when `NhsnApp:ReactCorePackage`
  points at a file that is not there or has a different name. The BFF and everything else run as usual.
- **The file is needed for the install only.** Once `nhsn/NHSN-App-UI/node_modules/webpack-cli` exists,
  later starts skip the install and do not look for the file. Delete `node_modules` to have the next
  start install again; the file must be in place then.
- **A new version of the design system** changes the file name in the UI's `package.json`. Put the new
  file in `packages/` and delete `node_modules`. The AppHost reads the name from `package.json`, so it
  needs no change.

#### Using it

Open http://localhost:18090. The shell opens in BFF mode, with `/api` proxied to `nhsn-app-bff`. Create a
test profile in its sidebar: email, name, groups (such as `FACADMIN`) and facility id. The issuer, key
id and private key are already filled in.

Those three come from a key pair the AppHost makes on the first run and keeps under the temp directory
(`link-aspire/nhsn-app-jwt`). The shell signs a token with the private key in the browser, and the BFF
validates it against the pair's certificate, as it validates the NHSN gateway's tokens when deployed. No
key is committed.

- **A saved profile holds the key**, so it keeps working across restarts. If that temp folder is deleted,
  the next start makes a new pair, and the BFF answers a saved profile with 401. Edit the profile, or
  create a new one, to pick up the new values.
- **The BFF's own endpoints always require one of those tokens**, in both `Auth:Mode`s. `Auth:Mode` only
  decides whether the BFF sends Link bearer tokens to the Link services it calls.
- **The UI's own files are not changed.** It runs under `webpack serve`, as `npm start` runs it, with
  `NhsnAppUi/webpack.config.js` wrapped around the UI's config for the port, the proxy and the profile
  defaults. Running `npm start` in `nhsn/NHSN-App-UI` yourself still uses port 4300 and a BFF on 8079.
- **The BFF runs in the `Docker` environment**, like the other services, so it reads its
  `appsettings.Docker.json`. Its database is `link-nhsn-app-bff` on the AppHost's SQL Server.

### Searching the logs

The dashboard's console view shows each service's output as it happens. To search across services, or
back in time, use Grafana at http://localhost:13000 (no login), or the Admin UI's **Logs > Grafana**. It
has one data source, Loki, which every service writes to.

Open **Explore** and query by label. Every line carries `app` (`link-cloud`) and `component` (the
service):

```
{component="Validation"} |= "rubric"
{app="link-cloud"} |~ "(?i)exception"
{component=~"DataAcquisition.*"} |= "error"
```

Loki keeps nothing between AppHost runs, so a search only reaches back to the last start. Grafana keeps
nothing either: its data source is written in on each start, and a dashboard you build there is gone on
the next one.

The dashboard's Structured logs view stays empty: the services send it traces and metrics, and their
logs go to the console and to Loki.

### Scaling a service out

`Replicas:<service>` in `appsettings.json` (or user secrets, or `Replicas__validation=3` in the
environment) runs that many copies of a service, as a deployed environment scales one horizontally. The
name is the resource's name in the dashboard; every service is listed in `appsettings.json` at `1`.

```powershell
dotnet user-secrets set "Replicas:validation" "3"
```

- **Every copy is the same service.** Same code, same settings, same Kafka consumer groups, so a topic's
  partitions are split between the copies. Whatever a service does at startup (migrations, Hibernate's
  schema update, Quartz jobs) each copy does, as it would deployed. If a service misbehaves with two
  copies here, it would with two deployed: that is a finding, not something the AppHost hides.
- **Three is the useful limit for a Kafka consumer.** Every topic has three partitions, and a consumer
  group gives each partition to one copy, so a fourth copy sits idle. The AppHost logs a warning above 3.
- **.NET services** use Aspire's replicas: one resource in the dashboard with several instances, behind
  the proxy on the service's usual port, which spreads requests across them.
- **MeasureEval and Validation** are Java executables, which Aspire's replicas don't cover, so each copy
  is a resource of its own: `validation`, `validation-2`, `validation-3`. The first keeps the usual port
  and receives every HTTP call and the dashboard commands; the others are on ports Aspire picks. Their
  value is the extra Kafka consumers.
- **Every log line says which copy wrote it.** .NET lines carry a `ServiceInstance` property
  (`dataacq-worker-rynurthc`, the name the dashboard shows the replica with), and Java lines end with
  `[instance=validation-2]`. The AppHost sets `LINK_SERVICE_INSTANCE` on each copy; deployed, where nothing sets
  it, the services use `HOSTNAME`, which Kubernetes sets to the pod's name.

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
- Kafka is plain (no SASL), and its topics come from `topics.txt`: `kafka-init` creates any that are
  missing on each start, and Kafka counts as healthy only once it has.
- Each service gets its own database (`db-<name>` in the dashboard), created on start and migrated by
  the service (`AutoMigrate`), as under compose. Validation's schema is updated by Hibernate. The
  dashboard command **Apply Validation migrations** also runs
  `Java/validation/src/main/resources/database/migrations/V*.sql`, as the deployment pipeline does.
- Telemetry goes to the Aspire dashboard, and logs go to its console view and to Loki. Grafana
  (http://localhost:13000, also the Admin UI's Logs > Grafana) searches Loki; see
  [Searching the logs](#searching-the-logs).

### Project layout

| Path | What it holds |
|---|---|
| `Program.cs` | Wires every resource together: infrastructure, services, and how sign-in works |
| `Properties/launchSettings.json` | The `http` and `https` profiles |
| `Ports.cs` | Every fixed host port |
| `Extensions/ReplicaExtensions.cs` | `Replicas:<service>`: how many copies of each service run |
| `Security/LinkSystemToken.cs` | The Link bearer token the AppHost's own commands send |
| `Security/AuthMode.cs` | `Auth:Mode`: Anonymous (http profile) or Keycloak (https profile) |
| `Security/LinkServiceClient.cs` | HTTP clients for the AppHost's own calls to services, carrying the Link system token |
| `Security/LinkKeycloak.cs` | Keycloak's URLs and parameters, its admin token, and creating users |
| `Extensions/InfrastructureExtensions.cs` | The containers: SQL Server, Kafka (with `kafka-init`, Kafka UI and the REST proxy), Mongo, Redis, Azurite, Loki, Grafana, HAPI FHIR, SFTP |
| `Extensions/DotNetServiceExtensions.cs` | How a .NET service is run and configured |
| `Extensions/JavaServiceExtensions.cs` | The Maven build, MeasureEval, Validation, and the Validation migrations command |
| `Extensions/AdminUiExtensions.cs` | The Admin UI under `ng serve` |
| `Extensions/NhsnAppExtensions.cs` | The NHSN App BFF, the NHSN App UI under `webpack serve`, and the key pair they share |
| `Extensions/KeycloakExtensions.cs` | Reads `Auth:Mode`, and adds Keycloak with the development certificate |
| `Extensions/SeedDataExtensions.cs` | Seed data and the **Seed data** command |
| `Extensions/AccountUserExtensions.cs` | The **Create user** command |
| `Extensions/FacilitySetupExtensions.cs` | The **Create test facility** command, which runs `FacilitySetup/steps.json` |
| `Extensions/CommandNotifications.cs` | The running, success and failure notices the dashboard commands show |
| `AdminUi/proxy.mjs` | The Admin UI's `ng serve` proxy |
| `NhsnAppUi/webpack.config.js` | The NHSN App UI's webpack config: the UI's own, with `/api` proxied to this host's BFF |
| `Keycloak/link-realm.json` | The `link` realm Keycloak imports |
| `FacilitySetup/` | `steps.json` and the request payloads, by service, that **Create test facility** sends |
| `SeedData/Validation/bare-minimum-requirements.yaml` | The validation rubric **Seed data** publishes, and Validation's shadow comparison uses |

## Dashboard commands

Commands that span several services are on `admin-bff`, the platform's front door. A command that
belongs to one service is on that service. A command is greyed out until its service is healthy. While
a command runs, a notice at the top of the dashboard says so. It's replaced by a green notice on success
or a red one on failure, which stays until dismissed.

- **Seed data** (on `admin-bff`): creates the local seed data that's missing. Link doesn't seed reference
  data through migrations, so a new environment has none of it. There are three parts, and each one
  also runs automatically when its service turns healthy:
  - **Roles (Account):** `LinkUser`, with the `IsLinkAdmin` permission.
  - **Validation:** its artifacts and categories, initialised from the files it ships with, when
    Validation has none yet. Then the rubric in `SeedData/Validation/` is registered and published if
    it isn't already. The AppHost sets `vaas.bridge.rubric-id` to that rubric's id, so the shadow
    comparison has a published rubric to use.
  - **Measures (MeasureEval):** the three bundles in `DotNet/Automation/measures/`. Before a bundle is
    stored, each of its value sets is expanded through Terminology, since the bundles ship without
    expansions and MeasureEval otherwise falls back to the compose definition. A value set Terminology
    doesn't know is left as it is. A measure is loaded if MeasureEval doesn't have it, and reloaded if
    the stored copy has fewer expanded value sets. Tenant refuses a facility that schedules a measure
    MeasureEval doesn't know.

  Every part goes through the service's own API and only adds what's missing: the one change to
  existing data is reloading a measure to add expansions. Add more parts in
  `Extensions/SeedDataExtensions.cs`.
- **Create test facility** (on `admin-bff`): a form for facility id and name, time zone, measure (from
  the ones MeasureEval has), EHR vendor and FHIR server. The defaults are `dev-facility`, `Dev Facility`,
  `America/Chicago`, `NHSNAcuteCareHospitalMonthlyInitialPopulation`, Epic and QA's FHIR server. The id
  takes letters, digits and hyphens; an empty name becomes the id. If MeasureEval has no measures yet,
  the measure is a text box with a warning to run Seed data first. It then runs
  `FacilitySetup/steps.json`, which:
  - creates the DMRP measure mapping and the facility in Tenant;
  - configures Census (disabled), QueryDispatch, DataAcquisition (Monthly and Discharge query plans,
    the FHIR query configuration and the patient lists) and Normalization. Normalization is skipped as
    a whole if the facility already has operations.

  The EHR vendor picks the query plans: `query-plan-*-epic.json` or `query-plan-*-cerner.json`, each a
  copy of the ACH Monthly plan production uses for that vendor. Epic fetches Encounter first and
  everything else as supplemental data, using `_include` for Locations, Medications and Specimens;
  Cerner fetches those through Reference queries and also fetches MedicationAdministration. A facility
  that already has plans keeps them, whichever vendor is picked.

  The steps and payloads come from the QA Postman collections. They're read when the command runs,
  so editing them needs no rebuild. Running it again for the same facility only creates what's
  missing. The FHIR server can be the AppHost's mock (`http://localhost:16158/fhir`), a deployed
  server, or your own. The default is QA's `https://ehr-test.nhsnlink.org/fhir`, the only one where
  the query list's patient lists exist; they matter only to Census, which the facility has disabled.
  See `FacilitySetup/steps.json` for the format: placeholders, existence checks, captured ids and
  groups.
- **Create user** (on `account`, Keycloak mode only): a form for first name, last name, email,
  username (optional; the email is used if it is empty), role and password. The roles are read from
  Account, which is seeded with them first if it has none. It creates the user in Keycloak, to
  sign in with, and in Account, through Account's own `POST /api/account/user`. Each side is tried
  even if the other already has the user, so after a Keycloak or SQL Server reset, running it again
  fills in whichever is missing. It reads the Account user back to check the role was really
  assigned, because Account skips a role it can't find and still answers 201. It isn't offered in the
  http profile: no one signs in there, so there's no user to create.
- **Apply Validation migrations** (on `validation`): runs
  `Java/validation/src/main/resources/database/migrations/V*.sql` in order. The `U*.sql` undo scripts
  beside them are not run.

## Things to know

- **Don't run the same service under the AppHost and from your IDE at once.** They use different
  Kafka brokers, so they won't share a consumer group, but they would each process their own copy
  of everything.
- **Local secrets.** The .NET services keep secrets in one file per Windows user,
  `%LOCALAPPDATA%\Link\Secrets\link-local-secrets.enc`, which DataAcquisition also uses to hand a
  facility's FHIR credentials to its worker. The AppHost gives every service the same Data Protection
  key ring (`Link`), so all of them can read it. A service that can't decrypt it won't write over it:
  it logs "Failed to decrypt secrets file ... It will not be written over" and keeps its own secrets
  in memory, and saving a facility's credentials fails. That happens when the file was written under
  another key ring, for example by a service run from your IDE with different settings. **Delete the
  file**, even with the services running: the next one to save a secret writes a new one.
- **Data.** SQL Server, MongoDB, Azurite, Kafka, Keycloak and the SFTP server keep their data in Docker
  volumes (`link-aspire-*`) between runs. Redis and the HAPI FHIR server start empty each time. Submitted
  reports are also written to `%TEMP%\link-aspire\submission`.
  - **Kafka keeps its consumer groups' committed offsets, as compose does.** Most Link consumers start
    a new group at the latest message, so with nothing kept, every run's groups were new. A message
    sent before its consumer joined was then lost; Report replaying a stored report at startup lost
    its acquisition requests that way. Now only the very first run is exposed.
  - **To reset everything,** stop the AppHost and run
    `docker volume rm link-aspire-sql link-aspire-mongo link-aspire-azurite link-aspire-kafka link-aspire-keycloak link-aspire-sftp`.
    Then follow [From nothing to your first ad-hoc report](#from-nothing-to-your-first-ad-hoc-report)
    from step 3.
- **The Admin UI** runs `ng serve` with `AdminUi/proxy.mjs`, which proxies `/api` to the Admin BFF.
  It also answers `/assets/app.config.local.json`, which the UI merges over `app.config.json`, with
  the AppHost's overrides (`authRequired`, and `grafanaUrl` for **Logs > Grafana**). Nothing is
  written into `Web/Admin.UI`, and `proxy.conf.json` is left alone. If you have your own
  `src/assets/app.config.local.json`, it is served first and wins. Packages are installed (`npm ci`,
  shown as `admin-ui-install`) only when `node_modules/@angular/cli` is missing.
- **Java telemetry.** Under the http profile MeasureEval and Validation are given the dashboard's OTLP
  address but not its API key: Aspire's usual way of passing the key sets variables the OpenTelemetry
  Spring starter cannot read, and they stop the services from starting. Their traces may not show on the
  dashboard. Under the https profile they are given no address, so their telemetry is off: the dashboard
  serves OTLP over HTTPS with the .NET development certificate there, which the JVM does not trust, and
  every export failed with "PKIX path building failed". Each service logs once at startup that
  telemetry is not set. Their logs reach the console view and Loki under both profiles.
- **Not included:** Tempo and Prometheus. Traces and metrics are on the Aspire dashboard instead.
