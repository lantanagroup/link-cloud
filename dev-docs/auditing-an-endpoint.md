# Auditing an endpoint

Every REST endpoint that changes configuration records who changed what. This is how.

The mechanism lives in `DotNet/Shared/Application/Auditing`. You mark an endpoint, and the
mechanism runs it, captures what changed in the database, builds the event once the action has
succeeded and publishes it to the
`AuditableEventOccurred` topic, where the Audit service stores it. Which endpoints are auditable, with
what action and resource, is decided by the
[REST API auditing ADR](https://keystone.lantanagroup.com/Proposals/Details/rest-api-auditing) - look
your service up in its table rather than inventing values.

It is .NET only. The Java services' ADR rows - Validation and MeasureEval - need their own producer,
which must publish the same `AuditEventMessage` to the same topic; see
[known limits](auditing-known-limits.md).

## Register the mechanism once per service

A service whose writes go through Entity Framework:

```csharp
builder.Services.AddLinkAuditing<TenantDbContext>();
```

The interceptor that captures property changes is registered by that call but **attached by you**,
inside your own `AddDbContext`, alongside whichever `UpdateBaseEntityInterceptor` your service uses.
There is no single place to do this for you: Account, Audit and Notification each keep their own copy
of `UpdateBaseEntityInterceptor`, and Report and Submission register the shared one without ever
attaching it, although both persist through Entity Framework.

```csharp
builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(sp.GetRequiredService<UpdateBaseEntityInterceptor>())
           .AddLinkAuditInterceptor(sp));
```

`AddLinkAuditInterceptor(sp)` hands the registered interceptor to Entity Framework, which calls only
the interceptors given to the options: registered but not attached, it never runs, and every event
is published with no property changes and no error. It does nothing when `AddLinkAuditing` was not
called, so it is safe in setup code shared with a process that does not audit - Data Acquisition's
worker runs the same `RegisterEntityFramework` as its API, and only the API registers auditing.

The exception is a service that builds its context with the shared `builder.AddSQLServerEF<T>()`
helper, as QueryDispatch does: it has no `AddDbContext` of its own, so the helper calls
`AddLinkAuditInterceptor` itself. Calling `AddLinkAuditing<T>()` is enough.

A service that persists nothing through Entity Framework, such as Terminology, uses the overload with
no context. No property changes are captured:

```csharp
builder.Services.AddLinkAuditing();
```

Either way the service must already register a `KafkaConnection`; auditing publishes to Kafka. Most
services do. If yours does not, the service fails to start, with a message telling you the line to
add - not on the first audited request in production. A broker that is merely down does not stop it
starting; only a missing configuration does.

A test host that builds the service by hand - a `WebApplicationFactory` subclass with its own service
registrations - needs `AddLinkAuditing` too, usually with a fake `IAuditPublisher` so no Kafka is
needed. Without it every request to an auditable endpoint fails, with an error naming that line.
Faking the publisher does not take the counter out of the graph: the recorder resolves
`IAuditMetrics` itself, to account for an event it loses. So such a host also needs `AddMetrics()` and
a `ServiceInformation` whose `ServiceConfigName` is set, which `SetupServiceInformation` does, or a
fake `IAuditMetrics` of its own.

## Mark the endpoint

On a controller action:

```csharp
[HttpPut("{facilityId}")]
[Auditable(AuditEventType.Update, "Facility")]
public async Task<IActionResult> UpdateFacility(string facilityId, ...)
```

On a minimal-API endpoint:

```csharp
userEndpoints.MapDelete("/user/{id}", DeleteExistingUser.Handle)
             .Auditable(AuditEventType.SoftDelete, "LinkUser");
```

Both publish exactly one event when the response is a success, and nothing at all when it is an error
or the handler throws. A note is only given when the ADR's row for that endpoint specifies one, to
keep the audit rows small:

```csharp
[Auditable(AuditEventType.Delete, "HSLOC", "All HSLOC codes for version {version} deleted")]
```

Placeholders written as `{name}` are filled from the handler's context values first - what it passed
to `Set`, [below](#when-the-handler-has-to-help) - and then from the route. A placeholder whose name
neither the handler nor the route supplies is left visible in the note, so a missing value shows up
in the audit trail instead of quietly reading as prose. A value the handler sets to `null` fills it
with nothing. For `DELETE api/VendorVersion/42`, marked with the note
`"Id {id} deleted (Vendor: {Name}, Version: {Version})"`:

| The handler | The note reads |
| --- | --- |
| sets `Name` to `Epic` and `Version` to `2024` | `Id 42 deleted (Vendor: Epic, Version: 2024)` - `{id}` from the route |
| sets `Name` only | `Id 42 deleted (Vendor: Epic, Version: {Version})` - the missing value is visible |
| sets `Name`, and `Version` to `null` | `Id 42 deleted (Vendor: Epic, Version: )` - an empty value, not a missing one |

Because the handler's values come first, a handler can also replace a route value of the same name.

If the record's id is not in a route value called `id`, say which one it is:

```csharp
[Auditable(AuditEventType.SoftDelete, "Facility", RecordIdRouteKey = "facilityId")]
```

The event's `ResourceId` must uniquely identify the row the request acted on, so name the row's own
id here - `facilityId` above because a facility is that row - never the facility a child record
belongs to. When the route does not carry the row's id at all, the handler sets `RecordId`, below.

The facility is taken from a route value called `facilityId`, or, when the route has none, from a
query string parameter of that name - Normalization's `OperationSequence` endpoints take
`?facilityId=`. A query value is sanitised first, and a parameter given twice is not guessed at. If
the route calls the facility something else, say what:

```csharp
[Auditable(AuditEventType.Update, "SftpConfiguration", FacilityIdRouteKey = "organizationId")]
```

Leave it out and the event's `FacilityId` is null, logged only at Debug: in production a route that
names the facility differently fails silently.

The two are looked up separately, so one route value can fill both, one, or neither:

| Endpoint | `ResourceId` | `FacilityId` | What it needs |
| --- | --- | --- | --- |
| `DELETE api/Facility/softDelete/{facilityId}` | the facility | the facility | `RecordIdRouteKey = "facilityId"` - the record *is* the facility |
| `PUT api/data-acquisition/{facilityId}/QueryPlan` | the query plan | the facility | nothing on the attribute; the handler sets `RecordId`, since the route carries only the facility |
| `DELETE api/Normalization/OperationSequence?facilityId=` | none - a bulk delete | from the query string | nothing |
| `PUT api/data-acquisition/{organizationId}/sftp-configurations/{configurationId}` | the SFTP configuration | the facility | `RecordIdRouteKey = "configurationId"`, `FacilityIdRouteKey = "organizationId"` |

Only an Update event carries property changes. When one request changes too many rows for them to
be useful - Normalization's HSLOC upload moves every code to a new version, one identical entry per
code - leave them off and say what changed in the note:

```csharp
[Auditable(AuditEventType.Update, "HSLOC",
    "HSLOC updated from version {oldVersion} to {newVersion}: {updated} updated, {added} added, {deactivated} deactivated",
    SuppressPropertyChanges = true)]
```

The handler fills those values with `Set`, below: the versions arrive in the form and the counts are
only known once the upload is processed.

The ADR recommends property changes on Update actions, so use it only where the entries would not
tell a reader anything - a bulk Update, not an endpoint whose changes are merely inconvenient - and
let the note say what changed. It is the endpoint's setting rather than the handler's because the
handler never sees the changes: they reach the event once the action has succeeded, after the handler
has returned.

The minimal-API form takes the same settings as arguments:
`.Auditable(action, resource, recordIdRouteKey: "userId", facilityIdRouteKey: "organizationId",
suppressPropertyChanges: true)`. A facility that arrives in the request body is the handler's to
set, below.

## When the handler has to help

The attribute cannot know anything that is not in the route or the endpoint's signature. Inject
`IAuditContext` and tell it:

```csharp
public FacilityController(IAuditContext audit, ...) => _audit = audit;
```

| Set this | When |
| --- | --- |
| `FacilityId` | the facility arrived in the request body rather than the route |
| `PatientId` | the request concerns a patient |
| `RecordId` | the route does not carry the row's own id - always on a create, whose new id only exists after the save, and on any endpoint whose id arrives in the body |
| `ActionOverride` | the endpoint creates on one branch and updates on another, and you know which happened |
| `SuppressNote` | the attribute's note only applies to one branch of the handler |
| `Set(name, value)` | a note template refers to `{name}` |

```csharp
//a create-or-update endpoint, saying which it did
_audit.ActionOverride = existing is null ? AuditEventType.Create : AuditEventType.Update;

//values for the note "Id {id} deleted (Vendor: {Name}, Version: {Version})"
_audit.Set("Name", version.VendorName);
_audit.Set("Version", version.Version);
```

A minimal-API handler takes it as a parameter, `[FromServices] IAuditContext audit`.

A note that names the deleted record needs the record's values, so read them before the delete. This
matters most when the delete is an `ExecuteDeleteAsync`, which never loads the row: without the read,
nothing can fill the note.

```csharp
var existing = await _queries.GetAsync(id, cancellationToken);
if (existing is not null)
{
    _audit.Set("Measure", existing.Measure);
    _audit.Set("dqm", existing.DQM);
}

await _manager.DeleteAsync(id, cancellationToken);
```

The context is per request and shared with the interceptor, so writing to it anywhere in the handler
is enough; order does not matter.

What the handler does to the database is its own business - transactions, retry loops and all. Marking
an endpoint auditable changes none of it.

What that leaves with the handler is atomicity. A handler that saves more than once, or saves and then
returns a non-2xx result, can leave a partial change that the event describes as if it were whole, so
if the writes belong together, open a transaction and finish it before returning. The shapes to watch
for are in [known limits](auditing-known-limits.md).

## What the audit trail masks

Property changes are filtered before the event is built. Three things mask a value:

1. **The shared name list** - `Password`, `Secret`, `ClientSecret`, `Token`, `AccessToken`,
   `RefreshToken`, `ApiKey`, `PrivateKey`, `Passphrase`, `ConnectionString`, `Authentication`,
   `Credentials`. Matched whole, ignoring case.
2. **The shared patterns** - `*Secret*`, `*Password*`, `*Token*`, `*Key`.
3. **`[AuditMask]` on the property** - how personal data is masked. A name or an email address
   is not a secret, so no pattern catches it:

   ```csharp
   [AuditMask] public string ContactEmail { get; set; }
   ```

   Nothing shared catches personal data, so mark it with `[AuditMask]` in the same change that makes
   its endpoints auditable: an Update endpoint whose entity holds a name or an email address otherwise
   publishes them as they are.

A value one of them catches is **masked, not omitted**. The change still appears in the event, under the
property's name, with `[redacted]` as both its old and its new value, so a reader of the audit trail
can see that it changed and when, but never from what or to what. The mask is the same on both
sides, so it does not say whether the value was set, changed or cleared either. A value assigned
again unchanged is not reported, and neither is a JSON column replaced with an equal one: the stored
forms are compared in memory to find out, and neither is kept.

A service adds its own entries at registration, and **cannot remove a shared one** - `Remove` and
`Clear` throw. The ADR's three Data Acquisition rows on credentials need no line of the service's
own. The ADR's notes say to omit these values; the mechanism masks them instead, which tells a reader
that the credentials changed without saying what they changed to.

- **The query config PUT** and **the AuthenticationConfig PUT**. Data Acquisition stores an
  `AuthenticationConfiguration` as a JSON column named `Authentication` on `FhirQueryConfiguration`
  and `FhirListConfiguration`. `Authentication` is in the shared name list, and both endpoints write
  that column. It is one value, so the mask says the authentication settings changed, not which
  field.
- **The SFTP credentials PUT**. The credentials are kept in the secret manager, not through Entity
  Framework, so the interceptor never sees them and the event carries no property changes.

When a service does need its own entry, it adds it at registration:

```csharp
builder.Services.AddLinkAuditing<MyServiceDbContext>(o =>
{
    o.MaskedPropertyNames.Add("SessionCookie");       //a sensitive name nothing shared catches
});
```

Mask only what must not leave the service. A query plan's `InitialQueries` and
`SupplementalQueries` are large, but they are what a query plan update changes, and the ADR asks for
its property changes.

Each service logs what it masks once at startup, at Information, so the answer to "was that supposed
to be masked?" is in the service's own log. This is the line Account will write once its personal
data is marked `[AuditMask]`; until then it reads `attributes=[]`:

```
Audit masking for Account: names=[Password, Secret, ...] patterns=[*Secret*, *Password*, *Token*, *Key] attributes=[LinkUser.Email, LinkUser.FirstName, ...] skipped timestamps=[ModifyDate, ModifiedDate, ...]
```

`attributes` lists every mapped property marked `[AuditMask]`, as `Entity.Property`, read from the
model of the context passed to `AddLinkAuditing<TContext>()`; a service registered without a context
has none. If the model cannot be built, it reads `attributes=[unreadable]` with a warning, rather than
an empty list that would look like nothing is marked.

Modify timestamps are skipped as well, from a separate list: `ModifyDate`, `ModifiedDate`, `ModifiedOn`,
`LastModifiedOn`, `UpdatedAt`, `LastUpdatedDate` and `LastUpdated`. A service's
`UpdateBaseEntityInterceptor` writes one on every save, so without this every Update event would
carry it, and a save that changed nothing would look like an edit. They are noise rather than
secrets, which is why they are skipped rather than masked. If your entity's timestamp has another
name, add it the same way: `o.TimestampPropertyNames.Add("StampedAt")`.

## Known limits

What the mechanism does not do, and the rules that follow from it, are in
[Auditing: known limits](auditing-known-limits.md). Read it before converting a service: it covers what
the mechanism leaves to the handler, untracked writes, which actions carry property changes, JSON
columns, bulk endpoints, `ResourceId`, and when an event can be lost or arrive twice.

## Checking it works

- **The event** - `GET api/audit` in the Audit service, filtered by resource and resourceId.
- **The actor** - `User` is the caller's email claim. A token with no email claim records `null`; no
  placeholder is invented. Note that `CreateSystemToken` currently mints the configured Link admin
  address on every service-to-service call, so those calls record that address until LEGLINK-1359
  decides otherwise.
- **The record** - confirm `ResourceId` on the stored event is the id of the row the request acted
  on, and null only on a bulk endpoint. Turn on Debug for `LantanaGroup.Link.Shared.Application.Auditing`
  while testing: a missing `ResourceId` or `FacilityId` is logged there, naming the keys it looked for.
- **The facility** - confirm `FacilityId` on the stored event is set, on every endpoint whose route or
  query carries one.
- **The property changes** - on an Update, confirm each changed property is there with its old and
  new value; on every other action, and on an endpoint with `SuppressPropertyChanges`, confirm there
  are none. An Update with none at all usually means an untracked write (`AsNoTracking`,
  `ExecuteUpdateAsync`) - see [known limits](auditing-known-limits.md).
- **The masking** - read the raw Kafka message, not the stored row, and confirm the property is
  there with `[redacted]` on both sides and its value appears nowhere in the message.
- **The failure path** - stop Kafka and make the request. It still succeeds, after at most the
  publish response timeout (3 seconds by default), and the log says delivery continues in the
  background. If Kafka comes back within the producer's message timeout (librdkafka's default is
  5 minutes) the event arrives late and nothing is counted. Only if it stays down past that does
  `audit_publish_failed` increment, so a QA check of the counter has to wait that long.
