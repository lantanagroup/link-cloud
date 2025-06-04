# Artifact Management

This document outlines the current state of artifact upload, storage, and usage in both the **measureeval** (evaluation) service and the **validation** service.

---

## Measure Evaluation (measureeval) Service

### Upload & Storage
- Bundles are uploaded via: `PUT /api/measureDefinition/:id`
- The uploaded bundle is a FHIR JSON Bundle and typically includes: `Measure`, `Library`, `StructureDefinition` (Profile), `ValueSet`, `CodeSystem`
- Bundles are stored **as-is** in the measureeval service's database.

### Evaluation Execution
When a request is made to evaluate a measure:
1. The service checks if a `MeasureEvaluator` instance has already been compiled and cached.
2. If not, it:
   - Retrieves the corresponding bundle from the database.
   - Compiles a `MeasureEvaluator` instance.
   - Stores the compiled instance in memory/cache for future use.

**Terminology Handling:**
  - Currently, the bundle must include all required `ValueSet` and `CodeSystem` resources for evaluation to succeed.
  - There are future plans to integrate a FHIR Terminology (TX) Service, which would offload terminology expansion (e.g., `$expand`) and eliminate the need for local terminology resources in the bundle.

### Known Deficiencies
- **No version tracking**: Only a single bundle per `:id` is retained.
- **Overwrites are destructive**: Uploading a new bundle overwrites the existing one.
- **No version selection**: There is no support for evaluating a measure against a specific version of the bundle.

## Validation Service

### Upload & Storage
- Artifacts are uploaded individually using: `PUT /api/validation/artifact/:type/:name`
- `type` = "RESOURCE"
- `name` = FHIR resource `id`
- Each resource from the FHIR bundle (e.g., `StructureDefinition`, `ValueSet`, `Measure`, `Library`) is uploaded and stored independently.

### Validation Execution
- When validation is performed:
- The service loads **all artifacts** from the database into memory.
- It validates input data against all loaded profiles, value sets, and code systems.

### Known Deficiencies
- **Global scope**: All stored artifacts are always loaded; there is no filtering or scoping based on tenant or package.
- **No tenant-specific configuration**: There is no ability to configure validation behavior per tenant or per package version.