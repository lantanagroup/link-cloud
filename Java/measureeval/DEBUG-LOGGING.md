# Measure Evaluation Debug Output

The `$evaluate` endpoint on the Measure Definition API can return structured debug data
alongside the standard FHIR `MeasureReport`. This is intended for measure authoring and
troubleshooting — not for production monitoring or analytics.

## Endpoint

```
POST /api/measureeval/measure-definition/{id}/$evaluate?debug={sections}
```

Request body: the same FHIR `Parameters` resource as a non-debug evaluation
(`periodStart`, `periodEnd`, `subject`, `additionalData`).

## The `debug` query parameter

`debug` is a comma-separated list of sections to populate. Spring binds the value through
a custom converter so the controller receives a `Set<DebugSections>` directly.

| Value                                 | Meaning                          |
|---------------------------------------|----------------------------------|
| _(parameter omitted)_                 | No debug data (fast path)        |
| `false` or empty                      | No debug data (fast path)        |
| `true` or `all`                       | Every section                    |
| `groups`                              | Population counts + errors       |
| `expressions`                         | CQL expression → result map      |
| `librarydebug`                        | Per-library node-level results   |
| `messages`                            | CQL engine messages              |
| `traces`                              | Hierarchical execution trace     |
| `debuglog`                            | Human-readable rendered log      |
| `expressions,traces` (any combination)| Just those sections              |

Token matching is case-insensitive and tolerates surrounding whitespace.
Unknown tokens are silently ignored; the recognised ones around them still parse.

## Response shape

Without debug:

```json
{
  "measureReport": { "resourceType": "MeasureReport", ... }
}
```

With debug:

```json
{
  "measureReport": { "resourceType": "MeasureReport", ... },
  "debugInfo": {
    "groups": [
      {
        "id": "group-1",
        "populations": [
          { "type": "initial-population", "count": 1, "subjects": ["Patient/p1"] }
        ]
      }
    ],
    "expressionResults": { "Initial Population": "[Encounter/enc-1]" },
    "traces": [ ... ],
    "truncated": true
  }
}
```

Sections that weren't requested (or that the engine didn't produce data for) are omitted
from `debugInfo` rather than emitted as `null`. The `debugInfo` wrapper itself is omitted
when no sections are requested.

The `truncated` flag is present and set to `true` only if a section had to be capped to
protect the response from runaway memory use. Currently this only happens when the trace
tree exceeds `MeasureEvaluator.MAX_TRACE_FRAMES` (10,000 frames).

## Wire-format change notice

Before this change, `$evaluate` returned the FHIR `MeasureReport` directly as the response
body. It now returns a wrapper object — clients that previously deserialized the response
as a `MeasureReport` need to read `response.measureReport` instead.

## Performance considerations

- **Without debug**: identical to the pre-change behaviour. Uses
  `R4MultiMeasureService.evaluate(...)` and returns immediately.
- **With debug**: routes through `R4MultiMeasureService.evaluateSingleMeasureCaptureDef(...)`
  to capture the underlying `EvaluationResult` map. There is some overhead in this path even
  when only the `groups` section is requested.
- **With `traces` or `all`**: the CQL engine's tracing flags are turned on, which adds
  per-expression instrumentation cost. On a realistic measure, this can multiply evaluation
  time several-fold and produce response payloads in the MB range. Use only against
  representative test inputs, not full production patient bundles.
- The 10,000-frame trace cap (`MeasureEvaluator.MAX_TRACE_FRAMES`) is a soft floor against
  pathological cases. If you hit it routinely on legitimate measures, raise the constant or
  request only a subset of debug sections.

## Examples

```bash
# Fast path (production behaviour)
curl -X POST -H "Authorization: Bearer ..." \
  -H "Content-Type: application/json" \
  -d @parameters.json \
  https://.../api/measureeval/measure-definition/my-measure/\$evaluate

# Just see population counts plus the report
curl -X POST -H "Authorization: Bearer ..." \
  -H "Content-Type: application/json" \
  -d @parameters.json \
  "https://.../api/measureeval/measure-definition/my-measure/\$evaluate?debug=groups"

# Everything (use sparingly)
curl -X POST -H "Authorization: Bearer ..." \
  -H "Content-Type: application/json" \
  -d @parameters.json \
  "https://.../api/measureeval/measure-definition/my-measure/\$evaluate?debug=all"

# Targeted: expression results plus a human-readable log
curl -X POST -H "Authorization: Bearer ..." \
  -H "Content-Type: application/json" \
  -d @parameters.json \
  "https://.../api/measureeval/measure-definition/my-measure/\$evaluate?debug=expressions,debuglog"
```

## Observability

When `debug` is non-empty, the controller emits an INFO log line at the start of the
request identifying the measure id and the requested sections:

```
Measure evaluation requested with debug sections [EXPRESSIONS, TRACES] for measure my-measure
```

This is silent on the fast path so production logs aren't polluted.

## Caching note

The `MeasureEvaluatorCache` keeps compiled evaluators keyed by measure id, but the
controller intentionally bypasses the cached evaluator's `evaluate(...)` method on the
debug path. The cached evaluator was compiled with the service-wide `link.cql-debug` flag
baked in; the request-specific `debug` parameter may differ. So the controller looks the
measure up via the cache (for the side effect of registering the libraries with the
`LibraryResolver`, used by `CqlLogAppender`), then calls
`MeasureEvaluator.compileAndEvaluate(...)` which produces a per-request evaluator with the
correct engine flags. This costs an extra compile per request — acceptable because debug
is opt-in and rare.

## Related code

| File                                              | Role                                                       |
|---------------------------------------------------|------------------------------------------------------------|
| `models/DebugSections.java`                       | Enum of available sections + `parse(String)`               |
| `models/MeasureEvaluationResult.java`             | Response DTO (`measureReport` + optional `debugInfo`)      |
| `converters/DebugSectionsConverter.java`          | Spring MVC `String` → `Set<DebugSections>` converter       |
| `configs/WebMvcConfig.java`                       | Registers the converter with the formatter registry        |
| `controllers/MeasureDefinitionController.java`    | `$evaluate` endpoint                                       |
| `services/MeasureEvaluator.java`                  | Compile + evaluate + `buildDebugInfo` + `buildTraceTree`   |
