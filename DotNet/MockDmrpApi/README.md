# MockDmrpApi

> **The contract in this project is provisional.** DMRP (Digital Monthly Reporting Plan) is
> being developed by **Leidos for CDC**, and no OpenAPI description has been published to
> LCG. `Contracts/dmrp-openapi.yaml` is a reconstruction from what we know — every path,
> field name and status code in it is a guess. Do not treat any of it as authoritative, and
> see [§8](#8-open-questions-for-leidos) before building against it.

`MockDmrpApi` stands in for the DMRP API and for the NHSN Auth API it sits behind. The real
DMRP endpoint is reachable only from the CDC Dev environment, so local development and the
LCG environments have no other way to exercise code that depends on it.

---

## 1. What it is for

A consumer needs two things from DMRP: a token, and the set of measures a facility is
enrolled to report for a period. This service provides both, backed by a database you can
seed over REST so a test sets up an exact scenario.

It is a **stand-in, not a simulation of CDC's implementation**. It returns what you put in
it. Its value is that a caller has to acquire a token, present it, and interpret a reporting
plan — the same sequence it will perform against the real service.

### 1.1 ⚠️ Two surfaces, and only one of them is DMRP

Everything this service serves falls into one of two groups, and conflating them is the
mistake most likely to waste someone's week.

| | **Contract surface** | **Support surface** |
|---|---|---|
| Routes | `GET /msc`, `GET /ps/annual` | everything under `/mock` |
| Whose API is it? | The third party's | Ours |
| In `Contracts/dmrp-openapi.yaml`? | Yes — it describes *only* these | No, deliberately |
| Authentication | The third party's bearer token | **Link's standard scheme** |
| Exists in real DMRP? | Yes | **No** |

**A consumer integrates against the contract surface only.** The support surface exists so a
test can seed and inspect the data those two endpoints then serve; it has no counterpart in
the real service, and code written against it has nothing to talk to in production.

The split is why replacing the contract when Leidos publishes theirs is safe: nothing of ours
lives in that document, so it can be swapped wholesale (§2.8).

### 1.2 The two components

The two contract endpoints differ in **subject and in cadence**, which is why they take
different parameters.

| Endpoint | Component | Subject | Cadence | Parameters |
|---|---|---|---|---|
| `GET /msc` | `MSC` | Medicine reports | Monthly | facility + month + year |
| `GET /ps/annual` | `PS` | Patient safety | Annual | facility + year |

That cadence difference reaches the schema: `ReportingMonth` is **nullable**, populated for
MSC and null for PS. Whether it is required depends on the component, which no column
constraint or range annotation can express, so the service enforces it — see §4.1.

⚠️ **Both endpoints are placeholders.** Their shape is expected to change once the published
contract arrives, and the strings `"MSC"` and `"PS"` are our invention. They are cheap to
change while nothing is deployed.

---

## 2. How this project is built — contract-first with NSwag

This is the part worth reading before changing anything.

### 2.1 The spec is the source of truth — for the contract surface only

`Contracts/dmrp-openapi.yaml` describes **the two contract endpoints and nothing else**. It
uses production operation names; the words "test" and "mock" appear nowhere in it. The support
surface at `/mock` is hand-written and deliberately absent from it (§1.1).

The server URL carries **no path component**:

```yaml
servers:
  - url: http://localhost:6159
```

So the spec's `/msc` resolves to `http://localhost:6159/msc`, and the operations sit at the
root. `DmrpController` therefore declares **no `[Route]` of its own** — routing comes entirely
from the generated base. Repointing a consumer at another instance, or eventually at real
DMRP, is a change to that one server URL and nothing else.

That is the point of putting the contract endpoints at the root and our own under `/mock`:
the base URL is the only thing that has to change, because none of our paths would come with
it. `TheContractEndpointsSitAtTheRootWithNoPrefix` in `DmrpControllerTests` pins it.

### 2.2 What happens on build

```
Contracts/dmrp-openapi.yaml
        │
        │  NSwag  (MSBuild target "GenerateDmrpContract", before CoreCompile)
        ▼
obj/NSwag/DmrpApi.Generated.cs
        │
        ├── abstract class DmrpControllerBase   routes, [ProducesResponseType], 2 abstract methods
        └── DTOs                                ReportingPlanResponse, ReportingPlanMeasure, …
        │
        │  we override
        ▼
Presentation/Controllers/DmrpController.cs      the contract surface

Presentation/Controllers/MockController.cs      the support surface — untouched by any of this
```

The target's `Inputs`/`Outputs` make `dotnet build` incremental — codegen re-runs only when
the spec or `nswag.json` changes. **`dotnet publish` regenerates every time**, so the
publish-then-restart loop always picks up a spec edit.

### 2.3 Validation rules live in the spec, not the C#

NSwag emits `[Required]`, `[Range]` and `[StringLength]` from the schema's `required`,
`minimum`/`maximum` and `maxLength`, and `[BindRequired]` for required query parameters. So a
range change on the contract surface is a **yaml edit**, not a code edit.

This applies to the contract surface only. The support surface carries its own data
annotations in `Application/Models/MockEntryModels.cs`, and `sortBy` there is a C# enum for
the reason a closed set matters: the repository resolves the sort field by property name and
throws for anything that is not one, which would turn ordinary client input into a server
fault.

### 2.4 What we write by hand

| File | Role |
|---|---|
| `Presentation/Controllers/DmrpController.cs` | Overrides the generated base. Validate the token, select by component, project |
| `Presentation/Controllers/MockController.cs` | The whole support surface, including the token endpoint. Outside the contract |
| `Application/Models/MockEntryModels.cs` | The support surface's own request/response models |
| `Application/Models/MockTokenModels.cs` | Token exchange models |
| `Application/Services/ReportingPlanService.cs` | Storage, querying, and the component/cadence rules |
| `Application/Services/AuthTokenService.cs` | Issues and validates the third-party token |
| `Application/Mapping/EntryMapper.cs` | Entity → generated DTO. The only seam where contract types appear |

The support surface has its **own models rather than borrowing the generated ones**. If it
shared them, replacing the third party's contract would break endpoints that have nothing to
do with it.

### 2.5 Why the generated code is not committed

It lands in `obj/` and is gitignored. Committing it would reintroduce the exact problem
contract-first solves: the C# and the spec could disagree the moment someone edits one
without rebuilding, and nothing would catch it until a consumer hit the mismatch at runtime.
In `obj/` they cannot diverge. It also keeps a contract change reviewable as a **spec diff**
rather than thousands of lines of mechanical churn.

### 2.6 ⚠️ The fresh-clone gotcha

After cloning, the generated types do not exist until you build once, so the IDE shows errors
in `DmrpController.cs`. This is not a broken checkout.

```
dotnet build DotNet/MockDmrpApi/MockDmrpApi.csproj
```

### 2.7 ⚠️ Traps when overriding the generated base

None of these is inherited from the base, and none produces a compiler warning. All are
covered by `GeneratedControllerBindingTests` in `DotNet/ServiceTests`, which will fail loudly
if an NSwag or ASP.NET upgrade changes the behaviour.

What **does** survive into the override, because MVC resolves parameter attributes through the
base declaration:

- Binding *source* attributes — `[FromQuery]`, `[FromBody]`
- Validation attributes — `[BindRequired]`, so a missing required parameter is still a `400`
- Routes, including the method-level ones the current contract relies on entirely

What does **not**:

1. **Default parameter values.** An override that drops `= default` gets none. Harmless for the
   cancellation token MVC supplies anyway; not harmless for a `pageSize = 10`, where the loss
   silently unpages an endpoint.
2. **Nullability of optional string filters.** NSwag emits `string facilityId`, not `string?`,
   and `[ApiController]` treats a non-nullable reference parameter as required — so filters the
   contract documents as optional return `400` until the override restates them as `string?`.

The current two-operation contract has no optional parameters, so trap 2 has no live example
and trap 1 only the cancellation token. **Both are near-certain to matter again** once a
fuller contract lands with paging and filters, which is why they are documented here rather
than deleted. Keep `[ApiController]`: it is what infers `Path` for route parameters carrying
only `[BindRequired]`.

### 2.8 Replacing the spec when Leidos publishes theirs

1. Replace `Contracts/dmrp-openapi.yaml` **wholesale**. Do not hand-merge.
2. `dotnet build DotNet/MockDmrpApi/MockDmrpApi.csproj`
3. Read the compile errors — they enumerate the work.
4. Fix each override in `DmrpController.cs`, restating defaults and nullability per §2.7.
5. Run `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~MockDmrpApi`.

`MockController` and its models should need **no changes at all**. If a spec replacement forces
edits there, the two surfaces have become entangled and that is the thing to fix.

Never edit the generated file, and never add routes that exist only on this stand-in to the
spec — those belong on `MockController`.

### 2.9 Troubleshooting

| Symptom | Cause |
|---|---|
| `$(NSwagExe_Net80)` not found | `dotnet restore` has not run; `NSwag.MSBuild` provides it |
| Build succeeds but generated types are missing | Check `namespace` in `nswag.json` |
| Docker build cannot find the spec | `.dockerignore` is excluding `Contracts/*.yaml` |
| DTOs suddenly carry Newtonsoft attributes | `jsonLibrary` in `nswag.json` is no longer `SystemTextJson` |
| Need to skip codegen (air-gapped agent) | `dotnet build /p:RunNSwagCodeGen=false`, once the file exists |

---

## 3. Route map

### Contract surface — in the spec, this is DMRP

Authenticated with the **third party's** bearer token, from `POST /mock/oauth2/token`.

| Route | Purpose |
|---|---|
| `GET /msc?facilityId=&reportingMonth=&reportingYear=` | Monthly medicine reporting plan (`MSC`) |
| `GET /ps/annual?facilityId=&reportingYear=` | Annual patient-safety reporting plan (`PS`) |

Both return `ReportingPlanResponse`. Both answer `200` with an empty `measures` array for a
facility enrolled in nothing (§4), and `401` without a valid token.

### Support surface — ours, not in the spec, no counterpart in real DMRP

Authenticated with **Link's** standard scheme (`IsLinkAdmin`).

| Route | Purpose |
|---|---|
| `POST /mock/oauth2/token` | Issues the third-party token the contract surface accepts |
| `POST /mock` | Create an entry → `201` + `Location` |
| `GET /mock/{id}` | One entry. `400` on a non-GUID, `404` if absent |
| `PUT /mock/{id}` | Update → `202`. **Never creates**; `404` if absent |
| `GET /mock/facilities/{facilityId}` | A facility's entries across both components, paged. `204` when none |
| `GET /mock/search` | Filtered search, paged. `204` when none |
| `DELETE /mock/{id}` | `204`, or `404` if absent |
| `DELETE /mock/facilities/{facilityId}` | Idempotent `204` |
| `DELETE /mock` | Removes **every** entry. No confirmation step |

### Unauthenticated

| Route | Notes |
|---|---|
| `GET /health`, `GET /api/mock-dmrp/info` | Answer even when the service is disabled |

⚠️ The token endpoint sits on the support surface but hands out a **contract-surface**
credential. That is deliberate: a caller acquires a third-party token through Link's
authenticated surface, then presents it to the endpoints impersonating the third party —
the same acquire-then-use sequence it will perform for real.

---

## 4. Enrollment is conveyed by presence

The single most important behaviour, and the one most likely to be mis-implemented.

**A measure that does not appear in `measures` means the facility is NOT enrolled in it.**
There is no negative representation. `isReporting` is `"Y"` wherever an entry exists.

Seed only `HOB` for FAC001, May 2026, then `GET /msc`:

```json
{
  "facilityId": "FAC001",
  "reportingMonth": 5,
  "reportingYear": 2026,
  "measures": [ { "measure": "HOB", "isReporting": "Y" } ],
  "retrievedOn": "2026-05-01T12:00:00+00:00"
}
```

The caller concludes "not enrolled in HTCDI" from its absence.

A facility enrolled in nothing returns **`200` with `"measures": []`** — not `204`, not `404`
— because an empty plan is a meaningful answer rather than an absent resource. This
deliberately differs from `/mock/search` and `/mock/facilities/{id}`, which do return `204`
when empty. An entry stored with `isReporting` other than `"Y"` is excluded from a plan
entirely.

An annual plan omits `reportingMonth` from the response, rather than reporting a zero or a
stale value that would tell a consumer the plan covers one particular month.

### 4.1 ⚠️ The cadence rule, and why the service enforces it

A monthly component **requires** a reporting month; an annual one **must omit** it. The rule
is conditional on the component, so no column constraint or `[Range]` annotation can express
it — `ReportingPlanService` rejects violations with a `400` naming the cadence.

It has to be enforced rather than merely documented, because both failure modes are silent:

- A **PS entry saved with a month** satisfies the unique index perfectly well, and `/ps/annual`
  does not filter on month — so the row is returned for every request, looking correct.
- An **MSC entry saved without one** matches no month, so `/msc` never returns it. The plan
  simply comes back short, with nothing to indicate a row was skipped.

Neither produces an error anywhere. The only symptom is a reporting plan that is quietly wrong.

### 4.2 ⚠️ The unique index must not be filtered

The natural key is `(facilityId, component, reportingYear, reportingMonth, measure)`.

`ReportingMonth` is nullable, and **EF's default for a unique index over a nullable column is a
filtered index** — `WHERE [ReportingMonth] IS NOT NULL`. That would drop every annual row out
of the index and silently permit duplicate patient-safety entries, which is precisely what the
index exists to prevent. `ReportingPlanEntryMap` suppresses it with `.HasFilter(null)`, and
`TheUniqueIndexIsNotFiltered` pins that.

SQL Server then treats NULL as a single value in the index and rejects the second annual row
(error 2601), which is the behaviour wanted: one PS row per (facility, year, measure), one MSC
row per (facility, year, month, measure).

⚠️ **The integration fixture runs on SQLite, which does not share that behaviour** — it follows
the standard, where NULLs are distinct, so the index alone does not stop a duplicate annual
entry there. What holds on both providers is the service's own pre-check, and that is what the
tests assert.

---

## 5. Seeding a scenario

With the service running (see §7):

```bash
B=http://localhost:6159

# Start clean
curl -X DELETE $B/mock

# Enrol FAC001 in HOB for May 2026 -- monthly, so a month is required
curl -X POST $B/mock -H 'Content-Type: application/json' \
  -d '{"facilityId":"FAC001","component":"MSC","measure":"HOB","reportingMonth":5,"reportingYear":2026,"isReporting":"Y"}'

# ...and in HAI for 2026 -- annual, so a month must be omitted
curl -X POST $B/mock -H 'Content-Type: application/json' \
  -d '{"facilityId":"FAC001","component":"PS","measure":"HAI","reportingYear":2026,"isReporting":"Y"}'

# Acquire a third-party token
TOKEN=$(curl -s -X POST $B/mock/oauth2/token -H 'Content-Type: application/json' \
  -d '{"grant_type":"client_credentials","client_id":"link-cloud-dev","client_secret":"link-cloud-dev-secret","scope":"dmrp.read"}' \
  | jq -r .access_token)

# Query each plan -- each returns only its own component's measures
curl -s -H "Authorization: Bearer $TOKEN" "$B/msc?facilityId=FAC001&reportingMonth=5&reportingYear=2026" | jq
curl -s -H "Authorization: Bearer $TOKEN" "$B/ps/annual?facilityId=FAC001&reportingYear=2026" | jq
```

Against a deployed instance the `/mock` calls also need a Link bearer token; locally
docker-compose sets `Authentication:EnableAnonymousAccess`, as it does for every Link service
(§6).

A second create with the same natural key is a `409`, so the store cannot hold two
contradictory rows for one facility, component, measure and period. The same measure name
under **both** components is two legitimate rows, not a duplicate — the plans are independent.

---

## 6. Configuration

All under the `MockDmrpApi` section, or `MockDmrpApi__<Key>` as an environment variable.

| Setting | Type | Default | Notes |
|---|---|---|---|
| `Enabled` | bool | `true` | Master switch. See §6.1 |
| `AuthClientId` | string | `link-cloud-dev` | |
| `AuthClientSecret` | string | `link-cloud-dev-secret` | Published in docker-compose. Override anywhere deployed |
| `SigningKey` | string | a local-dev key | **≥ 64 bytes** (HS512 needs 512 bits) or the service fails at startup. **Must be identical on every replica** |
| `Issuer` | string | `link-mock-dmrp` | |
| `Audience` | string | `dmrp-api` | |
| `TokenLifetimeSeconds` | int | `3600` | |

Also read: `ConnectionStrings:DatabaseConnection`, `DatabaseProvider` (`SqlServer`),
`AutoMigrate`, `EnableSwagger`, `ExternalConfigurationSource`.

**Link authentication** uses the same global keys every other Link service does, all already
in `app-config.yaml`: `Authentication:EnableAnonymousAccess`,
`Authentication:Schemas:LinkBearer:{Authority,ValidateToken}`, `DataProtection:Enabled` and
`LinkTokenService:SigningKey`. Nothing service-specific was added.

`appsettings.Docker.json` sets `Authentication:EnableAnonymousAccess: true`, matching every
other service in the local stack — only `admin-bff` and `automation-ui` carry Link token
config in docker-compose, so enforcing it here would make seeding locally require a token
nothing in the stack conveniently issues. It is unset elsewhere, so deployed environments
enforce. The `401` behaviour of every `/mock` endpoint is covered by `MockControllerTests`
rather than by the local stack.

⚠️ **`SigningKey` must be the same value on every replica.** Tokens are validated by the same
service that issues them, so a per-instance key means a token minted by one pod is rejected by
another — an intermittent `401` that reads as a bug in the caller.

### 6.1 The availability switch

Two layers, and the outer one is absolute.

**Production never serves this service, whatever configuration says.** Azure App Configuration
is appended last in the configuration chain, so a row provisioned against a production label
would silently outrank appsettings and environment variables — and a running mock looks exactly
like a healthy service, so the failure would be invisible. That is closed off in code.

Everywhere else, `MockDmrpApi:Enabled` decides. When disabled:

- every route answers **`503`** with problem details carrying a `traceId`
- `/health` and `/api/mock-dmrp/info` keep answering, so the container stays healthy rather
  than looking like an outage
- **EF migration is skipped** — a dormant deployment has no business altering a schema
- a warning is logged naming the environment and the routes that remain

### 6.2 The two authentication systems

They are separate on purpose, mirroring the real topology.

| | Guards | Scheme |
|---|---|---|
| **Link's** | the support surface at `/mock` | `AddLinkBearerServiceAuthentication` + `[Authorize(Policy = IsLinkAdmin)]`, exactly as Terminology, Census and Tenant do |
| **The third party's** | `GET /msc`, `GET /ps/annual` | HS512 JWT minted by `POST /mock/oauth2/token`, validated by `AuthTokenService` |

`DmrpController` carries `[AllowAnonymous]` so Link's middleware never sees it, and checks the
third-party token itself. That is not a hole: those endpoints impersonate an external service,
and Link's credential has no meaning to it.

### 6.3 The token, and where it differs from real NHSN Auth

Issued tokens are genuine signed JWTs (HS512) carrying `iss`, `aud`, `sub`, `scope`, `iat`,
`nbf`, `exp` and `jti`, with a real expiry — so a caller's acquire, cache and
refresh-on-expiry path is exercised rather than trivially satisfied.

⚠️ **There is no discovery document and no JWKS, and the signature is symmetric.** A consumer
cannot point `AddJwtBearer(o => o.Authority = …)` at this service and get signature validation
working the way it will against real NHSN Auth, which is almost certainly RS256 with published
keys. **This is the one seam where mock-tested integration code is not the code that ships.**
Upgrading to RS256 + JWKS + discovery later is additive and would not disturb the token
endpoint's contract.

`grant_type` is typed as a **string rather than an enum**, so an unknown grant reaches the
service and comes back as `unsupported_grant_type` — the code a real authorization server
would use. Bound as an enum it would fail model binding and produce a generic validation
`400` instead.

---

## 7. Running it

### Docker Compose

```bash
docker compose up -d mock-dmrp-api
```

Listens on **6159** (8080 in the container). Depends on `mssql_init`, which creates the
`link-mock-dmrp` catalog; migration runs at startup.

### Locally

```bash
dotnet run --project DotNet/MockDmrpApi/MockDmrpApi.csproj
```

Uses `appsettings.Development.json`, which points at `localhost\SQLEXPRESS`.

### Migrations

```bash
cd DotNet/MockDmrpApi
ConnectionStrings__DatabaseConnection="Server=…;Initial Catalog=link-mock-dmrp;…" \
  dotnet ef database update --project MockDmrpApi.csproj --startup-project MockDmrpApi.csproj
```

The design-time factory reads that environment variable before falling back to a local
default. EF prefers a design-time factory over the application's service provider, so without
the variable `dotnet ef database update` targets the local default — worth knowing before
pointing schema changes at a server.

### ⚠️ Swagger is not the contract

`/swagger` is available when `EnableSwagger` is true. It is a Swashbuckle-reflected document
generated from the controllers, so it shows **both** surfaces — including everything under
`/mock`, which is not part of DMRP at all. `Contracts/dmrp-openapi.yaml` is the actual
contract, and it describes only the two endpoints in §3. **Read the yaml, not the Swagger
page**, when you need the contract; the Swagger page is useful for exercising the support
surface.

---

## 8. Open questions for Leidos

Everything below is invented. Getting a real request/response pair, or the published spec,
matters more than anything else in this project — a consumer written against these guesses
will work perfectly here and fail on first contact with the real endpoint.

- **Path shapes** — are `/msc` and `/ps/annual` the real paths, and are they the *only* two?
- **Component identifiers** — the strings `"MSC"` and `"PS"` are ours. Does the real API use
  these, longer names, or coded values?
- **Field names** — `facilityId` vs `orgId`/`orgID`; `measures` vs `reportingPlan`; whether
  `retrievedOn` exists at all
- **`isReporting`** — genuinely the string `"Y"` on the wire, or a boolean the ticket
  paraphrased?
- **`measure`** — a short name (`HOB`, `HTCDI`) or a coded identifier?
- **The period** — separate month and year integers, or one `reportingPeriod` like `2026-05`?
  And does the annual endpoint really take no month?
- **Response shape parity** — do both endpoints genuinely return the *same* schema, or does
  the annual one differ in ways we have flattened together?
- **Auth** — is NHSN Auth a separate service with its own base URL, and does it use RS256
  with a published JWKS?

---

## 9. Project structure

```
DotNet/MockDmrpApi/
├── Contracts/dmrp-openapi.yaml       Source of truth for the CONTRACT surface only
├── nswag.json                        Codegen configuration
├── Program.cs
├── Settings/                         DmrpApiConstants, DmrpApiSettings
├── Domain/
│   ├── Entities/                     ReportingPlanEntryEntity, ReportingComponents
│   └── Context/                      ReportingPlanDbContext + mapping
├── Migrations/                       InitMockDmrp
├── Application/
│   ├── Middleware/                   DmrpAvailability, DmrpDisabledMiddleware
│   ├── Models/                       ReportingPlanSearchCriteria,
│   │                                 MockEntryModels, MockTokenModels
│   ├── Mapping/                      EntryMapper  (entity → generated DTO)
│   └── Services/                     ReportingPlanService, AuthTokenService
└── Presentation/Controllers/
    ├── DmrpController.cs             Contract surface — overrides the generated base
    └── MockController.cs             Support surface — entirely hand-written
```

Tests live in `DotNet/ServiceTests` under `UnitTests/MockDmrpApi` and
`IntegrationTests/MockDmrpApi`.

```bash
dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~MockDmrpApi
```
