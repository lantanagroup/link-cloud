# Validation cost audit

Point-and-shoot tool for auditing the validation cost of a FHIR IG. Given an IG
package (`.tgz`), a directory of dependency packages, and a directory of sample
bundles, it drives validation of every resource in every bundle against every
declared profile in isolation, times each call with `System.nanoTime()`, and
emits a JSON report ranked by total cost. A message rollup surfaces the
concrete validation messages the IG is producing so an IG author has a concrete
list to work from, not just "profile X is slow."

The tool is deliberately internal: a standalone `main()` class inside the
`validation` module, not a productionized service. Not on any Kafka topic, not
exposed via HTTP, not deployed anywhere. Point it, run it, read the report,
delete the report — that's the workflow.

## Running it

```bash
mvn -pl validation exec:java \
  -Dexec.mainClass=com.lantanagroup.link.validation.audit.ValidationCostAudit \
  -Dexec.args="--ig /path/to/package.tgz \
               --deps /path/to/deps-dir \
               --bundles /path/to/bundles-dir \
               --iterations 3 \
               --report /path/to/report.json"
```

CLI flags:

| Flag | Required | Default | Purpose |
|------|----------|---------|---------|
| `--ig <path>` | yes | — | Primary IG package (`.tgz`) to audit. |
| `--bundles <path>` | yes | — | Directory containing sample bundle JSON files. Discovery finds any `*-bundle.json` file not under a `*-files/` subdirectory. |
| `--deps <path>` | no | — | Directory of dependency IG `.tgz` files to load into the validator's terminology chain alongside the primary IG. If omitted, only the primary IG plus HAPI's built-in R4 profiles are available. |
| `--iterations N` | no | `3` | Total iterations per (resource, profile) pair. First is warmup and discarded; remaining `N-1` are recorded. Must be ≥ 2. |
| `--report <path>` | no | `validation-cost-report.json` | Output path for JSON report. |
| `--top-messages N` | no | `20` | Number of top-N validation messages to include in the rollup. |
| `--verbose`, `-v` | no | off | Keep HAPI's INFO-level chatter (`Fetching CodeSystem...`, `Loading structure definitions...`). Default silences these; WARN/ERROR still get through. |

## Inputs

**Primary IG.** Any FHIR IG NPM package. HAPI's `NpmPackage.fromPackage(...)`
loads it; `CodeSystem`, `ValueSet`, and `StructureDefinition` resources are
extracted and added to the validator's terminology chain.

**Dependency IGs.** Every `.tgz` file in `--deps` is loaded the same way. Deps
matter: an IG that depends on US Core, QICore, CQFmeasures, etc. won't validate
correctly against a bare HAPI R4 baseline. `Java/validation/src/main/resources/artifacts/packages/`
already carries the IGs the production `validation` service ships with, so
pointing `--deps` at that directory covers most Link measure IGs.

**Sample bundles.** Any FHIR JSON Bundle. Discovery walks the directory
recursively, picks up `*-bundle.json` files, and skips anything under a
`*-files/` subdirectory (the convention Lantana measure builds use for
supporting-artifact bundles that would otherwise inflate the run).

## How isolation works

HAPI's `validateWithResult(resource, options)` validates a resource against
both the profiles declared in `resource.meta.profile` and any profiles added
via `ValidationOptions.addProfile(url)`. To attribute cost to a single profile,
the tool:

1. Copies the resource (deep copy via HAPI's `Resource.copy()`).
2. Clears `meta.profile` on the copy.
3. Sets `meta.profile` back to just the one profile under test (also adds via
   `ValidationOptions` so both codepaths agree).
4. Times the validation call.

For a resource that declares no profiles at all, the tool records a `(base)`
bucket representing the cost of validating against the base FHIR resource type.

## Terminology chain

The audit uses in-memory terminology only:

- `DefaultProfileValidationSupport` — HAPI's built-in R4 profiles.
- `PrePopulatedValidationSupport` — loaded from the primary IG + all dep IGs.
- `CommonCodeSystemsTerminologyService` — common non-terminology-server code
  systems.
- `InMemoryTerminologyServerValidationSupport` — in-memory ValueSet expansion.
- `SnapshotGeneratingValidationSupport` — for profiles that need snapshots
  generated on the fly.
- Wrapped in `CachingValidationSupport`.

**No remote terminology service.** Deliberate: hitting a network TS would
measure network latency, not IG cost, and production's remote-TS response
caching already flattens that in the real service. The audit is about slice
discrimination and FHIRPath invariants — the things that dominate CPU once
terminology is cached.

## Output

### Executive summary (stdout)

Grand total plus top-10 tables for each of: profiles by total time, resource
types by total time, validation messages by count.

### JSON report

```
{
  "ig":              "path/to/package.tgz",
  "iterations":      3,
  "warmupDropped":   1,

  "bundles": [ {
    "file":         "path/to/bundle.json",
    "entryCount":   38,
    "sampleCount":  142,
    "totalMs":      3278.4,
    "samples": [ {
      "resourceType":  "Measure",
      "resourceId":    "NHSNGlycemicControlHypoglycemicInitialPopulation",
      "profile":       "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cohort-measure-cqfm",
      "elapsedMs":     240.1,
      "messageCount":  16,
      "messages": [ { "severity": "ERROR", "location": "Measure.library[0]",
                      "message": "Unknown code 'http://hl7.org/fhir/expression-language#text/cql-identifier'" }, ... ]
    }, ... ]
  }, ... ],

  "byProfile": [ {
    "profile":       "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cohort-measure-cqfm",
    "resourceTypes": ["Measure"],
    "sampleCount":   4,
    "totalMs":       766.0,
    "meanMs":        191.5,
    "medianMs":      197.4,
    "p95Ms":         266.5,
    "maxMs":         266.5,
    "totalMessages": 64
  }, ... ],

  "byResourceType": [ { "resourceType": "Measure", ... }, ... ],

  "topMessages": [ {
    "count":            90,
    "severity":         "ERROR",
    "pattern":          "Unknown code *",
    "exampleMessage":   "Unknown code 'http://hl7.org/fhir/expression-language#text/cql-identifier'",
    "exampleLocations": ["Measure.library[0]", ... ],
    "exampleResources": ["Measure/NHSNGlycemicControl...", ...]
  }, ... ]
}
```

### Interpreting the report

- **`byProfile[].totalMs`** is the primary ranking. A profile at the top of
  this list either fires often (high `sampleCount`) or is expensive per call
  (high `meanMs`) — check both dimensions before drawing conclusions.
- **`byResourceType[].totalMs`** rolls the same samples up by top-level FHIR
  type. Useful when the same profile applies to multiple types, or the profile
  URL is a red herring and the real cost driver is "Measure resources are
  intrinsically expensive."
- **`topMessages`** is where actionability comes from. If a profile's total
  cost is high AND it's producing thousands of `dom-6 narrative` warnings, a
  chunk of that cost is HAPI reporting the failures — fix the underlying data
  (or accept those warnings) and per-call cost drops.
- **`messageCount`** on each sample tells you whether a slow validation was
  slow because of intrinsic complexity or because it fired many errors.

Message pattern normalization collapses volatile substrings (`'...'` quoted
strings, UUIDs, `[N]` indexes, bare numbers) so that "Unknown code 'X'" and
"Unknown code 'Y'" bucket together. Without this the top-N list gets diluted
by long-tail single-hit variants.

## What it does not measure

- **Per-invariant / per-constraint timing.** HAPI doesn't natively expose the
  cost of running an individual FHIRPath invariant like `us-core-2` or the
  cost of a specific slice discriminator. If a profile ranks high in the
  report and the messages don't obviously explain it, the follow-up is
  instrumenting the FHIRPath engine — deliberately out of scope here.
- **Cold-start vs. steady-state.** The first iteration per (resource, profile)
  pair is discarded as warmup. Recorded numbers are warm-cache — the state
  production reaches after a few seconds of traffic.
- **Bundle-level structural checks.** Rules like `bdl-3` fire when you
  validate a Bundle as a whole; the audit validates entries individually.
  This is a deliberate trade-off — per-entry isolation is required to
  attribute cost per profile.
- **Reference resolution.** References between entries won't necessarily
  resolve in isolation. Resulting "reference not found" messages inflate
  `messageCount` and may add validation time. This is visible in the report
  and can be filtered out when interpreting.

## Example: NHSN measures audit

Against `gov.cdc.nhsn.measures.r4@2.0.0-cibuild`, two acute-care measure
bundles (38 + 9 entries), 3 iterations, 166 samples total, ~4 seconds of
wall time.

**Top profiles by total time:**

| Total | Samples | Mean | Profile |
|------:|--------:|-----:|---------|
| 1005.7 ms | 64 | 15.7 ms | `hl7.fhir/StructureDefinition/shareablevalueset` |
| 984.7 ms | 64 | 15.4 ms | `hl7.fhir/uv/crmi/StructureDefinition/crmi-publishablevalueset` |
| 766.0 ms | 4 | 191.5 ms | `hl7.fhir/us/cqfmeasures/StructureDefinition/cohort-measure-cqfm` |
| 673.3 ms | 4 | 168.3 ms | `hl7.fhir/us/cqfmeasures/StructureDefinition/computable-measure-cqfm` |

**Top messages by count (excerpt):**

| Count | Severity | Message |
|------:|---------|---------|
| 90 | ERROR | `Unknown code 'http://hl7.org/fhir/expression-language#text/cql-identifier'` |
| 78 | WARNING | `Constraint failed: dom-6: 'A resource should have narrative...'` |
| 66 | ERROR | `Extension 'valueset-effectiveDate' definition allows for types [dateTime] but found type ...` |
| 32 | ERROR | `ValueSet.description: minimum required = 1, but only found 0` |
| 32 | ERROR | `ValueSet.experimental: minimum required = 1, but only found 0` |

**Reading the numbers.** Measure resources are the per-call hotspot (170-190
ms mean). ValueSets dominate by volume (146 samples, 15 ms mean, 2.3 s total).
Most of the ValueSet cost is HAPI reporting the same three or four missing
required fields on `shareablevalueset` — fixing those in the IG likely drops
per-ValueSet mean by a noticeable fraction. The `cohort-measure-cqfm` per-call
cost (191 ms mean, 267 ms p95) doesn't obviously trace to the top messages,
which points at slice discrimination or FHIRPath invariants as the next
investigation.

## Related code

| Path | Purpose |
|------|---------|
| `Java/validation/src/main/java/com/lantanagroup/link/validation/audit/ValidationCostAudit.java` | Main class — validator setup, audit loop, aggregation, report emission. |
| `Java/validation/src/main/resources/artifacts/packages/` | Bundled dependency IGs; useful `--deps` target for any NHSN- or DEQM-adjacent audit. |
| `Java/validation/src/main/java/com/lantanagroup/link/validation/services/ArtifactValidationSupport.java` | Production's NPM-package loader. The audit tool mirrors its pattern (`NpmPackage.fromPackage(...)`) rather than reusing it directly, so the audit doesn't drag in Spring/JPA. |
