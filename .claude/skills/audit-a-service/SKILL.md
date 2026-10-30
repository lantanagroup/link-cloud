---
name: audit-a-service
description: Make one Link Cloud .NET service's REST endpoints auditable - register the shared audit mechanism, mark each endpoint the REST API auditing ADR lists with [Auditable]/.Auditable, and have handlers fill in what the route cannot. Use whenever asked to audit a service's APIs, adopt or turn on auditing somewhere, apply the Auditable attribute, record who changed what through an endpoint, or work a LEGLINK audit adoption story - including when the ask names only the service ("make Submission auditable", "Notification's endpoints need audit events").
---

# Adopt REST auditing in one service

One service per pass, one story per PR. The shared mechanism already exists in
`DotNet/Shared/Application/Auditing`; this is about switching it on for a service's endpoints
without changing what any of them do.

**Read `dev-docs/auditing-an-endpoint.md` before writing code.** It is the reference for the
attribute, note templates, `IAuditContext` and masking, and it is kept current - this skill does not
repeat it. `dev-docs/auditing-known-limits.md` says what the mechanism does not do; read it when a
handler looks like it saves more than once.

## 1. Get the ADR rows first

The [REST API auditing ADR](https://keystone.lantanagroup.com/Proposals/Details/rest-api-auditing)
decides **which** endpoints are auditable and with **what** action and resource. It is the source of
truth, not the controller.

Fetch it with the Keystone MCP tools (`mcp__keystone__get_adr`, id `rest-api-auditing`), loading them
with ToolSearch if they are not already available. If Keystone is unreachable, ask for the service's
rows rather than guessing - an invented resource name produces audit rows nobody can query.

Build the service's table before touching code, and show it. One row per endpoint:

| Endpoint | Action | Resource | ResourceId comes from | FacilityId comes from | Note |
| --- | --- | --- | --- | --- | --- |

An endpoint the ADR does not list is out of scope, however auditable it looks. GETs are not audited
unless the ADR gives them a `Query` row.

If the service has no rows at all, stop there and say so. Change nothing - not the controller, and
not the registration either, however harmless adding it "ready for later" looks: a service that
publishes no audit events needs no audit wiring, and the dead registration reads as adoption to the
next person. Submission is the worked example - one endpoint, `GET api/Submission/{facilityId}/{reportId}`,
and no section in the ADR. If the endpoints look like they ought to be audited, that is a question
for the ADR's owner, not a reason to mark them.

### When a row matches no endpoint

Some rows were written against an API that has since moved on, so the table is where you find out.
A row whose method and route match nothing, or whose note names placeholders the nearest endpoint has
no values for, is not an invitation to pick the closest endpoint - Census's
`POST api/census/patient-encounters` ("Restore", note naming `{id}`, `{eventtype}`, `{createDate}`)
has only `POST api/census/patient-encounters/rebuild` anywhere near it, which takes a facility and a
correlation id and restores nothing. Marking that endpoint would publish `Restore` events for a view
rebuild and leave the note half empty.

Leave it unmarked, do the rows that do match, and list the orphans for the ADR's owner - with the
routes the service actually has, so the row can be corrected rather than just queried.

### When the service already emits events for those endpoints

The ADR words some rows as updates to an existing event ("Remove Notes. Remove CorrelationId.") -
Tenant's Facility and Vendor controllers, QueryDispatch's configuration controller. That is a
replacement, not an annotation: find the existing sender first (`grep` the service for
`AuditEventMessage`, `CreateAuditEventCommand`, `AuditableEventOccurred`) and delete it in the same
change, or every request publishes twice.

Reconcile what differs before deleting anything, and say so in the PR. These three catch people out,
because a hand-rolled sender was written to one service's taste and the shared rules are not
negotiable:

- **Secrets become `[redacted]`.** A sender that deliberately recorded a key or secret id stops
  doing so, and a test asserting the real value is asserting the old policy.
- **A write that changes nothing still publishes.** The filter publishes on any successful response;
  a hand-rolled sender often returns early when no property changed.
- **The Kafka key is `FacilityId`, empty when there is none.** A resource that is not facility-scoped
  (Vendor, VendorVersion) loses whatever key the old sender chose.

Existing tests are the tell. If they drive the **manager** rather than the endpoint, they see nothing
once auditing moves to the filter, and they have to be rewritten against HTTP - which needs the
service's integration suite (Docker) to verify. Plan for that before starting, rather than
discovering it with the old sender already deleted.

## 2. Register the mechanism

`AddLinkAuditing<TContext>()` goes in `Program.cs` with the context the audited endpoints write
through. The property-change interceptor is attached separately, and **how** depends on the way the
service builds its `DbContext`. Work out which of these the service is before adding a line:

| The service builds its context with | Do this | Example |
| --- | --- | --- |
| the shared `AddSQLServerEF<T>()` helper | nothing - the helper attaches it | Tenant, QueryDispatch |
| its own `AddDbContext` | add `.AddLinkAuditInterceptor(sp)` to the options chain | Census, Normalization, Report, DataAcquisition |
| its own `AddDbContext`, already listing interceptors positionally | add `sp.GetRequiredService<AuditPropertyChangeInterceptor>()` to `AddInterceptors(...)` | Account |
| no `DbContext` at all | call the non-generic `AddLinkAuditing()`; events carry no property changes | Terminology |

The service must already register a `KafkaConnection` - auditing publishes to Kafka, and a service
without one fails to start, with a message naming the line to add.

## 3. Mark the endpoints

```csharp
[HttpPut("{facilityId}")]
[Auditable(AuditEventType.Update, "CensusConfig")]
```

```csharp
userEndpoints.MapDelete("/user/{id}", DeleteExistingUser.Handle)
             .Auditable(AuditEventType.SoftDelete, "LinkUser");
```

Give a note only where the ADR's row specifies one, and set `RecordIdRouteKey` / `FacilityIdRouteKey`
only where the route names them something else. The guide has the full set of options.

### The trap: ResourceId is the row, never the facility

`ResourceId` must uniquely identify the row the request acted on. A route carrying `{facilityId}`
fills `FacilityId` on its own - that is **not** the record's id unless the record *is* the facility
(Tenant's own endpoints). For a child record on a facility route the route carries no record id at
all, so the handler sets `RecordId`. Get this wrong and every audit row points at the facility, which
no reader can tell apart from a correct one.

A bulk endpoint that acts on many rows leaves `ResourceId` null and says what it did in its note.
Never fall back to the facility just to have something to put there.

## 4. When the handler has to help

The repo's controllers reach the context through a property rather than the constructor:

```csharp
/// <summary>
/// The request's audit context. Null when the controller is constructed directly, outside a request.
/// </summary>
private IAuditContext? Audit => HttpContext?.RequestServices?.GetService<IAuditContext>();
```

```csharp
if (Audit is { } audit)
{
    audit.RecordId = created.Id.ToString();
    audit.FacilityId = model.FacilityId;   //only when the route carries no facility
}
```

This is house style for a reason: several of these controllers are `new`-ed directly in tests with
positional arguments and `null!` padding (Normalization, Report, DMRP), where `HttpContext` is null.
A constructor parameter would mean editing every one of those call sites, so keep the adopted
controllers consistent rather than making one service the exception.

Minimal-API handlers take `[FromServices] IAuditContext audit` instead - there is no controller to
construct, so the guide's injected form applies there.

Set `ActionOverride` on a create-or-update endpoint, `Set(name, value)` for each `{placeholder}` a
note names, and read a row's values *before* deleting it when the note names them.

Two things about notes that only show up once you try to fill one:

- **The values a note names may not exist yet.** A summary note wants counts the manager computed and
  threw away; a delete note wants a row an `ExecuteDelete` never loads. Giving the manager a result
  type to return, or the queries a `GetById`, is part of this work rather than scope creep - the ADR
  asked for the note, and the note cannot be filled without it. Keep the addition to what the note
  needs.
- **A placeholder filled from the route carries the route value as it arrived.** The factory
  sanitises a facility read from the query string, but not route values, and handler values take
  precedence over both. So when the handler sanitises or normalises something the note names, `Set`
  it: otherwise the note records what the caller sent rather than what was acted on.

## 5. Mask what the entities carry

Property changes ride on `Update` events only, and only the shared secret lists are automatic.
Personal data is not: sweep the entities those endpoints write and put `[AuditMask]` on names, email
addresses and anything else identifying, **in the same change** that makes the endpoints auditable.
An Update endpoint whose entity holds an email publishes it otherwise.

`[AuditMask]` is an attribute, not a column - it needs no EF migration, and nothing in this work
should.

## 6. Keep the test hosts starting

A hand-built test host (a `WebApplicationFactory` subclass with its own registrations) that serves an
audited endpoint needs `AddLinkAuditing` too, usually with a fake `IAuditPublisher`, plus
`AddMetrics()` and a `ServiceInformation` whose `ServiceConfigName` is set. Without the registration
every request to the endpoint fails with an error naming the missing call.

## 7. Verify

```powershell
dotnet build DotNet/<Service>/<Service>.csproj
dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "Category=UnitTests"
```

The unit suite is the floor - it is what catches a test host that no longer starts. The integration
suites need Docker (Testcontainers); run them when Docker is up, and say plainly when it was not.

For a service's first adopted endpoint a live check is worth it: bring up the compose stack, call the
endpoint, and read the event off `AuditableEventOccurred`. Three things only a live run shows - the
caller's email in `User`, `ResourceId` pointing at the row rather than the facility, and masked values
reading `[redacted]` on both sides.

## 8. Stay inside the story

This work records what endpoints do; it does not change it. Do not fix an endpoint's status codes,
routes, validation or `204`-on-empty while here, even where `AGENTS.md` says they are wrong - raise
those separately. Transactions and retry loops stay the handler's business.

Java services (Validation, MeasureEval) are out of scope entirely: their ADR rows need a producer of
their own.

The PR takes the service's own story key, `LEGLINK-XXXX: `, and the description template in
`AGENTS.md`. Say which endpoints were marked and which ADR rows they came from.
