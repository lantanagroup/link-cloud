# Validation Categories — Intent and Roadmap

## Problem

> "Currently, validation is very noisy. As we and the client review the messages
> it's producing, we're better understanding what the client does and doesn't
> care about. So, as we identify messages/validations that the client doesn't
> care about, I'd like to know if we can configure the system to not report
> those things entirely; and ideally not even do the work of checking those
> things (don't waste CPU)."

Two goals in priority order:

1. **Don't report** messages the client has explicitly classified as acceptable.
2. **Don't compute** them in the first place (save CPU on terminology lookups,
   reference resolution, etc.).

## Current state

`Java/validation/src/main/resources/categories.json` holds **50 hand-curated
rules** representing domain knowledge accumulated from reviewing real HAPI
validation output. Each rule has:

- `id`, `title`, `severity` (`ERROR` / `WARNING` / `INFORMATION`)
- `acceptable` — boolean; whether this kind of issue should block submission
- `guidance` — human-readable explanation
- `matcher` — regex over `SEVERITY` / `CODE` / `MESSAGE` / `EXPRESSION` fields,
  optionally composite (`requiresAllChildren` for AND, default OR)

Current rule distribution:

| | Acceptable | Not acceptable | Total |
|---|---:|---:|---:|
| ERROR | 15 | 16 | 31 |
| WARNING | 7 | 5 | 12 |
| INFORMATION | 7 | 0 | 7 |
| **Total** | **29** | **21** | **50** |

**Today's flow** is 100% post-validation labeling:

1. HAPI `FhirInstanceValidator` runs against the bundle and produces every
   `SingleValidationMessage` it would normally produce.
2. `ValidationService.validate(...)` converts those messages into `Result`
   entities.
3. `CategorizationService.categorize(...)` matches each `Result` against all
   category rules and tags it with matching categories.
4. `ReadyForValidationConsumer` persists the tagged results and produces
   `ValidationComplete(valid = allTaggedCategoriesAreAcceptable)`.

Every rule, today, is a **label** — HAPI does the full validation work whether
the client cares about the result or not.

## Why "integrate into HAPI" is the wrong framing

HAPI's philosophy is that validation returns the truth and consumers decide what
to do. There's no built-in `IValidationMessageClassifier` hook; HAPI deliberately
ships no domain-specific suppression rules because every deployment's "known
acceptable" set is different.

The NHSN/Lantana categorization itself doesn't belong in HAPI core. But the
**plumbing** — letting a project hook into the right validation lifecycle stage
for each rule — is exactly what HAPI's existing extension points are for.

## Design

Add a per-rule **`strategy`** field to `categories.json`. Each rule declares the
earliest layer at which it can be handled:

| Strategy | Implementation | When it fires | CPU saved | Output | Audit trail |
|---|---|---|---|---|---|
| **`SKIP`** | `IValidationPolicyAdvisor.policyForCodedContent` / `policyForReference` returns `IGNORE` | Before the engine does the check | **Yes** (real — terminology lookups, reference resolution, ValueSet expansion all skipped) | Message never produced | Counter only |
| **`SUPPRESS`** | `IValidationPolicyAdvisor.isSuppressMessageId(path, messageId)` returns `true` | After the check runs, before the message is emitted | Negligible (the work has already happened) | Message dropped from `OperationOutcome` | Counter only |
| **`LABEL`** | `CategorizationService.categorize(...)` matches and tags (current behaviour) | After validation completes | None | Message in output, tagged with category | Full `Result` record in DB |

Default is `LABEL` for unmarked rules so this rollout is purely additive — every
existing rule keeps its current behaviour until someone deliberately promotes it.

Rules with `acceptable: false` **always** stay `LABEL`. Those messages must
appear in `ValidationComplete` to drive the report's `FailedValidation` state.

### Why three strategies and not two

- `SKIP` is the only strategy that saves real CPU. Use it when a rule's matcher
  can be expressed in terms of HAPI's policy decision points (code system URL
  patterns, reference type / target, ValueSet URL).
- `SUPPRESS` doesn't save CPU but produces a clean `OperationOutcome`. Use it
  when the rule maps to a stable HAPI message id (the `I18nConstants`
  constants — these are version-stable in a way that message text is not).
- `LABEL` is the universal fallback. Any rule whose matcher requires inspecting
  the produced message text (or whose decision depends on other resources in
  the bundle) has to stay here. It also keeps the audit trail intact, which the
  team uses to verify the categorization itself is correct.

### Rough classification of the existing 50 rules

A first-pass sort based on what each rule matches against:

| Category | Approx % | Likely strategy |
|---|---:|---|
| Terminology (unknown system, can't validate code, display mismatch) | ~30% | `SKIP` via `policyForCodedContent(IGNORE)` — biggest win |
| Reference resolution failures | ~10% | `SKIP` via `policyForReference(IGNORE)` |
| Cardinality / structural (`minimum required = 1, but only found 0`) | ~20% | `LABEL` (HAPI doesn't expose per-element skip without dropping the whole profile) |
| Constraint invariants (`Rule us-core-X: ... Failed`, `Rule rat-1: ... Failed`) | ~15% | `LABEL` (no per-invariant skip in HAPI; map to message id if possible for `SUPPRESS`) |
| Best-practice (`bpr-*`) | ~5% | Global `BestPracticeWarningLevel.Ignore` or `LABEL` |
| Composite / cross-resource / everything else | ~20% | `LABEL` |

Roughly **40%** of rules are realistic `SKIP` candidates. The terminology tier
alone, moved to `SKIP`, is likely the single largest CPU win — those rules tend
to be both noisy and expensive (remote terminology calls, ValueSet expansion).

### Schema change

```json
{
  "id": "unknown_code_system",
  "title": "Unknown Code System",
  "severity": "ERROR",
  "acceptable": true,
  "strategy": "SKIP",
  "scope": {
    "codeSystems": [
      "https?://fhir\\.cerner\\.com/.*",
      "https?://open\\.epic\\.com/FHIR/StructureDefinition/.*",
      "urn:oid:1\\.2\\.840\\.114350\\..*"
    ]
  },
  "guidance": "Internal: unrecognized code system; acceptable unless another coding...",
  "matcher": { "...": "..." }
}
```

- `strategy`: enum (`SKIP` / `SUPPRESS` / `LABEL`); default `LABEL` when absent.
- `scope`: present on `SKIP` rules to narrow which calls the rule applies to and
  which specific actions are short-circuited when it does. Shape depends on the
  policy hook:
  - `codeSystems`: regex patterns matched against `system` URLs in the
    `policyForCodedContent` advisory call. **Narrows when** the rule fires.
    Phase 1.
  - `excludeActions`: names of HAPI `CodedContentValidationAction` enum values
    to remove from the returned action set. **Narrows what** the rule does when
    it fires — instead of removing every check (`EnumSet.noneOf(...)`), it
    removes only the named actions (`EnumSet.complementOf(...)`). Phase 2.
  - `valueSets`: regex patterns matched against the bound ValueSet URL. Phase 1
    follow-up — **deferred** until there's a clean use case (no proposed Phase 2
    candidate actually fit the `valueSets` axis once we examined the matchers).
  - `referencePaths`: regex patterns matched against the reference target path.
    Phase 3.

  The axes compose: a rule with `codeSystems` AND `excludeActions` fires only
  when the system matches AND removes only the named actions when fired
  (useful pattern: "Epic display mismatches don't matter, but standard codes
  still do"). The matcher-narrowing-vs-action-narrowing distinction is
  important — they answer different questions and can be combined to express
  a precise policy.

  **Unscoped `SKIP`** is a separate semantic: a `SKIP` rule with no `scope` at
  all fires on every relevant policy-hook call.

  - **Unscoped + no `excludeActions`**: returns an empty action set on every
    call — a coarse, high-risk semantic that kills validation for every rule
    that would have caught a message at that element, including rules with
    `acceptable: false`. Reserved for the rare case where the team genuinely
    wants global suppression at the hook layer. Do not use this shape to mean
    "I haven't figured out the scope yet"; prefer a precise scope, or stay
    `LABEL` until `SUPPRESS` is available (Phase 4).
  - **Unscoped + `excludeActions`**: surgical global suppression — fires on
    every call but only removes the named action(s). No over-skip risk because
    other rules' actions still run. This is the safe shape for rules whose
    intent is "we never care about HAPI's X check anywhere"
    (`incorrect_display_value_for_code`'s `["InvalidDisplay"]` is the
    canonical example).

  The advisor logs loudly on load if an unscoped SKIP rule is configured
  without `excludeActions`.
- `matcher`: retained even on `SKIP` / `SUPPRESS` rules — it's the fallback
  identification for cases where the advisor call doesn't have enough context
  to disambiguate, and it stays useful for testing and human review of what
  the rule was originally written against. A migration that promotes some
  matcher branches to `SKIP` while leaving others as `LABEL` is a **partial
  migration**; the rule's `guidance` should explicitly call out which branches
  are which (see the `unknown_code_system` entry in the Migration log below for
  the worked example).

## Implementation roadmap

The goal is a sequence of independently-mergeable PRs, each of which either
adds capability or migrates a small set of rules. No big-bang change.

### Phase 1 — Mechanism (one PR) ✔ DONE

Adds the plumbing for `SKIP` rules with the smallest possible production change
and migrates one rule end-to-end to prove the pipeline.

**Shipped:**

- `CategoryStrategy` enum (`SKIP` / `SUPPRESS` / `LABEL`) and `CategoryScope`
  DTO with `codeSystems` / `valueSets` / `referencePaths` regex axes. Only
  `codeSystems` is consumed in Phase 1; the others are present in the schema
  so later phases don't need to break it.
- `Category` and `CategorySnapshot` entities carry `strategy` (default
  `LABEL`) and `scope` (nullable JSON column via `CategoryScopeConverter`).
- `V20260616__Add_category_strategy_and_scope.sql` and matching `U` undo
  script for the column additions, with defensive `if not exists` guards.
- `CategoryBackedPolicyAdvisor extends FhirDefaultPolicyAdvisor` overriding
  only `policyForCodedContent(...)`. All other advisor methods inherit
  HAPI's defaults. Constructor-time validation drops rules that would be
  unsafe (`acceptable=false` SKIP rules, regex-only SKIP rules whose every
  pattern fails to compile) with WARN logs.
- Unscoped `SKIP` capability is intentionally kept but discouraged in code
  comments and in this document.
- Wired into `ValidationService` via `instanceValidator.setValidatorPolicyAdvisor(...)`.
- New metric `link.validation.rule.outcome` (tagged with `rule_id` and
  `outcome=skipped|suppressed|labeled`) on `ValidationMetrics`. The advisor
  increments `skipped`; `CategorizationService` increments `labeled` for
  every post-validation match. `suppressed` is wired but unused until
  Phase 4 lands.
- `unknown_code_system` migrated to `SKIP` with the four EHR-vendor URL
  patterns as scope. This is a **partial** migration: the rule's four
  generic-shape matcher branches stay as a `LABEL` fallback until Phase 4
  ships SUPPRESS support.

**Tests added:** `CategoryStrategyTest`, `CategoryScopeConverterTest`,
`CategoryBackedPolicyAdvisorTest`. Existing `CategorizationServiceTest`
updated with a `ValidationMetrics` mock.

**Open follow-ups carried out of Phase 1:**

- Production migration runbook: we don't yet know who runs the V/U scripts
  in prod (Hibernate's `ddl-auto: update` handles dev/docker/local; only
  the `docker` profile sets that). Confirm before the next migration ships.
- `database/reference/create.sql` was manually updated to reflect the new
  columns; running the `local` profile against a SQL Server instance
  regenerates this file from JPA metadata. Verify on first opportunity.

### Phase 2 — Migrate terminology rules to SKIP ✔ DONE

This phase started as "data-only JSON migrations on top of Phase 1's
mechanism." Walking through the candidates revealed that mechanism extension
was needed too: most candidate rules couldn't be expressed as scope-narrowed
SKIP without over-skipping other rules' territory. Phase 2 ended up shipping
a small mechanism extension (`excludeActions` for surgical per-action
narrowing) plus three migrations.

**Shipped:**

- `scope.excludeActions` on `CategoryScope` and the corresponding return-value
  branch in `CategoryBackedPolicyAdvisor`. When a matched SKIP rule has
  `excludeActions`, the advisor returns `EnumSet.complementOf(EnumSet of named
  actions)` instead of `EnumSet.noneOf(...)`. Unknown action names are logged
  and dropped at load time; all-invalid demotes the rule to LABEL.
- Class-level Javadoc on `CategoryBackedPolicyAdvisor` updated to introduce
  the new capability and frame the unscoped-SKIP-with-`excludeActions` shape
  as safe (vs. unscoped-without-`excludeActions`, which stays discouraged).
- Three rules migrated to `SKIP`. See the Migration log below for each rule's
  shape and rationale.
- The proposed `unable_to_validate_code` candidate was removed from the list —
  `acceptable: false`, the advisor would have demoted it anyway.
  `unresolved_code_system` and `non_loinc_code_glucose_point_of_testing` were
  re-classified as Phase 4 candidates because their matchers are too narrow
  for pre-validation hooks to target precisely.
- `scope.valueSets` deferred — none of the proposed candidates actually fit
  the axis once we examined the matchers. Adding the implementation without
  a real use case is YAGNI; we'll add it when a clean case appears.

**Tests added:** six new cases in `CategoryBackedPolicyAdvisorTest` covering
single-action, multi-action, partial-failure (one valid + one invalid name),
total-failure (all names invalid), and composability of `codeSystems` +
`excludeActions` on the same rule.

### Phase 3 — `policyForReference` support — ⏭ DEFERRED (no current candidates)

Phase 3 was planned as "implement `policyForReference` + `referencePaths` scope
axis + migrate the small set of reference-resolution rules to SKIP." Walking
through the rule set revealed that **no current `acceptable=true` rule has a
matcher targeting messages that `policyForReference` actually controls**.

`policyForReference` returns a single `ReferenceValidationPolicy` enum value
(`IGNORE` / `CHECK_TYPE_IF_EXISTS` / `CHECK_EXISTS` / `CHECK_EXISTS_AND_TYPE` /
`CHECK_VALID`) and decides how the validator handles a Reference target —
whether to resolve, what to check after resolution. It does not affect
Reference *structure* messages (`ref-1`-style "Reference should have a
display"), nor cardinality (`Patient.link.other: minimum required = 1`), nor
FHIRPath invariants (`Rule us-core-2 Failed`).

The reference-themed rules in `categories.json` cluster into three categories,
none of which Phase 3's mechanism would help:

- **Structural reference rules** (e.g.
  `medicationrequest_requester_does_not_have_a_proper_reference`) — fire on
  HAPI's `ref-1` structural warning before resolution. Better fit for SUPPRESS
  (Phase 4) against the underlying `I18nConstants` message ID.
- **Canonical-URL resolution rules** (e.g. `unresolved_url`,
  `unable_to_validate_measure_measure_not_found`) — about profile / ValueSet /
  Measure canonical URLs, not Resource Reference URLs. `policyForReference`
  doesn't apply. All `acceptable: false` anyway.
- **Cardinality / invariant rules** that happen to mention "reference"
  (`missing_reference_to_linked_patient_resource`, `us_core_2`) — also
  `acceptable: false` and structurally unrelated to resolution policy.

**Revisit criteria.** If a future bundle shape produces resolution-failure
messages (`"Could not resolve reference"`, `"Reference target is of wrong
type"`) that the team classifies as acceptable, that's the trigger to
implement Phase 3. At that point the work is small:

1. Override `policyForReference(...)` on `CategoryBackedPolicyAdvisor` to
   return `IGNORE` (or another less-strict policy) when a SKIP rule's scope
   matches the incoming `path` / `url` / `destinationType`.
2. Wire up the already-stubbed `scope.referencePaths` axis on
   `CategoryScope` plus a new `scope.referenceUrls` axis if URL-based
   narrowing turns out to matter.
3. Add unit tests mirroring the Phase 1 pattern.

Until then, the `referencePaths` field stays in `CategoryScope` as a
forward-compat stub. The advisor doesn't read it.

Phase 4 (SUPPRESS) is the next investment — see below.

### Phase 4 — Add isSuppressMessageId support and migrate (research-heavy)

`SUPPRESS` runs the validation check but drops the produced message before it
reaches the `OperationOutcome`. No CPU savings, but unlike `SKIP` it can target
a specific HAPI message ID without over-skipping other rules' territory. The
four generic-shape matcher branches of `unknown_code_system` (and the same
shape across other partially-migrated rules) need this strategy to complete
their migration.

**Concrete work, in order:**

1. **Identify HAPI message IDs for each currently-LABEL'd matcher branch.**
   The authoritative list is `org.hl7.fhir.utilities.i18n.I18nConstants` —
   each user-facing validation message is generated from a constant whose
   value is the template. The research method: for a given matcher branch
   (e.g., `^Unknown Code System '.*'$`), find the `I18nConstants` constant
   whose template produces that text. Some sample mappings (unverified —
   need to confirm on the version of `org.hl7.fhir.utilities` shipped with
   the current HAPI):
   - `^Unknown Code System '.*'$` → likely `UNKNOWN_CODESYSTEM` or
     `Unknown_Code_System_R5`
   - `A code with no system .* A system should be provided` → likely
     `Code_Without_a_System_R5`
   - `^CodeSystem is unknown and can't be validated` → likely
     `CodeSystem_CS_UNK_EXTERNAL_R5`
   - `The Coding provided \(.*\) is not in the value set 'V3 Value SetActEncounterCode'`
     → likely `Terminology_TX_NoValid_SET_2` with the V3 binding
2. **Schema decision.** A single rule can hold both a `SKIP` scope and one
   or more `SUPPRESS` message IDs (the `unknown_code_system` migration
   shape). Decide whether to add a `suppressMessageIds: string[]` field
   alongside `scope` (each rule can declare both axes) or to split into
   separate categories. The author's lean is the former — keeps related
   matcher branches under one rule entry.
3. **Implementation.** Extend `CategoryBackedPolicyAdvisor` to override
   `isSuppressMessageId(path, messageId)`. Pre-compile the set of
   suppressible message IDs at advisor construction (same lifecycle as the
   existing SKIP rule load). Increment
   `link.validation.rule.outcome{outcome=suppressed}` on match.
4. **Migrate.** Start by completing `unknown_code_system` — once SUPPRESS
   works for its four generic-shape branches, the rule is fully covered
   and the matcher fallback becomes pure dead-letter defense. Then move on
   to other rules with the same shape (`unable_to_validate_code`'s generic
   branches, `incorrect_display_value_for_code`'s generic branches, etc.).
5. **Verify** on a representative bundle that the messages actually stop
   appearing and that no `acceptable=false` rule is silently swallowed by
   accident.

A `TODO` comment in `CategoryBackedPolicyAdvisor`'s Javadoc points at this
section so the gap is discoverable from the code.

### Phase 5 — Profile scope audit (separate, larger PR)

Independent of categories.json. Audit which FHIR profiles the validator
actually binds against in production and whether any of them generate
disproportionate "acceptable" noise. Dropping a single noisy profile from the
validation set is often a much bigger CPU win than any number of individual
rule migrations.

## Tradeoffs and risks

### Pros

- **CPU savings** are real for `SKIP` rules in the terminology tier. Concrete
  expectation: meaningful drop in remote terminology service call count.
- **Cleaner `OperationOutcome`** for SKIP/SUPPRESS rules. Downstream consumers
  get less noise to filter, regardless of whether they use our `Result`
  categorization at all.
- **Configurability**. After Phase 1 ships, moving a rule between strategies is
  a JSON-only change. No code rebuilds required to tune.
- **Upgrade resilience for `SUPPRESS`**: message IDs are stable across HAPI
  versions; regex matchers against message text are not.

### Cons

- **`SKIP` hides messages entirely.** If a scope is too broad, the team loses
  visibility into messages it would otherwise have wanted to see. Mitigated by:
  - Per-rule counter (rule fired N times even if no messages were emitted)
  - Default new rules to `LABEL` and explicitly promote
  - Code review focus on `SKIP` PRs vs `LABEL` PRs
- **`SUPPRESS` requires one-time research per rule** to identify the HAPI
  message id. Lower-leverage than `SKIP` since CPU savings are marginal — only
  worth it for rules that produce extremely high message volume.
- **Two systems to operate**. `CategoryBackedPolicyAdvisor` and
  `CategorizationService` both consult the same `categories.json` but at
  different lifecycle stages. Adds a place where the two can drift if the
  schema changes. Mitigated by sharing the rule-loading code and routing the
  strategy choice in one place.

### What this does not solve

- **Per-invariant skipping.** HAPI doesn't expose the ability to skip a
  specific FHIR-path invariant (e.g. `us-core-2`) while keeping the rest of US
  Core enforced. The only way to skip an invariant is to stop binding the
  whole profile.
- **Cardinality from profiles you want to keep.** If US Core requires
  `Patient.gender` and the client doesn't care, the only fix is removing US
  Core from the profile set (Phase 5).

## Verification before each promotion

- **Audit the rule's matcher.** What is it actually catching? Is the JSON
  matcher representative of the population of messages it would suppress, or
  just the historical examples we wrote the regex against?
- **Verify the scope's blast radius.** For a `SKIP` rule, what other messages
  would the same `policyForCodedContent(IGNORE)` decision suppress? Anything
  whose suppression we'd regret?
- **Measure before/after** on a representative validation:
  - Total message count
  - Per-severity message count
  - Validation wall time
  - Terminology service call count (if remote)

## Open questions

These aren't blockers for Phase 1 but should be answered before later phases:

1. **Do we want per-bundle overrides?** A facility might want stricter
   validation than the default — i.e., promote a `SKIP`'d rule back to `LABEL`
   for their bundles only. Today the rule set is global; adding per-tenant
   overrides is a real feature, not a free side effect.
2. **Where does configuration live long-term?** `categories.json` is checked
   in; that's fine for the team's curated set. If we move to per-tenant
   overrides, the data store probably has to follow.
3. **Should we expose category counters in the existing observability
   stack** (Grafana / Loki) or add them to the existing `ValidationMetrics`
   bean? The latter is closer to how validation observability works today.
4. **How do we deal with rule deletions?** If a rule promoted to `SKIP` gets
   deleted from `categories.json`, the advisor stops considering it and
   messages reappear. Is that the desired behaviour, or do we want explicit
   sunset markers?

## Migration log

A record of which rules have been promoted out of `LABEL`, when, and what the
remaining gaps are. Each entry should answer: which matcher branches moved
where, what stayed behind, and what blocks the rest.

| Date | Rule | Strategy | Coverage | Notes |
|---|---|---|---|---|
| 2026-06-16 | `unknown_code_system` | `SKIP` (scoped) | Partial — 4 of 8 matcher branches | URL-specific branches (Cerner FHIR, Epic FHIR StructureDefinition, Epic OIDs `1.2.840.114350.*`, namespace `1.2.246.537.6.96.*`) targeted via `scope.codeSystems`. The four generic-shape branches (`Unknown Code System '<...>'`, `A code with no system...`, `CodeSystem is unknown...`, `V3 ValueSet ActEncounterCode`) stay as the `LABEL` fallback until Phase 4 SUPPRESS support exists. |
| 2026-06-16 | `unresolved_epic_code_system_uri` | `SKIP` (scoped) | Complete | Two anchored `scope.codeSystems` patterns mirror the rule's existing composite matcher: `^urn:oid:1\.2\.840\.114350.*$` (Epic OID prefix) and `^https?://.*epic\.com/.*$` (any URL containing `epic.com`). Scope is broader than `unknown_code_system`'s Epic patterns; the overlap is harmless (first-match-wins on overlapping calls, identical outcome). Matcher kept as defensive fallback. |
| 2026-06-16 | `unresolved_medispan_code_system_uri` | `SKIP` (scoped) | Complete | Single anchored exact-match pattern `^urn:oid:2\.16\.840\.1\.113883\.6\.68$` for the Medispan GPI root OID. Anchoring is deliberate — Medispan sub-OIDs (`2.16.840.1.113883.6.68.X`) are different code systems and should continue to validate. `Pattern.find()` semantics without anchors would over-match them. |
| 2026-06-16 | `incorrect_display_value_for_code` | `SKIP` (unscoped + `excludeActions`) | Complete | First use of the Phase 2 `excludeActions` mechanism. Unscoped (fires on every `policyForCodedContent` call), with `excludeActions: ["InvalidDisplay"]`. Returns `EnumSet.complementOf(EnumSet.of(InvalidDisplay))` — every coded-content check runs except the display-name check. No over-skip risk: other rules' actions (`VSCheck`, `InvalidCode`, etc.) are untouched, so `invalid_code_in_required_valueset` (`acceptable: false`) and friends still produce their messages. Matcher kept as defensive fallback. |

## Related code

| File | Role |
|---|---|
| `src/main/resources/categories.json` | The hand-curated rule set (50 rules) |
| `src/main/resources/database/migrations/V*.sql`, `U*.sql` | Forward and undo schema migrations |
| `src/main/resources/database/reference/create.sql`, `drop.sql` | Hibernate-regenerated schema snapshots; PR-review aid |
| `src/main/java/.../entities/Category.java`, `CategoryRule.java`, `CategorySnapshot.java` | DB entities backing each rule and its versioned history |
| `src/main/java/.../entities/CategoryStrategy.java` | Enum (`SKIP` / `SUPPRESS` / `LABEL`) — added in Phase 1 |
| `src/main/java/.../entities/CategoryScope.java` | Scope DTO with `codeSystems` / `valueSets` / `referencePaths` axes — added in Phase 1 |
| `src/main/java/.../converters/CategoryScopeConverter.java` | JPA `AttributeConverter` for `CategoryScope` ↔ JSON column |
| `src/main/java/.../matchers/Matcher.java`, `CompositeMatcher.java`, `RegexMatcher.java` | The matcher polymorphism (used by `LABEL`) |
| `src/main/java/.../entities/ResultField.java` | Enumerates which message fields a matcher can target |
| `src/main/java/.../services/CategorizationService.java` | Loads rules; runs `LABEL` matching post-validation; increments the `labeled` counter |
| `src/main/java/.../services/CategoryBackedPolicyAdvisor.java` | HAPI policy advisor — runs `SKIP` decisions at `policyForCodedContent`. Added in Phase 1; Phase 4 adds `isSuppressMessageId` here |
| `src/main/java/.../services/ValidationService.java` | Where the `FhirInstanceValidator` lives — Phase 1 wires the advisor here via `setValidatorPolicyAdvisor(...)` |
| `src/main/java/.../services/ValidationMetrics.java` | Bean owning the `link.validation.rule.outcome` counter; constants `OUTCOME_SKIPPED` / `OUTCOME_SUPPRESSED` / `OUTCOME_LABELED` are the canonical outcome labels |
| `src/main/java/.../services/ReadyForValidationConsumer.java` | Kafka consumer that drives the categorize → persist → publish pipeline |
