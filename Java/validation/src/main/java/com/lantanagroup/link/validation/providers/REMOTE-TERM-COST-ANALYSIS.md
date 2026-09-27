# Remote terminology cost analysis

Cost profile of `RemoteTermServiceValidation` as currently deployed inside
`ValidationService`: which methods HAPI actually calls during resource
validation, which of those hit the cache, which don't, and where the
remaining latency and reliability costs live.

Scope: the `validation` service, in production, today. Not a proposal for
adoption by any other service.

## How the class is wired

`RemoteTermServiceValidation` is a HAPI `IValidationSupport` implementation.
`ValidationService`'s constructor composes it into a `ValidationSupportChain`:

```java
public ValidationService(FhirContext, ArtifactService, LinkConfig, ValidationCacheService) {
    ValidationSupportChain chain = new ValidationSupportChain(
        new DefaultProfileValidationSupport(fhirContext),
        artifactService.getValidationSupport(),                      // IG packages
        new SnapshotGeneratingValidationSupport(fhirContext));
    loadTerminologyValidationSupport(...);                           // adds RemoteTermServiceValidation
                                                                     // (or an in-memory fallback pair)
    fhirValidator = new FhirValidator(fhirContext);
    fhirValidator.registerValidatorModule(
        new FhirInstanceValidator(new CachingValidationSupport(chain)));
}
```

`RemoteTermServiceValidation` never appears on `ValidationService`'s public
surface — it's plumbing. `FhirInstanceValidator` calls into the chain during
`validateWithResult(resource)`, and the chain hands terminology questions to
whichever link claims the code system or ValueSet URL.

## What HAPI actually calls during `$validate`

In descending order of frequency:

| Method | When | Cached? |
|---|---|---|
| `isCodeSystemSupported` / `isValueSetSupported` | Chain traversal probes: on every coded element, HAPI walks the chain asking each link "do you handle this system / VS?" | ❌ uncached — full search-by-URL round-trip to the remote TS every time |
| `validateCode(system, code, display, vsUrl)` | Coded element binding check. Once the chain identifies which link owns the system/VS, the actual code check goes there. | ✅ `ValidationCacheService` (key: `Objects.hash(system, code, display, vsUrl)`) |
| `lookupCode(request)` | Display or property lookup for a validated code. | ❌ uncached |
| `fetchValueSet(url)` | Called opportunistically by `isValueSetSupported` when it needs to confirm the VS exists. | ❌ uncached, and see gap 3 below |
| `expandValueSet`, `validateCodeInValueSet`, `translateConcept`, `subsumes` | Not invoked during `$validate` on FHIR resources | n/a — not implemented, doesn't matter for this deployment |

## Cost gaps, ranked

### Latency

**Gap 1 — `isCodeSystemSupported` / `isValueSetSupported` are uncached
round-trips.** This is the single largest latency contributor. HAPI's chain
probes these methods per system per traversal; a bundle referencing 20 code
systems triggers 20 (or more, since the chain iterates each link) HTTP
round-trips just to answer "who owns this system?" — before any actual code
validation happens.

The `ValidationCacheService` only wraps `validateCode`. The `isXxxSupported`
methods bypass it entirely.

_Impact:_ every bundle validation pays this overhead. On a warm cache, code
validations are fast; support probes are still slow.

_Fix:_ cache the boolean result via the same `CacheManager` with a short TTL
(5–15 minutes is reasonable — the answer changes only when a code system is
added to or removed from the remote TS).

**Gap 2 — `lookupCode` is uncached.** Lower call frequency than
`isXxxSupported`, but still uncached. `$lookup` is invoked for display
verification when the resource declares a `display` that HAPI wants to
cross-check.

_Fix:_ cache with the same TTL model as gap 1. Same `CacheManager`.

### Reliability

**Gap 3 — `fetchValueSet` uses `CodeSystem.URL` as the search parameter.**
Line 246:

```java
IQuery<IBaseBundle> valueSetQuery = client.search().forResource("ValueSet")
    .where(CodeSystem.URL.matches().value(theValueSetUrl));
```

`CodeSystem.URL` is `CodeSystem`'s search parameter; on a `ValueSet` search
it works today only because both resources happen to expose a `url` search
parameter with the same name. Any HAPI refactor of search-param generation
breaks this silently.

_Fix:_ use `ValueSet.URL`. One-line change.

**Gap 4 — Silent failure on remote TS unreachable.** The `validateCode` path
catches `InvalidRequestException` and `ResourceNotFoundException` and returns
an ERROR-severity result. But a connection failure, socket timeout, or 5xx
that doesn't match those exception types propagates as an unhandled runtime
exception up through `ValidationService.validate(...)` — which then bubbles
to whichever caller triggered it (REST controller or Kafka consumer).

_Impact:_ during a remote TS outage, validation calls that would otherwise
degrade gracefully (missing code, unknown VS) turn into stack traces at the
service boundary. Kafka retries can pile up under a persistent outage.

_Fix:_ widen the exception coverage in `invokeRemoteValidateCode` to catch
connection / timeout failures, return a well-typed ERROR result with a clear
"terminology unavailable" message, and log at a distinct level or with a
distinct tag so ops can page. Consider a circuit breaker if outages recur.

### Code quality

**Gap 5 — Whitelist naming is inverted.** `whiteListCodeSystemRegex` /
`whiteListValueSetRegex` return `false` (unsupported) when the pattern
matches. That is a *skip list*, not a whitelist — patterns that match are
explicitly *excluded* from remote lookup.

_Impact:_ readers, especially new maintainers, must trace through the method
to work out what the config means. Config file docs likely inherit the
misleading name too.

_Fix:_ rename the field, config keys, and any documentation to
`skipCodeSystemRegex` / `skipValueSetRegex`. No behavior change; pure
clarity win.

**Gap 6 — `lookupCode` hard-rejects any FHIR version except DSTU3/R4.**
Lines 82–118 throw `UnsupportedOperationException` on R4B / R5. Currently
correct for this stack (R4 only), but locked in for any future FHIR version
migration.

_Impact:_ latent; not a live cost. Called out only because a future FHIR
version bump will hit this.

_Fix:_ loosen the version guard, or drop it entirely (HAPI handles version
differences at the client layer already).

## Consequences of the current implementation

| Dimension | Where it lands today |
|---|---|
| Per-bundle latency | Dominated by uncached `isXxxSupported` probes (gap 1). `validateCode` cache gives real steady-state wins on the code-check side but doesn't help the support probing. |
| Cold-cache latency | Every code check is a remote round-trip. `ValidationCacheService` fills in gradually. |
| Warm-cache latency | Code checks are near-free from cache. Support probes still uncached — so latency floor is set by them, not by `validateCode`. |
| Failure modes | Ambiguous during TS outages (gap 4). Some ops return ERROR results, some throw. |
| Traffic pattern | Roughly `N × isXxxSupported + M × validateCode` per bundle. Only the second term is cached. Even a fully warm cache still pays `N` support round-trips. |
| Observability | `link.validation.cache.validate-code.hit`/`.miss` counters (from the validation-cost-audit branch) surface hit rate on the *only* cached path. No per-op latency histograms, no counter on `isXxxSupported` or `lookupCode`. |

## Recommended fixes, by priority

1. **Cache the `isXxxSupported` probes** (gap 1). Single largest latency win.
   Use the existing `CacheManager` with a short TTL. Update
   `ValidationCacheService` to expose a second cached method (or a
   generalized `cacheProbe(kind, key, supplier)` shape). Add matching
   hit/miss counters to `ValidationMetrics`.
2. **Widen remote-TS failure handling** (gap 4). Broader catch in
   `invokeRemoteValidateCode`; distinct log tag so outages are searchable.
   Consider a circuit breaker if the ops runbook shows recurring TS
   incidents.
3. **Fix the `ValueSet.URL` search-parameter bug** (gap 3). One line, no
   behavior change today, removes a future failure mode.
4. **Cache `lookupCode`** (gap 2). Same TTL model as gap 1. Smaller
   throughput win but zero-cost engineering once gap 1 has the shape.
5. **Rename whitelist → skip list** (gap 5). Pure clarity; touch the field,
   config keys, and any docs.
6. **Defer `lookupCode` version guard** (gap 6). Not a live cost. Fix
   alongside a future FHIR version bump, not before.

## Interaction with the `validation-config` branch

The SKIP / SUPPRESS / LABEL categorization overhaul on `validation-config`
touches exactly the surface this document analyzes. Reviewing the two
together is more useful than reviewing either in isolation.

- **SKIP** hooks HAPI's `policyForCodedContent(...)` to return `IGNORE` (or
  drop specific `CodedContentValidationAction`s) before validation runs. For
  the code systems it targets, **the remote TS is never consulted at all** —
  no `isCodeSystemSupported` probe, no `validateCode` call.
- **SUPPRESS** hooks `isSuppressMessageId(...)` and drops specific message
  IDs after validation. **HAPI still calls the remote TS**; only the
  resulting message is suppressed downstream.
- **LABEL** is post-validation classification with no policy hook — no
  effect on remote traffic.

### Where the branches help each other

- **SKIP scope compounds with gap 1's fix.** SKIP reduces the number of code
  systems that ever reach `isCodeSystemSupported`; caching the probes
  handles what's left. Together they attack the largest latency contributor
  from two directions. Rules that shipped as SKIP on `validation-config`
  (`unknown_code_system`, `unresolved_epic_code_system_uri`,
  `unresolved_medispan_code_system_uri`, `incorrect_display_value_for_code`)
  are already the noisiest sources of remote traffic today; SKIP bypasses
  them at the policy layer, and gap 1's cache mops up the rest.
- **Gap 4's fix makes SUPPRESS deterministic during TS outages.** SUPPRESS
  depends on HAPI producing the expected message that then gets dropped. If
  the underlying `validateCode` throws (which is what happens today on
  unhandled connection failures — gap 4), no message is emitted, so
  SUPPRESS never fires and the caller sees a stack trace instead of the
  intended silent behavior. Landing gap 4 before or with the categorization
  merge makes SUPPRESS's behavior stable across TS availability states.
- **Metric alignment.** `link.validation.rule.outcome{outcome=skipped/suppressed/labeled}`
  (validation-config) and `link.validation.cache.validate-code.hit`/`.miss`
  (validation-cost-audit) together paint a complete terminology-cost
  picture: skipped rules never touch the remote TS; suppressed rules pay
  the full remote cost but emit no message; labeled rules pay the cost and
  emit a message. A dashboard combining both is more useful than either
  counter alone.

### Where they tension

- **Two independent skip surfaces for the same problem.** The Category
  `scope.codeSystems` (SKIP strategy) and `RemoteTermServiceValidation`'s
  `whiteListCodeSystemRegex` (gap 5) both configure "don't call the remote
  TS about these URLs." Overlap is not a correctness issue — a code system
  covered by both is simply bypassed twice — but it's two config surfaces
  to keep in sync, and their semantics differ (Category is per-rule with
  named strategies; the field is process-wide with regex matching). If gap
  5 is renamed and re-scoped per the recommended fixes above, worth
  deciding whether the two continue to coexist or whether Category
  `scope.codeSystems` becomes the single source of truth for "skip the
  remote TS."
- **SKIP shrinks the cache's addressable space.** Once SKIP short-circuits
  a code system, `validateCode` is never called for it. Cache hit rate for
  that system trends to zero — but total remote traffic also drops for the
  same reason, so this is "less cache benefit because we need less
  caching," not a regression. Worth flagging for anyone reading hit-rate
  panels in isolation.

### What to align before both branches merge

1. **Scope-comparison audit.** Compare Category `scope.codeSystems`
   patterns (validation-config) against `whiteListCodeSystemRegex` values
   (validation, today). Any overlap should be a deliberate choice, not
   drift.
2. **Gap 4 sequencing.** Fix gap 4 before or with the categorization merge
   so SUPPRESS behaves predictably under TS outages.
3. **Dashboards.** Terminology-cost panels should combine
   `link.validation.rule.outcome` with the cache hit/miss counters — the
   individual metrics each tell an incomplete story.

## What this analysis does not change

- `RemoteTermServiceValidation`'s place inside `ValidationService`.
- The chain composition or the `ValidationSupportChain` structure.
- The caching-support wrapper (`CachingValidationSupport`) at the top of
  the chain.
- The choice of remote FHIR TS endpoint (`fhirTerminologyServiceUrl`) or
  the Link terminology service fallback (`terminologyServiceUrl`).
