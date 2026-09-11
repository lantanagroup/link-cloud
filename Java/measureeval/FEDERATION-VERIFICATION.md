# Federation verification procedure

Manual verification procedure for the federated-terminology wire-up in
`MeasureEvaluator`. The automated tests
(`MeasureEvaluatorFederationTests`) protect against regressions with small
in-code fixtures; this procedure verifies the same invariants against
production-shaped IG bundles.

## What is being verified

Three invariants of the composition in
`MeasureEvaluator.buildRepository()` under **TS-authoritative** semantics:

1. **The remote TS is consulted for every terminology lookup when
   configured.** When a remote terminology client is present, every
   `ValueSet` and `CodeSystem` lookup must go to the remote — the bundle
   is not consulted for terminology types even when it carries embedded
   copies. If the remote is not consulted, the bundle is silently
   winning and the operator's chosen terminology service is not actually
   in effect.
2. **Results are stable across the two paths when the TS serves the
   same terminology the bundle would.** The no-TS path (bundle-only)
   and the TS-configured path (remote-only for terminology) must produce
   the same MeasureReport when the remote is stubbed with the same
   ValueSet content the bundle carries. If they diverge, terminology is
   being resolved differently in the two paths — the wire-up has a bug.
3. **The TS supplies terminology the bundle lacks.** When ValueSets are
   stripped from the bundle, the remote must be consulted and the report
   must still match the reference result. Confirms the terminology tier
   is actually reaching the remote (not silently short-circuiting to an
   empty local answer).

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
  Bundle-bundle-example-ach-daily-subject-... | 1    | 1    | 1    | 12     | 12     | ✓ PASS
  Bundle-bundle-example-ach-daily-subject-... | 1    | 1    | 1    | 12     | 12     | ✓ PASS
  Bundle-bundle-example-ach-daily-subject-... | 1    | 1    | 1    | 12     | 12     | ✓ PASS
  Bundle-bundle-example-ach-daily-subject-... | 0    | 0    | 0    | 12     | 12     | ✓ PASS

Overall: 4/4 PASS

Legend:
  A     = initial-population, no TS, full bundle
  B     = initial-population, TS on, full bundle (must equal A)
  C     = initial-population, TS on, stripped bundle (must equal A)
  Bcalls= mock TS requests during scenario B (must be > 0 — TS is authoritative)
  Ccalls= mock TS requests during scenario C (must be > 0)
```

## Interpreting the output

### PASS

All three columns (A, B, C) match; Bcalls and Ccalls are both greater
than 0. This means:

- The initial-population count is stable across the no-TS and
  TS-authoritative paths for this measure and subject.
- The TS-authoritative invariant held: when the TS was configured, the
  remote was consulted on every terminology lookup — even in scenario B,
  where the bundle also had the ValueSets.
- The stripped-bundle path held: with the bundle's ValueSets removed,
  the remote continued to serve them and the result came out the same.

### FAIL: results diverge

Columns A, B, C are not all equal. Someone in the chain is resolving
terminology differently in the TS-authoritative path. Common causes:

- The mock TS is returning different content than the bundle for the
  same VS URL — check `stubValueSets(...)` in the verifier.
- A CQF upgrade changed the semantics of the search-by-URL path so
  results are being merged or transformed differently. Re-read the
  `FederatedFhirRepository` and `MeasureEvaluator.buildRepository()`
  Javadocs.

### FAIL: scenario B did not consult TS

Bcalls is 0. When the bundle carries the ValueSets, the TS should still
be authoritative — the remote must be consulted. Bcalls at 0 means the
bundle is winning silently, so an operator's choice of terminology
server would be ignored in production for any measure whose bundle
embeds its ValueSets.

- Likeliest cause: `FederatedFhirRepository` regressed to bundle-first
  or bundle-with-fallback semantics. Check its `read` / `search`
  overrides — for terminology types they must delegate straight to the
  remote without consulting `super`.

### FAIL: scenario C did not consult TS

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
MeasureReport, the numeric result is wrong regardless of terminology
routing — that's a measure-evaluation bug, not a routing bug, but
worth surfacing.

Reading the shipped MeasureReport by hand:

```bash
jq '.entry[] | select(.resource.resourceType=="MeasureReport")
     | .resource.group[0].population[0].count' \
   /path/to/nhsn-measures/output/Bundle-bundle-example-ach-daily-subject-initialpopulationpass.json
```

Should return an integer that agrees with column A above.

## When to re-run

- After any change to `MeasureEvaluator.buildRepository()` or
  `FederatedFhirRepository`.
- After a `cqf-fhir` version bump — behavior of `RestRepository` or the
  underlying HAPI client could shift.
- Before shipping any change that touches `LinkConfig.fhirTerminologyServiceUrl`,
  the `remoteTerminologyClient` bean, or the `MeasureDefinitionController`
  wire-up.

## Related

- `Java/measureeval/src/test/.../MeasureEvaluatorFederationTests.java`
  — the CI-friendly regression tests. Same invariants, smaller fixtures.
- `Java/measureeval/src/main/.../services/MeasureEvaluator.java` —
  `buildRepository()` and its Javadoc explain the routing choice.
- `Java/measureeval/src/main/.../repositories/FederatedFhirRepository.java`
  — the routing itself and the "TS-authoritative" semantics rationale.
- `Java/validation/src/main/.../providers/REMOTE-TERM-COST-ANALYSIS.md`
  — cost profile and open items for the validation service's remote
  terminology client.
