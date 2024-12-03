## Overview

**Measure Evaluation** is a critical process in assessing clinical data against FHIR digital quality measures. It ensures that healthcare data is analyzed consistently and accurately using standardized logic and definitions.

### Key Concepts

- **FHIR Digital Quality Measures**: Defined standards that outline how clinical data is measured for quality reporting and compliance.
- **Measure Package**: A comprehensive bundle (in FHIR JSON Bundle format) required for evaluation, including:
    - Measure definitions.
    - CQL logic in FHIR Library resources.
    - Terminology, such as pre-expanded value sets and optimized code systems.

### Evaluation Process

1. **Pre-preparation**:
    - Data is collected and normalized to align with FHIR standards.
    - Measure packages are prepared, containing all artifacts necessary for evaluation.
2. **Execution**: Measures are executed systematically against the acquired data for each patient.
3. **Results**: Each measure produces results indicating compliance or performance, which can be consumed by reporting or downstream systems. These results are in the form of a MeasureReport resource that is specific to the individual patient that the measure was executed against.

### Integration

Measure evaluation is often part of a broader workflow:
- **Data Acquisition**: Data is collected and normalized to a standard format.
- **Measure Execution**: Evaluations are run against pre-configured measures as data becomes available.
- **Result Propagation**: Evaluated results are consumed by the report service.

This approach ensures consistent, reliable evaluation of healthcare quality measures, supporting improved care outcomes and regulatory adherence.

### Testing

The measure engine may be tested against arbitrary data using the $evaluate operation that is custom-built for this purpose in the measure evaluation service.