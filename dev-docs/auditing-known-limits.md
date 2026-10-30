# Auditing: known limits

What the shared audit mechanism does not do, and the rules that follow from it. Read this alongside
[Auditing an endpoint](auditing-an-endpoint.md) before converting a service - several of these change
how a handler has to be written.

**The mechanism undoes nothing.** It watches an action and describes it; it does not change what the
action does to the database. A handler that fails after saving keeps its save, and gets no event,
because the action did not succeed. Atomicity is the handler's, exactly as it is outside an audited
request: if several writes belong together, open a transaction and finish it before returning.

Two shapes to look for when adding `[Auditable]` to an endpoint, because both can leave a partial
change that no event describes:

- a loop that saves per item - read once, write once instead;
- a save followed by work that can fail. The ordering this codebase uses is to do failable external
  work *before* the save: Tenant's vendor deletes check Normalization over HTTP first, and Tenant's
  facility PUT calls DMRP before the host save.

**Work outside the database is not undone either.** A Quartz job deleted, a secret written to the
secret manager or a message produced to Kafka before the handler fails stays done - Quartz keeps its
own store and cannot enlist in a database transaction at all. Nothing about auditing changes that.

**A change counts only once the database has kept it.** The interceptor holds what a save would
change until the save succeeds, or, inside a handler's transaction, until that transaction commits. A
failed save, a rollback, or a transaction disposed without a commit contributes nothing, so a retried
unit of work is described once, not once per attempt. One consequence: because the event is built
once the action has returned and its result has executed, a transaction the handler still
holds open at that point has not committed, so the event carries no property changes from saves
made inside it - and if it later rolls back, the audit trail describes a change that never
happened. A handler must finish its transaction before it returns.

**Only audited requests are captured.** Entity Framework runs the interceptor on every save of the
context it is attached to, Kafka listeners and Quartz jobs included, but it captures only once the
filter has marked the request as audited. A save made anywhere else costs one check and records
nothing.

**Only tracked changes are seen.** The interceptor reads the change tracker, so an `AsNoTracking`
load, an `Update()` on a detached instance, and `ExecuteUpdateAsync`/`ExecuteDeleteAsync` all produce
no property changes - the event is still published, it just describes nothing. If an endpoint uses
`ExecuteDeleteAsync` and the note needs the record's values, load the row first.

**Property changes describe updates only.** The ADR recommends them on Update actions and says
nothing of the others. The mechanism leaves them off the others, for the reason the ADR gives for
leaving out notes - the size of each record - so only an event whose action is `Update` carries
them: the action it reports, after any `ActionOverride`. The interceptor captures modified entities
and nothing else, so a Create event has no new values to carry and a Delete no old ones. A soft
delete or a restore *is* saved as a modification, but its event carries no `IsDeleted` entries
either: the action already says what happened, and on an endpoint that soft-deletes every schedule
a facility has they would number one per row. A PUT that changed nothing still publishes an Update
event with no property changes at all - the request succeeded, so it is recorded. A note is the
place for a value any other action must keep.

**A service registered without a context captures nothing.** `AddLinkAuditing()` with no context
attaches no interceptor, so every event it publishes has no property
changes - Terminology's two Update rows can never carry the property changes the ADR recommends. A
note, such as the number of codes replaced, is the useful fallback for a row served this way.

**JSON columns are one value.** A column mapped as JSON, a collection stored as one column through a
value converter (QueryDispatch's `DispatchSchedules`), and a collection mapped with no converter at
all, which Entity Framework stores as a JSON column of its own (Data Acquisition's
`FhirQuery.QueryParameters`), all show up as a single blob-to-blob change in the stored form - the
JSON - not field by field. A value replaced with an equal one, such as a new list holding the same
items, is not reported. Everything else is described so that the same value always reads the same
whatever the host's culture: dates and times in ISO-8601, decimals and floating point with a point,
binary as base64. Normalization's `OperationJson` is a good
example of when that is enough: a whole operation definition is a few hundred characters, and the
before and after show exactly which setting moved. Where a blob is large enough to swamp the event,
mask it by name with `o.MaskedPropertyNames.Add(...)`, and decide that per service. A masked JSON
column is masked whole: the event says the column changed, never which field inside it -
Data Acquisition's `Authentication` reads the same whether the client id or the secret moved.

**Property changes say what changed, not which row.** Each entry is a property name with its old
and new value; no entity or row is attached, and there is no limit on how many one event carries. An
Update endpoint that changes many rows - Normalization's HSLOC upload, which moves every code to the
new version - records one identical entry per row, which a reader cannot tell apart, and enough of
them can pass Kafka's message size limit (1 MB by default), at which point every attempt fails and
the event is abandoned. Check how many rows a bulk endpoint can touch before marking it, and where
the entries would tell a reader nothing, set `SuppressPropertyChanges` and let a note give the
counts instead.

**A transaction lasts as long as the handler holds it.** Anything slow done while one is open - a
call to another service, Quartz work - holds the connection and any row locks already taken for that
long. Keep calls that need not be part of the write before the first save, or out of the endpoint.
Tenant's facility PUT is the shape to copy: DMRP is called before the host save, and the Quartz
update after it.

**`ResourceId` uniquely identifies the row the request acted on.** It is taken from the handler's
`IAuditContext.RecordId` first, then the route key the attribute names - and nothing else. So a route
key must name the row's own id, never the facility it belongs to, and a handler whose route does not
carry that id sets `RecordId` itself. Nothing guesses it from what was saved: the first row a save
touches may be a child (a location condition rather than its configuration) or one of several, and a
wrong id looks exactly like a right one, where a missing one is visible.

A bulk endpoint ("delete every plan for this facility") has no single record, and the field is null by
design. But an endpoint that *does* act on one record and names the wrong route key also gets null,
silently in production. The factory logs that at Debug, so turn Debug on for
`LantanaGroup.Link.Shared.Application.Auditing` while testing a new endpoint. It logs a missing
`FacilityId` the same way, for the same reason: many endpoints concern no facility, but a route that
names the facility differently, without `FacilityIdRouteKey` saying so, loses it just as quietly.

**`ServiceName` is the host's.** It comes from `ServiceInformation.ServiceConfigName`, so DMRP events
read `Tenant` - DMRP runs inside Tenant's process.

**A minimal-API handler returning a plain value is recorded early.** Both filters publish only once
the handler's result has executed, so a response that fails to serialise leaves behind no event
claiming success. The minimal-API filter does that by returning a result that executes the handler's
own, which covers every `IResult` - everything `Results` and `TypedResults` produce. A handler that
returns a plain value, or nothing at all, has its response built by the request delegate instead, in
a way the filter cannot reproduce, so those are still recorded on the way out. No audited endpoint
returns either today; return an `IResult`.

**An event can still be lost.** The filter publishes only after the action has succeeded, its changes
are committed and its result has executed, so a broker outage cannot roll back a change the user was
already told had succeeded, and no event describes a response that failed to serialise. The
cost is that a publish failing after that point loses the event. The request waits at most
`AuditOptions.Publish.ResponseTimeout` for the broker; past that it returns and delivery continues in
the background, and an event the producer finally gives up on is counted on the
`audit_publish_failed` counter and written to the log in full at Error. In full means the whole
event, the caller's email and any `PatientId` included, so a log sink less protected than the audit
store ends up holding that for every event that never reached the store. The transactional outbox
feature removes this by writing the event to the database the request already wrote to and shipping
it from there, at which point audit events become never-lost.

**An event can also arrive twice.** A produce that comes back `PossiblyPersisted` is retried on
purpose: the broker may already hold the event, but not retrying could lose it with nothing counted,
and a duplicate row does less harm than a missing one. The event carries no id of its own and the
Audit service stores whatever arrives, so a duplicate is indistinguishable from two identical
requests made in the same second. Read two matching rows with that in mind.

**Java services are not covered.** The mechanism is .NET: Validation's and MeasureEval's ADR rows
need a producer of their own, publishing the same `AuditEventMessage` to `AuditableEventOccurred`.
Nothing on the topic enforces this mechanism's rules, so that producer must follow them itself, or the
Audit service stores Java events that read differently from the .NET ones:

- `Action` in the format the ADR's open wire-format question settles. Today that is a number; a Java
  serializer's default, the name, is dead-lettered.
- Secrets and personal data masked as `[redacted]` on both sides.
- `ResourceId` the id of the row acted on, never the facility.
- Property changes on Updates only.
