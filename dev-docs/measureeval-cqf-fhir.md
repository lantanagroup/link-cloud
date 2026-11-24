# MeasureEval and clinical-reasoning (cqf-fhir)

MeasureEval does not evaluate CQL itself. It hands each measure bundle and a patient's data to the
clinical-reasoning library (`org.opencds.cqf.fhir`, often called cqf-fhir), which runs the CQL engine
and builds the MeasureReport. This page covers how that dependency is versioned, the one place
MeasureEval reaches past the library's public API, and what to check when upgrading it.

## Versions

| What | Set in | Notes |
| --- | --- | --- |
| clinical-reasoning | `cqf-fhir.version` in `Java/pom.xml` | imported as `cqf-fhir-bom` |
| CQL engine (`org.cqframework:engine`) | not set | comes in through `cqf-fhir-cr` |
| HAPI FHIR | `hapi-fhir.version` in `Java/pom.xml` | must be one the clinical-reasoning release supports |

The engine is deliberately not declared in `measureeval/pom.xml`, even though MeasureEval uses its
debug and trace types directly. The clinical-reasoning BOM does not manage the engine, so a
separate pin had to be bumped by hand alongside `cqf-fhir.version`, and a missed bump leaves an old
engine on the classpath beside a newer clinical-reasoning. Letting `cqf-fhir-cr` bring it in keeps
the two in step.

Only MeasureEval depends on clinical-reasoning; the parent POM's BOM import does not affect the
other modules.

## The debug-sections bridge

`$evaluate` with debug sections needs more than the MeasureReport: the populated `MeasureDef`
(groups, populations and errors) and each subject's CQL results (expressions, library debug,
traces). `R4MultiMeasureService.evaluateSingleMeasureCaptureDef` returns all three, but from 4.6.0
it is package-private and marked as test infrastructure (clinical-reasoning PR #979).

`LinkMeasureDefCapture` sits in MeasureEval's source tree under the library's own package,
`org.opencds.cqf.fhir.cr.measure.r4`, so it can call that method. MeasureEval runs on the classpath,
not the module path, so a class in another jar's package is allowed.

The alternative was rebuilding the service's internal steps (resolving subjects, choosing the
evaluation type, post-processing the report) from the public `R4MeasureProcessor` methods. That
copies upstream logic into MeasureEval and lets the debug report drift from the report the normal
path produces. The bridge keeps both paths on the same code.

If an upgrade renames or removes the method, MeasureEval stops compiling at the bridge; it cannot
fail silently. At that point, look for a public replacement first, then fall back to the
`R4MeasureProcessor` route above.

## Upgrading clinical-reasoning

1. Read the release notes between the current and target versions, especially for changes to
   `R4MultiMeasureService`, `MeasureReference` and `MeasureEnvironment`.
2. Bump `cqf-fhir.version`, and `hapi-fhir.version` if the release needs a newer HAPI FHIR.
3. Check what resolves, from `Java/`. There should be one version of each artifact.

   ```shell
   mvn -pl measureeval -am dependency:tree -Dincludes=org.cqframework,org.opencds.cqf.fhir,ca.uhn.hapi.fhir
   ```

4. Run the `measureeval` tests. `MeasureEvaluatorTimingRepeatTests` covers a defect fixed in 4.9.0
   (a MedicationRequest rebuilt down to `Timing.Repeat` with `dayOfWeek` or `when` failed the whole
   subject), on both the normal and debug paths. `MeasureEvaluatorDebugTests` covers the bridge.
5. A library upgrade can change results without raising an error. Before release, compare
   MeasureReports from a reference dataset for each measure in use, before and after.
