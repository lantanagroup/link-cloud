# The `policyForElement` demo

A self-contained, runnable illustration of the lesson from
`scratch/policy-for-element-opportunity.md`: HAPI exposes more
pre-execution control than we're using, and the biggest unclaimed hook
is `policyForElement`.

The demo is a single `main()` class. It validates a small collection of
resources twice — once with no policy advisor, once with a three-rule
`DemoPolicyAdvisor` that skips cardinality checks on specific element
paths — and prints a side-by-side comparison of message counts and wall
time.

## Why this exists

Our production `CategoryBackedPolicyAdvisor` wires two HAPI hooks:
`policyForCodedContent` (SKIP strategy, terminology-scoped) and
`isSuppressMessageId` (SUPPRESS strategy, post-message drop). That
covered the motivating rules for Phases 1–4.

HAPI's `IValidationPolicyAdvisor` exposes eight pre-execution hooks in
total. This demo shows one of the six we haven't touched:
`policyForElement`, which returns an
`EnumSet<ElementValidationAction>` for each element visit — one of
`Cardinality`, `Invariants`, `Bindings`, `AdditionalBindings`,
`StatusCheck`. Whatever we exclude from that set, HAPI doesn't run.

The point of the demo is to show a dev team, in one command, that:

1. The hook works today with our current HAPI version, no upstream
   changes.
2. It composes cleanly with our existing advisor pattern — the wiring
   is ~90 lines, structurally identical to what
   `policyForCodedContent` does.
3. It has measurable impact on the specific message shapes our rule
   set catches with post-hoc regex today (`"minimum required = 1, but
   only found 0"` and similar).

## Running it

```bash
cd Java

mvn -pl validation exec:java \
  -Dexec.mainClass=com.lantanagroup.link.validation.demo.PolicyForElementDemo
```

Optional flags:

| Flag | Default | Purpose |
|------|---------|---------|
| `--bundle <path.json>` | (built-in fixture) | Validate the resources in the given FHIR bundle instead of the synthetic fixture. |
| `--iterations N` | `3` | Timed iterations. First is warmup and discarded. Must be ≥ 2. |
| `--verbose`, `-v` | off | Keep HAPI's INFO chatter (`Fetching CodeSystem ...`, snapshot generation notices). Off by default so the demo output stays legible. |

## Expected output

Against the built-in synthetic fixture:

```
Subjects: 5 resource(s) (synthetic fixture)
Running 3 iteration(s), first is warmup

┌────────────────────────────────┬──────────────┬──────────────┐
│                                │     Baseline │      Treated │
├────────────────────────────────┼──────────────┼──────────────┤
│ Total messages                 │            9 │            6 │
│ Best iteration (ms)            │           68 │           63 │
└────────────────────────────────┴──────────────┴──────────────┘

Delta: −3 messages (33.3%), −5 ms (7.4%)

Rule hits (advisor invocations that fired):
  demo_encounter_status_cardinality             1 element visit(s) affected
  demo_medicationrequest_intent_cardinality     1 element visit(s) affected
  demo_observation_code_cardinality             1 element visit(s) affected

Sample of treated-pass messages (first 5):
  ERROR  MedicationRequest.status: minimum required = 1, but only found 0
  ERROR  MedicationRequest.medication[x]: minimum required = 1, but only found 0
  ERROR  MedicationRequest.subject: minimum required = 1, but only found 0
  ERROR  Observation.status: minimum required = 1, but only found 0
  ERROR  DiagnosticReport.status: minimum required = 1, but only found 0

What to look at:
  * Messages that disappeared ...
  * Messages that stayed ...
  * DiagnosticReport gets zero suppressions ...
```

Exact numbers vary between runs. The **shape** of the output is what
matters: three messages disappear from the treated pass
(`Encounter.status`, `MedicationRequest.intent`, `Observation.code` —
the paths named by the three demo rules), everything else persists
(nothing else was named), and `DiagnosticReport` gets zero
suppressions because no rule mentions any `DiagnosticReport` path —
proof the advisor is targeted, not a blanket suppressor.

## What the code shows

Three files, under 300 lines total:

- **`demo/DemoPolicyAdvisor.java`** — the advisor itself.
  `policyForElement` iterates a `List<Rule>`, first-hit-wins, returns
  `EnumSet.complementOf(rule.excludeActions())`. Structurally identical
  to `CategoryBackedPolicyAdvisor.policyForCodedContent` in the
  validation-config branch.
- **`demo/PolicyForElementDemo.java`** — the runner. Builds the same
  support chain twice, attaches the advisor to one, times both, prints
  the comparison. The rule set is a three-item literal at
  `buildDemoRules()` — the whole "config" is 6 lines of Java.
- **`Java/validation/POLICY-FOR-ELEMENT-DEMO.md`** — this file.

Compare to the production shape sketched in
`scratch/policy-for-element-opportunity.md#what-extending-our-model-would-take`:
the difference between demo and production is (a) rules come from the
`Category` entity instead of a hardcoded list, (b) `excludeActions`
names live under `CategoryScope`, (c) startup demotes malformed rules
with a warning, (d) hit counts flow through `ValidationMetrics` instead
of a `Map<String, Integer>`. Same three responsibilities, more
production plumbing.

## What the demo intentionally does NOT show

- **Real-IG cardinality.** The synthetic fixture uses base-FHIR
  required fields (`Encounter.status`, `MedicationRequest.intent`,
  `Observation.code`) that fire without loading any StructureDefinition
  beyond HAPI's default R4 profiles. This keeps the demo one-command-runnable. Pointing at a
  real US Core or CQFmeasures bundle via `--bundle` will exercise the
  same hook against those profiles' cardinality declarations — no code
  change needed, but the deps need to be loadable (add
  `PrePopulatedValidationSupport` in `run()` if you want that flow).
- **Invariants axis.** The rules only exclude `Cardinality`. Try
  swapping in `ElementValidationAction.Invariants` on a rule targeting
  `Observation.component` against a US Core bundle to see the same
  pattern skip a FHIRPath rule (`us-core-2`).
- **Bindings axis.** Same shape — exclude `Bindings` at a
  ValueSet-bound element to short-circuit before terminology engages.
  This overlaps with what `policyForCodedContent` already does; the
  scratch doc's "which hook does what" section covers the tradeoff.
- **Path-shape validation.** The demo uses regex patterns on the
  `path` argument HAPI passes. In production we'd want to know what
  those paths look like when a slice discriminator is involved
  (`DiagnosticReport.category:LaboratorySlice` etc.) — this demo
  wouldn't tell you. That's open question #2 in the scratch doc.
- **Metrics.** Rule hits are counted in a plain `Map`. Production
  would attribute via `ValidationMetrics` / OpenTelemetry so dashboards
  can show per-rule skip volume over time. Not part of the illustrative
  wiring.

## Where to go from here

If the demo lands well and the team wants a production version, the
work is:

1. Add `elementPaths` and `excludeElementActions` (or a shared `paths`
   axis) to `CategoryScope`.
2. Wire `policyForElement` in `CategoryBackedPolicyAdvisor` matching
   the shape of `policyForCodedContent`.
3. Migrate the ~15 cardinality-shape rules named in
   `scratch/policy-for-element-opportunity.md`.
4. Run `ValidationCostAudit` pre/post to quantify the impact against
   NHSN measure bundles.

None of that touches HAPI, the DB schema
(`scope varchar(max)` already stores JSON), or the validator chain
composition — the demo's shape is what makes it into production, just
with wider rule input and observability plumbing added.

## Related

- `scratch/policy-for-element-opportunity.md` — the design analysis
  that motivates this demo. Reads better together with the demo code.
- `validation-config` branch — where `CategoryBackedPolicyAdvisor`,
  `CategoryStrategy`, and `CategoryScope` live in their Phase 1–4 form.
  The demo does not depend on that branch — it forks from `dev` — so
  the diff on this branch is scoped to just what `policyForElement`
  requires.
