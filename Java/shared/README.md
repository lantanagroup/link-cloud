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
or an exception publishes nothing, and a failed publish never fails the request. Design and wire contract:
[`docs/superpowers/specs/2026-11-23-java-audit-mechanism-design.md`](../../docs/superpowers/specs/2026-11-23-java-audit-mechanism-design.md).

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
    Category existing = categoryRepository.findById(id).orElse(null);
    audit.overrideAction(existing == null ? AuditAction.CREATE : AuditAction.UPDATE);
    audit.changes(existing, category);
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

- Call `changes(before, after)` before the save: it compares at the moment it is called, and a managed entity that
  `save` merges into would show no difference afterwards.
- Values passed to `set(name, value)` fill the note as given and are not masked. They must be safe to store in the
  audit trail. Masking (`@AuditMask`, the shared name list and patterns, `AuditMaskingCustomizer`) covers property
  changes only.

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
