[← Back Home](../README.md)

# Normalization Functionality

## Overview

The **Normalization** functionality is a critical step in preparing FHIR resources for further processing, such as measure evaluation or submission. It ensures that data adheres to expected formats, codes, and structures, enabling consistent and accurate downstream workflows.

Normalization is performed by the **Normalization** service, detailed in the [Normalization Service Specification](../service_specs/normalization.md).

## Key Features

- Normalization is applied to **each FHIR resource** acquired from the EHR.
- The process is executed **immediately after data acquisition**.
- **Tenant-specific configuration** ensures normalization aligns with each tenant's requirements.
- When a property is normalized, an **extension is added** to the FHIR resource containing the original value for reference.

## Supported Operations

Normalization is supported by the following configurable operations:

### 1. Concept Map

- **Description**: Converts codes from one system to another using a configurable list of mappings.
- **Details**:
    - Matches a "source code/system" pair.
    - Converts it to the specified "target code/system."
- **Use Case**: Standardizing codes between local EHR systems and external reporting requirements.

---

### 2. Fix Resource ID

- **Description**: Corrects resource IDs that are improperly formatted, such as IDs exceeding 64 characters.
- **Details**:
    - Replaces the invalid ID with a **hash** of the original ID.
    - Updates any **references** to the corrected ID to ensure integrity.
- **Use Case**: Complying with FHIR standards and maintaining reference consistency.

---

### 3. Copy Elements

- **Description**: Copies values between properties in a FHIR resource using FHIRPath expressions.
- **Details**:
    - Identifies source and destination properties using FHIRPath.
    - Copies the value from the source to the destination.
- **Use Case**: Populating properties required for measure evaluation or reporting.

---

### 4. Conditional Transform

- **Description**: *(TODO: Document)*

---

### 5. Copy Location Identifier to Type

- **Description**: Copies the **identifiers** from a Location resource to its **type** property.
- **Details**:
    - Facilitates normalization of **local codes** into standardized codes via the `Location.type` field.
- **Use Case**: Ensuring location data adheres to external standards.

---

### 6. Fix Period Dates

- **Description**: Ensures that the precision of `Period.start` matches the precision of `Period.end`.
- **Details**:
    - Modifies `Period.start` to match the precision of `Period.end`.
    - Prevents errors in the CQL (measure evaluation) engine caused by inconsistent precision in Period types.
- **Use Case**: Avoiding processing errors during measure evaluation.

## Configuration

Normalization settings are **tenant-specific** and configured to meet the unique requirements of each tenant. This ensures flexibility and adaptability across different EHR implementations.

Order of operations in the normalization configuration is defined by the index of each operation in the dictionary/configuration.

Example:

```json
{
  "OperationSequence": {
    "1": {
      "$type": "CopyLocationIdentifierToTypeOperation"
    },
    "0": {
      "$type": "ConceptMapOperation"
    },
    "2": {
      "$type": "ConditionalTransformationOperation"
    }
  }
}
```

In this case, "ConceptMapOperation" is executed first, followed by "CopyLocationIdentifier...", followed by "ConditionalTransform...".

---

## Notes

- Normalization ensures that all FHIR resources are processed in compliance with the applicable standards and requirements.
- Extensions added to properties retain original values, enabling transparency and traceability in the normalization process.

---

For more details on the Normalization service, refer to the [Normalization Service Specification](../service_specs/normalization.md).
