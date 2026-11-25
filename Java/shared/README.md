# Overview

The shared module contains shared functionality across all java services
It is published to a separate private maven repository so that builds of service modules can find the shared module in
the private repository

# Testing Changes

To test changes to the shared library before deploying them to the private maven repo, run `mvn clean install` on the
shared
library, first. Then re-run `mvn clean install` on the dependent modules, and they will pick up the changes from the
local
.m2 repository where the shared library was installed.

# Auditing

`com.lantanagroup.link.shared.audit` lets a controller method emit the `AuditableEventOccurred` event the .NET
services send. A marked method that returns 2xx publishes one event after the response is complete; any other status
publishes nothing, and so does an exception nothing handles. The status is what decides: an exception that an
`@ExceptionHandler` turns into a 2xx response is a successful call, and publishes. A failed publish never fails
the request.

**Adopting it.** The shared jar registers `AuditAutoConfiguration` itself. A service sets one property in its
`application.yml`, or the application fails to start once any method is marked:

```yaml
link:
  audit:
    service-name: Validation
```

Record the key in `/app-config.yaml` with `required: false` and the service's value as `defaultValue`.

**Marking an endpoint and telling the context what the route cannot.**

The controller takes `AuditContext` by constructor injection, as any other bean:

```java
private final AuditContext audit;

@PutMapping("/{id}")
@Auditable(action = AuditAction.UPDATE, resource = "Category")
public void saveCategory(@PathVariable String id, @RequestBody Category category) {
    // The handler says only which of the two happened; what an update changed is captured from the save.
    if (!categoryRepository.existsById(id)) {
        audit.overrideAction(AuditAction.CREATE);
    }
    categoryRepository.save(category);
}

@DeleteMapping("/{type}/{name}")
@Auditable(action = AuditAction.DELETE, resource = "Artifact", note = "Artifact (Type: {type}, Name: {name}) deleted")
public void delete(@PathVariable String type, @PathVariable String name) {
    Artifact artifact = artifactRepository.findByTypeAndName(type, name).orElseThrow();
    // Neither path variable is the row's own id on its own, so the handler says which id it acted on.
    audit.setRecordId(artifact.getId());
    artifactRepository.delete(artifact);
}
```

- Values passed to `set(name, value)` fill the note as given and are not masked. They must be safe to store in the
  audit trail. Masking (`@AuditMask`, the shared name list and patterns, `AuditMaskingCustomizer`) covers property
  changes only.

**Property changes.** An Update event carries what changed as old and new value pairs, unless the endpoint sets
`propertyChanges = false`.

Put `@AuditMask` on a field or on its getter. On a field it masks the property of the same name, whatever the case. A
marked field whose getter has another name (field `socialSecurityNumber`, getter `getSsn()`) cannot be matched to
that getter, so every property of the class that has no field of its own name is masked, and a WARN names them, until
the annotation is moved to the getter.

*A service on JPA gets them without asking.* Where the service has a Hibernate persistence unit, the mechanism
attaches a listener to it (`HibernateAuditChangeListener`, the counterpart of the .NET
`AuditPropertyChangeInterceptor`) and reads the changes from the saves an audited request makes. Each change names
its `Entity` and `RowKey`, so a request that updates several rows says which row each change belongs to. A change is
reported only once its transaction has committed, and only for a request whose handler is marked; a save made by a
Kafka listener or a job is not watched. The handler does nothing, and must not also call `changes()` for the same
entity, or every change appears twice.

A foreign key is reported as it is in .NET. When a row is pointed at a different related row (a `@ManyToOne`, or
the owning side of a `@OneToOne`), the change is the related row's id, under the property's name with `Id` appended:
`vendor` moving from row 10 to row 20 is `VendorId`, `10` to `20`. The related row is not loaded to learn its id.
An entity that also maps the column as a simple property (`vendorId` beside a read-only `vendor`) reports it once,
through that property. `@AuditMask` on the link masks it.

A column whose stored form differs from its Java value is reported as it is in .NET too: as one property, in the
form the database stores. That covers two kinds of column:

- one written through an `AttributeConverter`: a list or an object kept as JSON reads as that JSON, and a simple value
  kept in another form (a boolean as `Y`/`N`) reads in that form;
- a binary column (`byte[]`), which reads as base64.

An object kept in a JSON column (`@JdbcTypeCode(SqlTypes.JSON)`) is reported field by field, the way .NET reports an
object it owns and stores as JSON: each field that changed is a change of its own, with the object's class as its
`Entity` and the owning row's key as its `RowKey`. An object inside it is read the same way under its own class, and
a list of simple values inside it as a JSON array. A list of objects is read item by item, the `RowKey` being the
owner's with the item's position appended, counted from one (`12|1`). `@AuditMask` on a field masks that field;
on the column it masks every field inside. Three things to know:

- Fields are whatever the getters expose. A value that has none (a record, a class of public fields, a `Map`, a list
  of simple values, plain text) is reported as one value, the JSON before and after. A stored field with no getter is
  not reported on its own: when one changed, alone or beside fields that have getters, the column is reported as
  one value and nothing is reported field by field beside it. A getter is matched to what is stored by name,
  whatever the case or separators (`startHour` stored as `start_hour`); a property stored under a different name
  (`@JsonProperty("x")` on `getY()`) is reported as one value too. A getter that computes its value is reported
  like a stored field.
- A list of objects that gained or lost an item is reported as one value too, so the item shows. The .NET
  interceptor reads modified entries only, so there an added or removed item is not reported.
- One value shows everything stored in it, so where anything inside is masked (`@AuditMask` on a field, getter or
  record component, or a masked name) the whole column is masked.

An array or a list of simple values kept in one column (`String[]`, or a `List<String>` mapped as an array) is
reported as a JSON array of its elements, `["A","B"]`, as .NET reports one. That is how it reads whatever the database
keeps: SQL Server has no array type, so there Hibernate stores it as binary.

A value replaced with an equal one is not reported. Nor is a column the update does not write
(`updatable = false`), on the row or in an embedded object, whatever the object was given: the database kept its
value. Where the stored value is large enough to swamp the event, or holds anything that must not leave the
service, mask the property with `@AuditMask` or by name, or set `propertyChanges = false` on the endpoint: a masked
column is masked whole.

An embedded object (`@Embedded`) is reported the way .NET reports an owned type: each field of it that changed is a
change of its own, with the embedded class as its `Entity` and the owning row's key as its `RowKey`. A station whose
address moves city reports `City` under `Address`, beside the station's own changes under `Station`. The fields are
read as a row's are, so a foreign key, a converter column or a further embedded object inside it is covered, and
`@AuditMask` on a field masks that field. `@AuditMask` on the embedded property itself masks every field inside.
Setting or clearing the whole object reports each field from or to empty. Two embedded properties of the same class
on one row cannot be told apart in the event; .NET has the same limit.

What the listener does not see:

- An update made by a query (`@Modifying`, JPQL or native) rather than through a loaded entity. The event of an
  endpoint that updates that way carries no captured property changes: say what happened in the note, or record the
  value by hand with `audit.change(name, before, after)`.
- Collections of rows or elements kept in their own table (`@OneToMany`, `@ElementCollection`). A changed row of a
  `@OneToMany` is reported as that row's own update; rows added to or removed from either are not reported.
- Creates and deletes: property changes describe updates only.

*A service without JPA says it itself.* MeasureEval is on MongoDB, which tracks no changes, so there the handler calls
`audit.changes(before, after)`. Call it before the save: it compares at the moment it is called. These changes carry no
`Entity` or `RowKey`.

**Testing a controller.** A `@WebMvcTest` slice applies the mechanism and starts with no Kafka configuration. To
assert on the event, supply an `AuditPublisher`; it replaces the Kafka one:

```java
static final List<AuditEventMessage> recorded = new CopyOnWriteArrayList<>();

@TestConfiguration
static class Config {
    @Bean AuditPublisher publisher() { return recorded::add; }
}
```

A slice in a module with no `bootstrap.yml` (as in this module's own tests) also needs
`spring.cloud.azure.appconfiguration.enabled=false`. See `audit/slice/AuditSliceTest.java`.

A slice that mocks its repositories saves nothing, so its events carry no captured property changes. To assert them,
run the controller over a real repository: `@WebMvcTest` with `@AutoConfigureDataJpa` and an embedded database. Do
not wrap such a test in a transaction; a change is reported only when its transaction commits.
