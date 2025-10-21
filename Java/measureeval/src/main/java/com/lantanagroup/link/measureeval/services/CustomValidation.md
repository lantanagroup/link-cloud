## Custom Validation & Data Transparency

This module supports two modes of operation — **evaluation‑first** (default) and **validation‑first**. In evaluation‑first mode, we first run measure evaluation on the *provided data* to determine whether the patient meets the minimal criteria (Initial Population). Only if the Initial Population is met do we proceed to profile validation and conformance gating. Throughout either mode, the system provides a precise, auditable view of what was requested, what was received, what was kept, and why.

### Objectives

- Data transparency: Clearly report how data is gathered, validated, trimmed, and forwarded.
- Evaluation‑first minimal criteria: Run the provided data through measure evaluation to test Initial Population (IPP) before profile validation.
- Profile-aware retrieval: Prefer "summary" payloads aligned to the measure's profiles (or simulate summaries locally).
- Custom validation: Validate against measure-specific profiles (e.g., US Core, QI-Core, IG-specific) and record findings.
- Conformance gate (configurable): After evaluation, optionally enforce profile conformance for forwarding/tagging; can be relaxed or set to record‑only.
- Non-conformance reporting: Persist or emit OperationOutcome for anything excluded.
- ModelInfo generation (roadmap): Generate a CQL data model from the active profile set to improve authoring and evaluation

### High-Level Architecture

flowchart LR
  A[Provided Data / Retrieved Bundle] --> B{Measure Evaluation}
  B -->|IPP not met| I[Transparency Report Bundle]
  B -->|IPP met| C[Optional Local Projection]
  C --> D[Profile Validation]
  D -->|pass| E[Conformance Gate (optional)]
  D -->|fail| G[OperationOutcome]
  E --> H[Forward to Downstream (evaluator/reporting)]
  C --> I[Transparency Report Bundle]
  D --> I
  G --> I

### Workflow

1. Evaluate minimal criteria (Initial Population)
   - Run measure evaluation on the provided data bundle (or retrieved data) to determine if the patient meets IPP.
   - If IPP is **not** met, stop here and emit a transparency report (no validation required).
2. Optional local projection (summary simulation)
   - If desired, trim resources *post‑evaluation* to mustSupport ∪ required (min>0) ∪ ancestors for the target profile(s) to stabilize validation.
3. Custom profile validation (post‑evaluation)
   - Validate each relevant resource against configured profiles using HAPI/HL7 validators.
   - Capture OperationOutcome.issue[*] with severity, diagnostic, code, and expression (FHIRPath).
4. Conformance gate (optional)
   - If all required profiles pass (per configured severity), tag the resource with meta.profile += <canonical|version> and mark as conformant.
   - Otherwise, either exclude from downstream forwarding **or** include with “record‑only” mode while emitting OperationOutcome.
5. Transparency output
   - Produce a Bundle (collection) containing:
     - A summary Parameters or lightweight JSON of evaluation/validation stats (including whether IPP was met).
     - One OperationOutcome per excluded/non‑conformant resource (may be contained).
     - Optionally, include the trimmed resources actually forwarded.
6. (Optional) Retrieval
   - If you are not supplied a bundle, you may still derive data requirements and perform retrieval; however, in evaluation‑first mode retrieval is not required before the initial IPP check.

Configuration Example:

```yaml
mode:
  strategy: evaluation-first          # evaluation-first (default) | validation-first
  initialPopulationGroup: "Initial Population"
  ipFailShortCircuit: true            # stop after IPP=false and emit transparency
validator:
  failOnSeverity: error               # or: warning, information (threshold for exclusion)
  tagConformant: true                 # add meta.profile when a profile passes
  recordOnlyOnFailure: false          # if true, include non-conformant with OO instead of excluding
  implementationguides:
    USCORE:
      url: http://hl7.org/fhir/us/core/STU3.1.1/package.tgz
    NHSNMeasures:
      url: http://www.cdc.gov/nhsn/fhirportal/dqm/ig/STU1.0.0/package.tgz
retrieval:
  enabled: false                      # not required for evaluation-first if a bundle is provided
  preferServerSummary: false          # Epic often lacks _elements/_summary; do local projection
  pageSize: 200
  parallelism: 8
projection:
  when: post-evaluation               # pre-validation | post-evaluation
  strategy: mustsupport-plus-required
  keepAlways:
    - "*.id"
    - "*.meta"
    - "*.meta.profile"
reporting:
  includeTrimmedResources: true
  includeOperationOutcomes: true
```

### Data Gathering

_Note: In **evaluation‑first** mode, data gathering is optional if the caller supplies a bundle. Use this section only when you must retrieve additional data; otherwise proceed directly to evaluation._

Challenges & strategies
- Minimal data set: Start from $data-requirements (or other data requirements processing); constrain by profile + key elements to bound the search scope.
- Remote capabilities differ: Don't assume _elements/_summary/chained search. Fall back gracefully to full resources and local projection.
- Identify relevant data in large sets: Use value sets, codes, and date windows tied to the evaluation period. Keep a per-type include/exclude filter (e.g., only lab Observations used by the measure).

Good patterns
- Cache NPM packages and ValueSet expansion.
- Log request response metrics (count, ms) by query signature and surface timeouts.

### Custom Profile Validation
- Validate each resource **after the IPP check** against one or more configured profiles using HAPI/HL7 validators.
- The configured profiles are typically determined from the Measure’s data requirements and active IGs; in evaluation‑first, this step is deferred until IPP is met.
- Success criteria: no issue severity higher or equal to failOnSeverity.
- On success ensure the resource declares the canonical in meta.profile (add if missing).

### Reporting

Return one OperationOutcome per resource (or a consolidated one), using:
- issue.severity: information|warning|error|fatal
- issue.code: validator's code (e.g., invalid, structure, processing)
- issue.diagnostic: human-readable explanation of issue
- issue.expression: FHIRPath to the offending element (e.g., Observation.code.coding[0].system)

### Data Conformance Gate
- Include resources that meet profile criteria (when the gate is enabled).
- Exclude resources that exceed the severity threshold, or switch to **record‑only** mode to forward with OperationOutcome.
  - Perhaps these resources will be included with the contained validation messages?
- Tag conformant resources: resource.meta.profile should include the exact canonical (optionally with |version).
- Containment & references: Keep referenced contained resources if the referencing element remains after projection. Consider pruning dangling references and reporting them.

### Local Projection (Summary Simulation)

When servers don't support _elements/_summary — or when stabilizing validation *after* IPP evaluation — project locally:
- Keep mustSupport elements from the relevant StructureDefinition(s).
- Also keep elements with min > 0 (required by the base/profile), plus their ancestors.
- Normalize choice paths (valueQuantity → value[x]) when comparing to snapshot paths.
- Be cautious with slices: if mustSupport is slice-specific (e.g., category:lab), you need the discriminator to decide which array entries to keep.
