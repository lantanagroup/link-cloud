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
2. **Execution**: Measures are executed systematically against the acquired data for each patient, including multiple evaluations during **progressive querying** as described in [Progressive Querying](data_acquisition.md).
3. **Results**: Each measure produces results indicating compliance or performance, which can be consumed by reporting or downstream systems. These results are in the form of a MeasureReport resource specific to the individual patient that the measure was executed against.

### Role in Progressive Querying

Measure evaluation is performed multiple times for each patient during **progressive querying** to support an efficient and focused reporting pipeline:
- Determines whether the patient from the census meets the initial criteria for submission.
- Identifies what data should be submitted for the reporting scenario if the patient is relevant.
- Includes "FHIR Profile" assertion statements in the resulting data to support the validation service in determining which profiles to validate the data against.

### Integration

Measure evaluation is often part of a broader workflow:
- **Data Acquisition**: Data is collected and normalized to a standard format.
- **Measure Execution**: Evaluations are run against pre-configured measures as data becomes available.
- **Result Propagation**: Evaluated results are consumed by the report service.

This approach ensures consistent, reliable evaluation of healthcare quality measures, supporting improved care outcomes and regulatory adherence.

### Testing

The measure engine may be tested against arbitrary data using the $evaluate operation (which is custom-built for this purpose in the measure evaluation service) or using the `measureeval-cli.jar` that can be built separately from the service; see [measureeval/README.md](../../Java/measureeval/README.md) for more information.