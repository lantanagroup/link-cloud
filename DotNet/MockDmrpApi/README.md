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
| Routes | `GET /msc`, `GET /ps/annual/mrp` | everything under `/api/mock-dmrp` |
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

The two contract endpoints differ in **subject and in cadence**:

| Endpoint | Component | Subject | Cadence |
|---|---|---|---|
| `GET /msc` | `MSC` | Medicine reports | Monthly |
| `GET /ps/annual/mrp` | `PS` | Patient safety | Annual |

That cadence difference reaches the schema: `ReportingMonth` is **nullable**, populated for
MSC and null for PS. Whether it is required depends on the component, which no column
constraint or range annotation can express, so the service enforces it — see §4.1.

⚠️ **The strings `"MSC"` and `"PS"` are our invention,** as is the response body. Both are
cheap to change while nothing is deployed.

### 1.3 The query parameters

Both endpoints take the same four, and **only the first is required**:

| Name | Description | Required | Type | Example |
|---|---|---|---|---|
| `nhsnorgid` | The Link Cloud facility identifier | **Yes** | string | `100` |
| `name` | The NHSN module (`HOB`, `HTCDI`). Not the dQM (ACH Monthly, Daily, …) | No | string | `HOB` |
| `year` | The year of the reporting period | No | string | `2020` |
| `month` | The month of the reporting period | No | string | `2` |

Two things about this are easy to get wrong:

- **`year` and `month` are strings, not integers.** The service parses them; a value that is
  not a whole number — or a month outside 1–12 — is a `400` rather than a filter that quietly
  matches nothing. A typo must not read as "enrolled in nothing", which is the one conclusion
  this API exists to convey.
- **`month` has no effect on `/ps/annual/mrp`.** Annual entries carry no month, so narrowing by one
  would exclude every row the endpoint is supposed to return. It is accepted for symmetry and
  ignored, and the response omits `reportingMonth` regardless.

The optional parameters are why `DmrpController` restates them as `string?`; see the second
trap in §2.7.

There is deliberately **no `day` parameter**. A facility enrolling in daily reporting is
agreeing to it for the whole month, so a day would have nothing to select.

### 1.4 ⚠️ Two response quirks that must not be tidied up

Both come from the real API, and this stand-in reproduces them exactly. Normalising either
would let a consumer write code that works here and fails on first contact with the real
endpoint — which is the one failure this service exists to prevent.

**The same values appear twice, with different types.**

| | Root object | Inside `plans` |
|---|---|---|
| Facility | `orgid`, **number** | `nhsnorgid`, **string** |
| Year | `year`, **number** | `year`, **string** |
| Month | `month`, **number** | `month`, **string** |

The generated C# reflects it: `int? Month` on `ReportingPlanResponse`, `string Month` on
`ReportingPlanItem`.

**The timestamps are not RFC 3339.** `modifyDate` and `createDate` are `2023-09-09 11:12:12.59`
— a space separator, two fractional digits, no timezone. They are typed as plain strings in the
contract rather than `date-time` precisely so they are emitted in that form; binding them as
dates would produce `2023-09-09T11:12:12.59+00:00`, which is well formed and not what a
consumer will have to parse in production.

One consequence of the numeric `orgid`: **a facility identifier that is not numeric cannot be
represented there**, and the root `orgid` comes back null. The string form survives in
`plans[].nhsnorgid`. Link facility identifiers are not always numeric, so this is worth
knowing before a consumer keys off `orgid`.

### 1.5 ⚠️ In production these are two APIs, not two paths

The DMRP integration ADR is explicit: Leidos deploys **a separate DMRP API per NHSN
component** — one for Medication Safety, one for Patient Safety — and Link Cloud has to query
each. Consolidating them is wanted but scheduled for a future release, so the multiple
endpoints have to be supported as they are.

This service hosts both under one base URL, because one stand-in is cheaper to run than two.
That is a deviation, and it has a practical consequence: **a consumer cannot switch to the
real thing by changing a single base URL.** It will need one per component, and the paths may
well not be `/msc` and `/ps/annual/mrp` once each has its own host.

Keeping the two operations distinct in the contract, rather than collapsing them into one
parameterised endpoint, is what makes that switch a routing change rather than a rewrite.

---

## 2. How this project is built — contract-first with NSwag

This is the part worth reading before changing anything.

### 2.1 The spec is the source of truth — for the contract surface only

`Contracts/dmrp-openapi.yaml` describes **the two contract endpoints and nothing else**. It
uses production operation names; the words "test" and "mock" appear nowhere in it. The support
surface at `/api/mock-dmrp` is hand-written and deliberately absent from it (§1.1).

The server URL carries **no path component**:

```yaml
servers:
  - url: http://localhost:6159
```

So the spec's `/msc` resolves to `http://localhost:6159/msc`, and the operations sit at the
root. `DmrpController` therefore declares **no `[Route]` of its own** — routing comes entirely
from the generated base. Repointing a consumer at another instance, or eventually at real
DMRP, is a change to that one server URL and nothing else.

That is the point of putting the contract endpoints at the root and our own under `/api/mock-dmrp`:
the base URL is the only thing that has to change, because none of our paths would come with
it. `TheContractEndpointsSitAtTheRootWithNoPrefix` in `DmrpControllerTests` pins it.

### 2.2 What happens on build

```text
Contracts/dmrp-openapi.yaml
        │
        │  NSwag  (MSBuild target "GenerateDmrpContract", before CoreCompile)
        ▼
obj/NSwag/DmrpApi.Generated.cs
        │
        ├── abstract class DmrpControllerBase   routes, [ProducesResponseType], 2 abstract methods
        └── DTOs                                ReportingPlanResponse, ReportingPlanItem, …
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

```bash
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
2. **Nullability of optional string filters.** NSwag emits `string name`, not `string?`,
   and `[ApiController]` treats a non-nullable reference parameter as required — so filters the
   contract documents as optional return `400` until the override restates them as `string?`.

Trap 2 is live: `name`, `year` and `month` are all optional in the contract, so
`DmrpController` restates all three as nullable. Leave them as generated and every one becomes
mandatory, which is a `400` for requests the contract says are valid. Trap 1 currently has only
the cancellation token to demonstrate it, and is kept because a fuller contract with paging
would make it bite. Keep `[ApiController]`: it is what infers `Path` for route parameters
carrying only `[BindRequired]`.

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

Authenticated with the **third party's** bearer token, from `POST /api/mock-dmrp/oauth2/token`.

| Route | Purpose |
|---|---|
| `GET /msc?nhsnorgid=&name=&year=&month=` | Monthly medicine reporting plan (`MSC`) |
| `GET /ps/annual/mrp?nhsnorgid=&name=&year=&month=` | Annual patient-safety reporting plan (`PS`) |

Only `nhsnorgid` is required. `name`, `year` and `month` each narrow the result when supplied
and are ignored when not, so a caller passing only `nhsnorgid` gets the facility's whole plan
for that component. `year` and `month` are **strings** on the wire, not integers. See §1.3.

Both return `ReportingPlanResponse`. Both answer `200` with an empty `plans` array for a
facility enrolled in nothing (§4), and `401` without a valid token.

### Support surface — ours, not in the spec, no counterpart in real DMRP

Authenticated with **Link's** standard scheme (`IsLinkAdmin`).

| Route | Purpose |
|---|---|
| `POST /api/mock-dmrp/oauth2/token` | Issues the third-party token the contract surface accepts |
| `POST /api/mock-dmrp/entries` | Create an entry → `201` + `Location` |
| `GET /api/mock-dmrp/entries/{id}` | One entry. `400` on a non-GUID, `404` if absent |
| `PUT /api/mock-dmrp/entries/{id}` | Update → `202`. **Never creates**; `404` if absent |
| `GET /api/mock-dmrp/facilities/{facilityId}/entries` | A facility's entries across both components, paged. Empty page when it has none |
| `GET /api/mock-dmrp/entries/search` | Filtered search, paged. Empty page when none |
| `DELETE /api/mock-dmrp/entries/{id}` | `204`, or `404` if absent |
| `DELETE /api/mock-dmrp/facilities/{facilityId}/entries` | Idempotent `204` |
| `DELETE /api/mock-dmrp/entries` | Removes **every** entry. No confirmation step |
| `GET /api/mock-dmrp/delay` | The artificial delay currently in force |
| `PUT /api/mock-dmrp/delay` | Sets an artificial delay on the contract endpoints. See §4.3 |
| `DELETE /api/mock-dmrp/delay` | Removes it. Idempotent |

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

**A measure that does not appear in `plans` means the facility is NOT enrolled in it.**
There is no negative representation. `isReporting` is `"Y"` wherever an entry exists.

Seed only `HOB` for facility `100`, February 2020, then `GET /msc?nhsnorgid=100&year=2020&month=2`:

```json
{
  "psDMRptPlanID": 1,
  "orgid": 100,
  "year": 2020,
  "month": 2,
  "modifyDate": "2023-09-09 11:12:12.59",
  "createDate": "2023-09-09 11:12:12.59",
  "plans": [
    { "name": "HOB", "nhsnorgid": "100", "month": "2", "year": "2020", "reporting": "Y", "rptSeq": 0 }
  ]
}
```

The caller concludes "not enrolled in HTCDI" from its absence. `reporting` is only ever
`"Y"`, so it carries no information a caller can act on — presence is the whole signal.

A facility enrolled in nothing returns **`200` with `"plans": []`** — not `204`, not `404` —
because an empty plan is a meaningful answer rather than an absent resource.

**No read on either surface has an empty-result branch.** `GET
/api/mock-dmrp/facilities/{id}/entries` and `GET /api/mock-dmrp/entries/search` both answer
**`200` with an empty `records` array** and usable paging metadata, so a caller parses one
shape whichever route it came from. Not `404` on the by-facility route — zero entries means
the facility has no reporting plans, not that it does not exist, and `ReportingPlanEntry` is
the only table this service has to check against. A **blank** facility identifier is still a
`400`.

An entry stored with `isReporting` other than `"Y"` is excluded from a plan entirely.

An annual plan omits the root `month` from the response, rather than reporting a zero or a
stale value that would tell a consumer the plan covers one particular month.

### 4.1 ⚠️ The cadence rule, and why the service enforces it

A monthly component **requires** a reporting month; an annual one **must omit** it. The rule
is conditional on the component, so no column constraint or `[Range]` annotation can express
it — `ReportingPlanService` rejects violations with a `400` naming the cadence.

It has to be enforced rather than merely documented, because both failure modes are silent:

- A **PS entry saved with a month** satisfies the unique index perfectly well, and `/ps/annual/mrp`
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

### 4.3 Making the contract endpoints answer slowly

A caller's timeout and retry path only gets exercised against an upstream that is actually
slow, so the service can be told to hold contract requests:

```bash
curl -X PUT $B/api/mock-dmrp/delay -H 'Content-Type: application/json' -d '{"milliseconds":5000}'
curl $B/api/mock-dmrp/delay          # what is in force
curl -X DELETE $B/api/mock-dmrp/delay
```

**In memory, never persisted.** A restart always returns the service to answering immediately.
That is deliberate: the delay describes what a test is doing right now, not how the service is
configured, so a forgotten delay must not outlive the run that set it.

⚠️ **The delay reaches the contract endpoints only.** `/api`, `/health` and `/swagger` are never
delayed, and that scoping is load-bearing rather than tidy:

- If `/api/mock-dmrp` were delayed, turning a five-minute delay off would take five minutes, because
  the endpoint that clears it would be delayed too. The escape hatch has to stay fast.
- If `/health` were delayed, the container would miss its probe timeout and be restarted —
  which reads as an outage rather than a test in progress.

The rule is written as "everything except our own namespaced paths" rather than a list of
contract routes, so an endpoint added to the contract is delayed automatically. That inversion
is only safe because the contract endpoints sit at the root and everything of ours is prefixed
(§2.1).

Two other properties worth knowing:

- **Capped at five minutes.** Long enough to exceed any client timeout worth testing, short
  enough that a mistyped value cannot make the contract surface unusable until someone
  restarts the service.
- **The wait is cancellable.** A caller that times out and disconnects releases its request
  instead of holding it for the full delay, so concurrent callers against a long delay do not
  tie up requests nobody is waiting for any more.

`PUT` answers `200` rather than the `202` used elsewhere for updates: the change has already
taken effect by the time the response is written, and the body is the state now in force.

---

## 5. Seeding a scenario

With the service running (see §7):

```bash
B=http://localhost:6159

# Start clean
curl -X DELETE $B/api/mock-dmrp/entries

# Enrol facility 100 in HOB for February 2020 -- monthly, so a month is required
curl -X POST $B/api/mock-dmrp/entries -H 'Content-Type: application/json' \
  -d '{"facilityId":"100","component":"MSC","measure":"HOB","reportingMonth":2,"reportingYear":2020,"isReporting":"Y"}'

# ...and in HAI for 2020 -- annual, so a month must be omitted
curl -X POST $B/api/mock-dmrp/entries -H 'Content-Type: application/json' \
  -d '{"facilityId":"100","component":"PS","measure":"HAI","reportingYear":2020,"isReporting":"Y"}'

# Acquire a third-party token
TOKEN=$(curl -s -X POST $B/api/mock-dmrp/oauth2/token -H 'Content-Type: application/json' \
  -d '{"grant_type":"client_credentials","client_id":"link-cloud-dev","client_secret":"link-cloud-dev-secret","scope":"dmrp.read"}' \
  | jq -r .access_token)

# Query each plan -- each returns only its own component's measures
curl -s -H "Authorization: Bearer $TOKEN" "$B/msc?nhsnorgid=100&year=2020&month=2" | jq
curl -s -H "Authorization: Bearer $TOKEN" "$B/ps/annual/mrp?nhsnorgid=100&year=2020" | jq

# Everything but nhsnorgid is optional -- this returns the whole medicine plan
curl -s -H "Authorization: Bearer $TOKEN" "$B/msc?nhsnorgid=100" | jq
```

Against a deployed instance the `/api/mock-dmrp` calls also need a Link bearer token; locally
docker-compose sets `Authentication:EnableAnonymousAccess`, as it does for every Link service
(§6).

A second create with the same natural key is a `409`, so the store cannot hold two
contradictory rows for one facility, component, measure and period. The same measure name
under **both** components is two legitimate rows, not a duplicate — the plans are independent.

### 5.1 Key fields are trimmed

`facilityId`, `component`, `measure` and `isReporting` are trimmed before they are stored or
compared, on writes and on lookups alike.

That matters because the sanitizer keeps the space character. Without trimming, `" HOB"` and
`"HOB"` were two distinct rows in the natural key: both stored happily, and a plan seeded with
the padded one **silently omitted** the measure a consumer was looking for. No error anywhere,
just a short plan. Now the second create is a visible `409`, and a padded query still finds a
trimmed row.

A value that is empty once trimmed is rejected with a `400` rather than stored — otherwise
`"   "` would become an entry with no measure at all, which satisfies every other rule here.

### 5.2 Paging is rejected, not clamped

`pageSize` must be `1`–`100` and `pageNumber` at least `1`. Anything outside that is a `400`
from the request annotations.

Clamping was the friendlier behaviour and the wrong one for a stand-in: a caller asking for
5,000 rows and silently receiving 100 has no way to tell that happened, and a test written
against the clamped result would pass while proving nothing. The service still clamps
internally for callers that bypass HTTP, so a bad value cannot reach the repository as a
negative `Skip`.

---

## 6. Configuration

All under the `MockDmrpApi` section, or `MockDmrpApi__<Key>` as an environment variable.

| Setting | Type | Default | Notes |
|---|---|---|---|
| `Enabled` | bool | `false` | Master switch, and it fails closed. See §6.1 |
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
enforce. The `401` behaviour of every `/api/mock-dmrp` endpoint is covered by `MockControllerTests`
rather than by the local stack.

⚠️ **`SigningKey` must be the same value on every replica.** Tokens are validated by the same
service that issues them, so a per-instance key means a token minted by one pod is rejected by
another — an intermittent `401` that reads as a bug in the caller.

### 6.1 The availability switch

One switch, and it fails closed. `MockDmrpApi:Enabled` must be present and **true** for the
mock to serve anything; an absent key means disabled.

**Do not gate this on the environment name.** An earlier version refused unconditionally when
`IHostEnvironment.IsProduction()` was true. That does not work here: every deployed Link
namespace runs with `ASPNETCORE_ENVIRONMENT=Production` — dev, qa and test included — so the
check disabled the mock in every environment it is actually deployed to, while protecting
nothing the fail-closed default does not already protect (LEGLINK-1048).

What keeps the mock out of production is two things, neither of which depends on a name:

- **no `MockDmrpApi:Enabled` row is provisioned in a production App Configuration store** —
  absent is off, so there is nothing to get wrong
- **the image is not deployed to a production namespace at all** —
  `Scripts/build_and_push_and_set.py` targets a `mock-dmrp-deploy` deployment, which only the
  lower environments have

The lower environments each need the row set to `true`, and `appsettings.Development.json`
and `appsettings.Docker.json` carry it so a workstation and the local stack serve out of the
box.

When disabled:

- every route answers **`503`** with problem details carrying a `traceId`
- `/health` and `/api/mock-dmrp/info` keep answering, so the container stays healthy rather
  than looking like an outage
- **EF migration is skipped** — a dormant deployment has no business altering a schema
- a warning is logged naming the environment, the key that decided it, and the routes that remain

### 6.2 Error responses

RFC 9457 problem details, shaped the way Terminology shapes them —
`AddDmrpProblemDetails` supplies a `traceId` on every problem and a `detail` where the
framework would leave none, and each controller passes a `title` and `type` alongside its own
message.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Entry Not Found",
  "status": 404,
  "detail": "No reporting plan entry was found with id '1111…'.",
  "traceId": "00-6a12bf09f00084d0c4f708a11a990f81-…"
}
```

A `500` has its detail replaced wholesale rather than filtered, so an exception message cannot
reach a caller by accident. `ProblemDetails:IncludeExceptionDetails` adds an `API` extension
naming the service; it is otherwise off outside Development.

Two things worth knowing:

- **`POST /api/mock-dmrp/oauth2/token` is the one exception.** It answers with the OAuth 2.0 error shape
  (`{"error": "invalid_client", …}`) rather than problem details, because it stands in for an
  authorization server and client libraries parse those codes.
- **This applies to the contract endpoints too.** The real DMRP API has not been observed to
  define an error shape, so matching Link's house style is the better default — but it is a
  divergence, and a consumer should not read these error bodies as evidence of what the real
  service returns.

### 6.3 The two authentication systems

They are separate on purpose, mirroring the real topology.

| | Guards | Scheme |
|---|---|---|
| **Link's** | the support surface at `/api/mock-dmrp` | `AddLinkBearerServiceAuthentication` + `[Authorize(Policy = IsLinkAdmin)]`, exactly as Terminology, Census and Tenant do |
| **The third party's** | `GET /msc`, `GET /ps/annual/mrp` | HS512 JWT minted by `POST /api/mock-dmrp/oauth2/token`, validated by `AuthTokenService` |

`DmrpController` carries `[AllowAnonymous]` so Link's middleware never sees it, and checks the
third-party token itself. That is not a hole: those endpoints impersonate an external service,
and Link's credential has no meaning to it.

### 6.4 The token, and where it differs from real NHSN Auth

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
`/api/mock-dmrp`, which is not part of DMRP at all. `Contracts/dmrp-openapi.yaml` is the actual
contract, and it describes only the two endpoints in §3. **Read the yaml, not the Swagger
page**, when you need the contract; the Swagger page is useful for exercising the support
surface.

---

## 8. Open questions for Leidos

Everything below is invented. Getting a real request/response pair, or the published spec,
matters more than anything else in this project — a consumer written against these guesses
will work perfectly here and fail on first contact with the real endpoint.

- **Path shapes** — `/msc` and `/ps/annual/mrp` are what LCG recorded, but the ADR describes the
  two components as *separately deployed APIs* rather than two paths on one. See §1.5.
- **Component identifiers** — the strings `"MSC"` and `"PS"` are ours. Does the real API use
  these, longer names, or coded values?
- **`psDMRptPlanID` and `rptSeq`.** Their meaning is not documented. This service derives a
  stable identifier from the query so repeated identical requests agree, and always returns
  `rptSeq: 0`. Neither is likely to be what the real API does.
- **What comes back with no period.** Omitting `year` and `month` is valid, so the result can
  span several periods — but the response carries a single root `month`/`year`. This returns
  null for both in that case. Does the real API do something else?
- **Malformed `year` or `month`.** This returns `400`. The real API might ignore it, or return
  an empty plan. That guess is deliberately loud rather than silent.
- **Status codes.** Every non-200 in the contract is invented; the ADR documents only the
  success body.
- **Response shape parity** — the ADR gives one example, from one component. Do both
  deployments genuinely return the same schema?
- **Auth** — is NHSN Auth a separate service with its own base URL, and does it use RS256
  with a published JWKS?

---

## 9. Project structure

```text
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
│   ├── Middleware/                   DmrpAvailability, DmrpDisabledMiddleware,
│   │                                 ResponseDelayMiddleware
│   ├── Models/                       ReportingPlanSearchCriteria, MockEntryModels,
│   │                                 MockTokenModels, MockDelayModels
│   ├── Mapping/                      EntryMapper  (entity → generated DTO)
│   └── Services/                     ReportingPlanService, AuthTokenService,
│                                     ResponseDelayService
└── Presentation/Controllers/
    ├── DmrpController.cs             Contract surface — overrides the generated base
    └── MockController.cs             Support surface — entirely hand-written
```

Tests live in `DotNet/ServiceTests` under `UnitTests/MockDmrpApi` and
`IntegrationTests/MockDmrpApi`.

```bash
dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~MockDmrpApi
```
