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

- `strategy`: enum (`SKIP` / `SUPPRESS` / `LABEL`); default `LABEL` when absent
- `scope`: present only for `SKIP` rules; shape depends on the policy hook
  - `codeSystems`: regex patterns matched against `system` URLs in the
    `policyForCodedContent` advisory call
  - `valueSets`: regex patterns matched against the bound ValueSet URL
  - `referencePaths`: regex patterns matched against the reference target path
- `matcher`: retained even on `SKIP` / `SUPPRESS` rules — it's the fallback
  identification for cases where the advisor call doesn't have enough context
  to disambiguate, and it stays useful for testing and human review of what
  the rule was originally written against.

## Implementation roadmap

The goal is a sequence of independently-mergeable PRs, each of which either
adds capability or migrates a small set of rules. No big-bang change.

### Phase 1 — Mechanism (one PR)

Add the plumbing for `SKIP` rules with the smallest possible production change.

- Extend `CategorySnapshot` / `Category` / `CategoryRule` entities with
  `strategy` (default `LABEL`) and `scope` (nullable JSON column).
- Implement `CategoryBackedPolicyAdvisor implements IValidationPolicyAdvisor`,
  initially handling only `policyForCodedContent(...)`. Other methods fall
  through to default behaviour.
- Wire the advisor into `ValidationService`:
  ```java
  instanceValidator.setValidatorPolicyAdvisor(policyAdvisor);
  ```
- Add per-rule counter metrics: `validation.rule.skipped{rule_id="..."}`,
  `validation.rule.suppressed{rule_id="..."}`, `validation.rule.labeled{rule_id="..."}`.
- Pick **one** rule for migration — `unknown_code_system` is the safest
  candidate (acceptable=true, terminology, well-understood). Migrate it to
  `SKIP` with the Epic/Cerner/Oracle URL patterns as scope.
- Document the new strategy field in this file and in the JSON schema (if any).

**Acceptance criteria:**
- All existing tests pass.
- Smoke test still works — `unknown_code_system` matches behaviour-preservingly
  but the underlying terminology call is now skipped.
- Metrics show the rule firing as `skipped` instead of `labeled`.

### Phase 2 — Migrate terminology rules to SKIP (one PR per batch)

For each acceptable-true terminology rule, identify the scope it should cover
and promote it to `SKIP`. Suggested order (lowest risk first):

1. `unknown_code_system` (already done in Phase 1)
2. `unable_to_validate_code`
3. `unresolved_code_system`
4. `non_loinc_code_glucose_point_of_testing`
5. `incorrect_display_value_for_code`

Each migration is a data-only JSON change after Phase 1 ships.

### Phase 3 — Add policyForReference support and migrate (one PR)

- Extend `CategoryBackedPolicyAdvisor` with `policyForReference(...)`.
- Add `referencePaths` to the `scope` schema.
- Migrate the small set of reference-resolution rules to `SKIP`.

### Phase 4 — Add isSuppressMessageId support and migrate (one or two PRs)

- Extend `CategoryBackedPolicyAdvisor` with `isSuppressMessageId(path, messageId)`.
- Build a mapping from each rule's matcher to the corresponding HAPI message id
  (using `org.hl7.fhir.utilities.i18n.I18nConstants` as the authoritative list).
  This is one-time research per rule.
- Migrate rules where the mapping is clean and stable.

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

## Related code

| File | Role |
|---|---|
| `src/main/resources/categories.json` | The hand-curated rule set (50 rules today) |
| `src/main/java/.../services/CategorizationService.java` | Loads rules; runs `LABEL` matching post-validation |
| `src/main/java/.../entities/Category.java`, `CategoryRule.java`, `CategorySnapshot.java` | DB entities backing each rule and its versioned history |
| `src/main/java/.../matchers/Matcher.java`, `CompositeMatcher.java`, `RegexMatcher.java` | The matcher polymorphism |
| `src/main/java/.../entities/ResultField.java` | Enumerates which message fields a matcher can target |
| `src/main/java/.../services/ValidationService.java` | Where the `FhirInstanceValidator` lives — Phase 1 wires the advisor here |
| `src/main/java/.../services/ReadyForValidationConsumer.java` | Kafka consumer that drives the categorize → persist → publish pipeline |
