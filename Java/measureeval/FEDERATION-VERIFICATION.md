# Federation verification procedure

Manual verification procedure for the federated-terminology wire-up in
`MeasureEvaluator`. The automated tests
(`MeasureEvaluatorFederationTests`) protect against regressions with small
in-code fixtures; this procedure verifies the same invariants against
production-shaped IG bundles.

## What is being verified

Three invariants of the composition in
`MeasureEvaluator.buildRepository()`:

1. **Federation preserves results.** For a bundle that carries its own
   ValueSets, evaluating with federation on and off must produce the same
   MeasureReport. If they diverge, terminology is being resolved
   differently in the two paths — the wire-up has a bug.
2. **Federation consults the bundle first.** When the bundle carries the
   ValueSets it needs, no remote calls should be made. If the remote is
   consulted at all, the federation is routing wholesale rather than
   falling through, and IG-embedded ValueSet versions would be silently
   ignored.
3. **Federation falls through when the bundle is empty.** When ValueSets
   are stripped from the bundle, the remote must be consulted and the
   report must still match the reference result. Confirms the fallback
   half of the composition actually works.

## Prerequisites

- The `nhsn-measures` repository checked out and built. Bundles are read
  from its `bundles/` and example subject data from its `output/`
  directory.
- `mvn` on `PATH`. The verifier runs against the test-scope classpath, so
  no separate WireMock or NHSN-specific dependency setup is needed — the
  project pom already includes them for test scope.

## Invocation

```bash
cd Java

mvn -pl measureeval exec:java -Dexec.classpathScope=test \
  -Dexec.mainClass=com.lantanagroup.link.measureeval.audit.FederatedTerminologyVerifier \
  -Dexec.args="\
    --measure-bundle /path/to/nhsn-measures/bundles/measure/NHSNAcuteCareHospitalDailyInitialPopulation/NHSNAcuteCareHospitalDailyInitialPopulation-bundle.json \
    --subjects-dir /path/to/nhsn-measures/output \
    --period-start 2024-01-01 \
    --period-end 2024-12-31"
```

Flags:

| Flag | Required | Default | Purpose |
|------|----------|---------|---------|
| `--measure-bundle <path>` | yes | — | Path to the measure bundle (Measure + Library + ValueSets). |
| `--subjects-dir <dir>` | yes | — | Directory containing subject bundles. The verifier picks up JSON files whose name contains `subject`. |
| `--ts-port <port>` | no | 8089 | Port for the embedded WireMock terminology server. Change if 8089 is in use. |
| `--period-start <yyyy-mm-dd>` | no | 2024-01-01 | Measurement period start passed to CQL evaluation. |
| `--period-end <yyyy-mm-dd>` | no | 2024-12-31 | Measurement period end. |

## Expected output

```
Measure bundle: /path/to/.../NHSNAcuteCareHospitalDailyInitialPopulation-bundle.json
Subjects dir:   /path/to/nhsn-measures/output
Mock TS port:   8089
Period:         2024-01-01 .. 2024-12-31

Measure bundle carries 34 ValueSet(s); stripped variant contains 4 entries
Found 4 subject bundle(s):
  - Bundle-bundle-example-ach-daily-subject-influenzatherapeutic.json
  - Bundle-bundle-example-ach-daily-subject-initialpopulationpass.json
  - Bundle-bundle-example-ach-daily-subject-negativepcr.json
  - Bundle-bundle-example-ach-daily-subject-rsvlabbtg.json

Subject                                       | A    | B    | C    | Bcalls | Ccalls | Verdict
----------------------------------------------|------|------|------|--------|--------|--------
  Bundle-bundle-example-ach-daily-subject-... | 1    | 1    | 1    | 0      | 12     | ✓ PASS
  Bundle-bundle-example-ach-daily-subject-... | 1    | 1    | 1    | 0      | 12     | ✓ PASS
  Bundle-bundle-example-ach-daily-subject-... | 1    | 1    | 1    | 0      | 12     | ✓ PASS
  Bundle-bundle-example-ach-daily-subject-... | 0    | 0    | 0    | 0      | 12     | ✓ PASS

Overall: 4/4 PASS

Legend:
  A     = initial-population, no federation, full bundle
  B     = initial-population, federation on, full bundle (must equal A)
  C     = initial-population, federation on, stripped bundle (must equal A)
  Bcalls= mock TS requests during scenario B (must be 0)
  Ccalls= mock TS requests during scenario C (must be > 0)
```

## Interpreting the output

### PASS

All three columns (A, B, C) match; Bcalls is 0; Ccalls is greater than 0.
This means:

- The initial-population count is stable across the federated and
  non-federated paths for this measure and subject.
- The bundle-first behavior held: when the bundle had the ValueSets, the
  remote TS was never touched.
- The fallback behavior held: when the ValueSets were removed, the mock
  TS served them and the result came out the same.

### FAIL: results diverge

Columns A, B, C are not all equal. Someone in the chain is resolving
terminology differently in the federated path. Common causes:

- `Repositories.proxy(...)` invocation is wrong — check the arg order
  `(data, content, terminology)` in `MeasureEvaluator.buildRepository()`.
- A CQF upgrade changed the semantics of `FederatedRepository` or
  `ProxyRepository` and our composition no longer behaves as before.
  Re-read the CQF `Repositories.proxy` bytecode notes in
  `MeasureEvaluator.buildRepository()`'s Javadoc.

### FAIL: scenario B hit remote

Bcalls is not 0. Federation is not consulting the bundle first — every
terminology lookup is going straight to the remote TS, and any bundle-
embedded ValueSet version is being silently ignored. This is the failure
mode our composition was specifically designed to prevent, so a Bcalls
count above 0 always warrants investigation.

- Likeliest cause: `FederatedRepository` is being constructed with the
  arguments in the wrong order (remote first, bundle second). Check
  `buildRepository()`.

### FAIL: scenario C did not use remote

Ccalls is 0. Stripping the ValueSets from the bundle didn't force any
remote calls, which means either:

- The measure being evaluated doesn't actually reference the stripped
  ValueSets in its CQL — try a different measure.
- CQF isn't reaching the remote tier at all — the composition is broken.

## Cross-referencing against the shipped MeasureReport

Each of the NHSN example subject bundles also contains a pre-computed
`MeasureReport`. Reading the initial-population count from that reference
report and comparing to columns A / B / C is a stronger check than
comparing A / B / C against each other alone. If the verifier reports
PASS but the initial-population value differs from the shipped
MeasureReport, the numeric result is wrong regardless of federation
behavior — that's a measure-evaluation bug, not a federation bug, but
worth surfacing.

Reading the shipped MeasureReport by hand:

```bash
jq '.entry[] | select(.resource.resourceType=="MeasureReport")
     | .resource.group[0].population[0].count' \
   /path/to/nhsn-measures/output/Bundle-bundle-example-ach-daily-subject-initialpopulationpass.json
```

Should return an integer that agrees with column A above.

## When to re-run

- After any change to `MeasureEvaluator.buildRepository()`.
- After a `cqf-fhir` version bump — behavior of `FederatedRepository`,
  `ProxyRepository`, or `Repositories.proxy(...)` could shift.
- Before shipping any change that touches `LinkConfig.fhirTerminologyServiceUrl`,
  the `remoteTerminologyClient` bean, or the `MeasureDefinitionController`
  wire-up.

## Related

- `Java/measureeval/src/test/.../MeasureEvaluatorFederationTests.java`
  — the CI-friendly regression tests. Same invariants, smaller fixtures.
- `Java/measureeval/src/main/.../services/MeasureEvaluator.java` —
  `buildRepository()` and its Javadoc explain the composition choice.
- `Java/validation/src/main/.../providers/REMOTE-TERM-COST-ANALYSIS.md`
  — cost profile and open items for the validation service's remote
  terminology client.
