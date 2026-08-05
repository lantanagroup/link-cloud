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

---

## 2. How this project is built — contract-first with NSwag

This is the part worth reading before changing anything.

### 2.1 The spec is the source of truth

`Contracts/dmrp-openapi.yaml` describes the API with production operation names. The words
"test" and "mock" appear nowhere in it.

Every path is **relative to the server URL**, and that URL carries the `dmrp/mock` prefix:

```yaml
servers:
  - url: http://localhost:6159/dmrp/mock
```

So the spec's `/search` resolves to `http://localhost:6159/dmrp/mock/search`, matching the
`[Route("dmrp/mock")]` on the controller. Repointing a consumer at another instance — or
eventually at the real DMRP — is a change to that one server URL and nothing else.

⚠️ **The prefix is declared twice, and that is deliberate.** Because the server URL has a path
component, NSwag emits a class-level `[Route("dmrp/mock")]` on the generated base as well.
Attribute routing takes the most-derived declaration rather than combining them, so the prefix
is applied once — `ThePrefixIsAppliedExactlyOnce` in `DmrpControllerTests` pins that.

The explicit route on the controller is what actually decides the served path. Keep it: if the
server URL were ever shortened to `http://localhost:6159`, the generated route would become
empty and, without the explicit one, every endpoint would silently move to `/`.

### 2.2 What happens on build

```
Contracts/dmrp-openapi.yaml
        │
        │  NSwag  (MSBuild target "GenerateDmrpContract", before CoreCompile)
        ▼
obj/NSwag/DmrpApi.Generated.cs
        │
        ├── abstract class DmrpControllerBase   routes, [ProducesResponseType], abstract methods
        └── DTOs                                ReportingPlanEntry, ReportingPlanResponse, …
        │
        │  we override
        ▼
Presentation/Controllers/DmrpController.cs
```

The target's `Inputs`/`Outputs` make `dotnet build` incremental — codegen re-runs only when
the spec or `nswag.json` changes. **`dotnet publish` regenerates every time**, so the
publish-then-restart loop always picks up a spec edit.

### 2.3 Validation rules live in the spec, not the C#

NSwag emits `[Required]`, `[Range]` and `[StringLength]` from the schema's `required`,
`minimum`/`maximum` and `maxLength`. So a range change is a **yaml edit**, not a code edit.
`sortBy` is a closed enum in the spec for the same reason: the repository resolves the sort
field by property name and throws for anything that is not one, which would turn ordinary
client input into a server fault.

### 2.4 What we write by hand

| File | Role |
|---|---|
| `Presentation/Controllers/DmrpController.cs` | Overrides the generated base; HTTP plumbing only |
| `Presentation/Controllers/NhsnAuthController.cs` | The `auth-test` route; outside the contract |
| `Application/Services/ReportingPlanService.cs` | Storage and querying |
| `Application/Services/AuthTokenService.cs` | Issues and validates tokens |
| `Application/Mapping/EntryMapper.cs` | Entity ⇄ generated DTO; keeps the contract out of the database |

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

### 2.7 ⚠️ Two traps when overriding the generated base

Neither is inherited from the base, and neither produces a compiler warning. Both are covered
by `GeneratedControllerBindingTests` in `DotNet/ServiceTests`, which will fail loudly if an
NSwag or ASP.NET upgrade changes the behaviour.

1. **Default parameter values do not carry over.** An override that drops `= 10` binds `null`
   and silently unpages the endpoint.
2. **Optional string filters are typed non-nullable.** NSwag emits `string facilityId`, not
   `string?`, and `[ApiController]` treats a non-nullable reference parameter as required —
   so the contract's optional filters return `400` until the override restates them as
   `string?`.

Binding *source* attributes (`[FromQuery]`, `[FromBody]`) **do** survive, because MVC resolves
them through the base declaration. Keep `[ApiController]`: it is what infers `Path` for route
parameters carrying only `[BindRequired]`.

### 2.8 Replacing the spec when Leidos publishes theirs

1. Replace `Contracts/dmrp-openapi.yaml` **wholesale**. Do not hand-merge.
2. `dotnet build DotNet/MockDmrpApi/MockDmrpApi.csproj`
3. Read the compile errors — they enumerate the work.
4. Fix each override in `DmrpController.cs`, restating defaults and nullability per §2.7.
5. Run `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~MockDmrpApi`.

Never edit the generated file, and never add routes that exist only on this stand-in to the
spec — those belong in `NhsnAuthController`.

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

Spec paths are relative to the server URL, which carries the `dmrp/mock` prefix (§2.1), so
the served URL is the prefix plus the path.

| Spec path | Served at | Purpose |
|---|---|---|
| `GET /reporting-plans` | `/dmrp/mock/reporting-plans` | **The consumer-facing query.** Requires a bearer token |
| `POST /oauth2/token` | `/dmrp/mock/oauth2/token` | Issues a token. Client credentials, JSON body |
| `POST /` | `/dmrp/mock` | Create an entry → `201` + `Location` |
| `GET /{id}` | `/dmrp/mock/{id}` | One entry. `400` on a non-GUID, `404` if absent |
| `PUT /{id}` | `/dmrp/mock/{id}` | Update → `202`. **Never creates**; `404` if absent |
| `GET /facilities/{facilityId}` | `/dmrp/mock/facilities/{id}` | A facility's entries, paged. `204` when none |
| `GET /search` | `/dmrp/mock/search` | Filtered search, paged. `204` when none |
| `DELETE /{id}` | `/dmrp/mock/{id}` | `204`, or `404` if absent |
| `DELETE /facilities/{facilityId}` | `/dmrp/mock/facilities/{id}` | Idempotent `204` |
| `DELETE /` | `/dmrp/mock` | Removes **every** entry. No confirmation step |

**Not in the spec** — this stand-in only:

| Route | Notes |
|---|---|
| `GET /dmrp/mock/auth-test` | Query-parameter convenience form of the token endpoint. The secret travels in the query string, which reaches access logs; integrate against `POST /oauth2/token` |
| `GET /health`, `GET /api/mock-dmrp/info` | Answer even when the service is disabled |

There is deliberately **no `api-test` route**. The reporting plan query belongs to the real
API and is already served at `/dmrp/mock/reporting-plans`; a second path to it would invite a
consumer to integrate against something the real service does not have.

---

## 4. Enrollment is conveyed by presence

The single most important behaviour, and the one most likely to be mis-implemented.

**A measure that does not appear in `measures` means the facility is NOT enrolled in it.**
There is no negative representation. `isReporting` is `"Y"` wherever an entry exists.

Seed only `HOB` for TestFacility01, May 2026:

```json
{
  "facilityId": "TestFacility01",
  "reportingMonth": 5,
  "reportingYear": 2026,
  "measures": [ { "measure": "HOB", "isReporting": "Y" } ],
  "retrievedOn": "2026-05-01T12:00:00+00:00"
}
```

The caller concludes "not enrolled in HTCDI" from its absence.

A facility enrolled in nothing returns **`200` with `"measures": []`** — not `204`, not `404`
— because an empty plan is a meaningful answer rather than an absent resource. This
deliberately differs from `/search` and `/facilities/{id}`, which do return `204` when empty.
An entry stored with `isReporting` other than `"Y"` is excluded from a plan entirely.

---

## 5. Seeding a scenario

With the service running (see §7):

```bash
B=http://localhost:6159

# Start clean
curl -X DELETE $B/dmrp/mock

# Enrol TestFacility01 in HOB for May 2026
curl -X POST $B/dmrp/mock -H 'Content-Type: application/json' \
  -d '{"facilityId":"TestFacility01","measure":"HOB","reportingMonth":5,"reportingYear":2026,"isReporting":"Y"}'

# Acquire a token
TOKEN=$(curl -s -X POST $B/dmrp/mock/oauth2/token -H 'Content-Type: application/json' \
  -d '{"grant_type":"client_credentials","client_id":"link-cloud-dev","client_secret":"link-cloud-dev-secret","scope":"dmrp.read"}' \
  | jq -r .access_token)

# Query the plan
curl -s -H "Authorization: Bearer $TOKEN" \
  "$B/dmrp/mock/reporting-plans?facilityId=TestFacility01&reportingMonth=5&reportingYear=2026" | jq
```

The natural key is `(facilityId, measure, reportingYear, reportingMonth)`, enforced by a
unique index — a second create with the same combination is a `409`, so the store cannot hold
two contradictory rows for one facility, measure and period.

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

### 6.2 The token, and where it differs from real NHSN Auth

Issued tokens are genuine signed JWTs (HS512) carrying `iss`, `aud`, `sub`, `scope`, `iat`,
`nbf`, `exp` and `jti`, with a real expiry — so a caller's acquire, cache and
refresh-on-expiry path is exercised rather than trivially satisfied.

⚠️ **There is no discovery document and no JWKS, and the signature is symmetric.** A consumer
cannot point `AddJwtBearer(o => o.Authority = …)` at this service and get signature validation
working the way it will against real NHSN Auth, which is almost certainly RS256 with published
keys. **This is the one seam where mock-tested integration code is not the code that ships.**
Upgrading to RS256 + JWKS + discovery later is additive and would not disturb the token
endpoint's contract.

One consequence of the contract typing `grant_type` as a single-value enum: an unknown grant
is rejected by model binding as a validation `400` and never reaches the
`unsupported_grant_type` branch a real authorization server would use.

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

`/swagger` is available when `EnableSwagger` is true. It is a Swashbuckle-reflected OpenAPI
**2.0** document generated from the controllers; `Contracts/dmrp-openapi.yaml` is a
hand-authored OpenAPI **3.0.3** document and is the actual contract. The two describe the same
routes but are produced by opposite processes, so they can disagree in detail. **Read the
yaml, not the Swagger page**, when you need the contract.

---

## 8. Open questions for Leidos

Everything below is invented. Getting a real request/response pair, or the published spec,
matters more than anything else in this project — a consumer written against these guesses
will work perfectly here and fail on first contact with the real endpoint.

- **Path shapes** — `/reporting-plans`, `/oauth2/token`, `/facilities/{id}`, `/search`
- **Field names** — `facilityId` vs `orgId`/`orgID`; `measures` vs `reportingPlan`; whether
  `retrievedOn` exists at all
- **`isReporting`** — genuinely the string `"Y"` on the wire, or a boolean the ticket
  paraphrased?
- **`measure`** — a short name (`HOB`, `HTCDI`) or a coded identifier?
- **The period** — separate month and year integers, or one `reportingPeriod` like `2026-05`?
- **Writes** — does DMRP expose them at all, or is it read-only with enrollment managed
  elsewhere?
- **Auth** — is NHSN Auth a separate service with its own base URL, and does it use RS256
  with a published JWKS?

---

## 9. Project structure

```
DotNet/MockDmrpApi/
├── Contracts/dmrp-openapi.yaml       Source of truth. Drives codegen
├── nswag.json                        Codegen configuration
├── Program.cs
├── Settings/                         DmrpApiConstants, DmrpApiSettings
├── Domain/
│   ├── Entities/                     ReportingPlanEntryEntity
│   └── Context/                      ReportingPlanDbContext + mapping
├── Migrations/                       InitMockDmrp
├── Application/
│   ├── Middleware/                   DmrpAvailability, DmrpDisabledMiddleware
│   ├── Models/                       ReportingPlanSearchCriteria
│   ├── Mapping/                      EntryMapper
│   └── Services/                     ReportingPlanService, AuthTokenService
└── Presentation/Controllers/         DmrpController, NhsnAuthController
```

Tests live in `DotNet/ServiceTests` under `UnitTests/MockDmrpApi` and
`IntegrationTests/MockDmrpApi`.

```bash
dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~MockDmrpApi
```
