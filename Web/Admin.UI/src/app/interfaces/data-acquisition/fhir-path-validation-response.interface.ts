// Mirrors the MeasureEval FhirPathValidationRequest/Response models
// (Java/measureeval .../models/FhirPathValidation{Request,Response}.java), reached through the
// BFF reverse proxy at POST /api/measureeval/fhir-path/$validate.

export interface IFhirPathValidationRequest {
  // The FHIR resource type to which expression applies
  resourceType: string;
  fhirPath: string;
}

export interface IFhirPathValidationResponse {
  // False when the expression has syntax errors or references elements/types that do not exist on the resource type.
  valid: boolean;
  returnType?: string;
  errors: string[];
  warnings: string[];
}
