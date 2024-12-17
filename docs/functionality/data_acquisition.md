[← Back Home](../README.md)

# Data Acquisition Overview

**Data Acquisition** is a crucial step in the report generation pipeline, responsible for obtaining clinical data from external systems, such as FHIR R4 endpoints within electronic health record (EHR) systems. By systematically acquiring and managing data, it ensures that downstream processes like evaluation and reporting are equipped with the necessary information.

## Key Roles of Data Acquisition

1. **Patient List Acquisition**:
    - Retrieves a FHIR List of patients from the EHR, if configured.
    - Serves as the initial step for identifying patients relevant to quality measure evaluations.

2. **Individual Patient Data Acquisition**:
    - Acquires detailed FHIR data for individual patients identified in the census.
    - Includes all essential data elements required for initial measure evaluations.

3. **Supplemental Data Acquisition**:
    - Retrieves additional data elements that may not be needed for initial evaluations but are desirable for complete submission and reporting.

## Where Data Acquisition Fits in the Pipeline

Data acquisition plays a role in three distinct stages of the report generation pipeline:

1. **Patient Identification**:
    - Acquiring a list of patients from the EHR to determine the cohort for evaluation.

2. **Initial Data Collection and Evaluation**:
    - Obtaining the primary dataset needed for evaluating quality measures.
    - Determines whether a patient qualifies for reporting based on initial measure criteria.

3. **Supplemental Data Collection**:
    - Acquiring additional, non-essential data to enrich the submission.
    - Completes the dataset for comprehensive evaluation and reporting.

## Progressive Querying

To optimize data acquisition, the system employs a technique called **Progressive Querying**.

During progressive querying, data is acquired in stages to meet the evaluation pipeline's needs. It flows between services via Kafka topics/events, starting from data acquisition to normalization, through initial evaluation to determine patient relevance, and back to acquire supplemental data, which is then normalized and re-evaluated.

This method minimizes the data retrieved from the EHR by acquiring only what is necessary at each stage of the pipeline:

1. **Initial Querying**:
    - Focuses on essential data needed to evaluate measures and determine patient inclusion.

2. **Supplemental Querying**:
    - Retrieves additional data elements after initial evaluations confirm patient relevance.

3. **Final Evaluation**:
    - Combines initial and supplemental data for comprehensive measure evaluation and reporting.

### Benefits of Progressive Querying
- **Efficiency**: Reduces the volume of data retrieved from the EHR, optimizing system performance.
- **Precision**: Focuses on acquiring only data that is needed for specific stages in the pipeline.
- **Scalability**: Supports large-scale operations by limiting unnecessary data transfers.

## Bulk FHIR in Data Acquisition

**Bulk FHIR** is a mechanism under exploration for acquiring data efficiently. However, several limitations impact its general use in data acquisition workflows:

1. **Challenges with Bulk FHIR**:
    - Most implementations lack sufficient support for acquiring specific patient data.
    - Filtering returned data is often not robust enough.
    - To align with the goal of acquiring only necessary data, the system does not currently implement Bulk FHIR for general initial or supplemental data acquisition.

2. **Use Cases for Bulk FHIR**:
    - **Patient Census Identification**:
        - Bulk FHIR is a viable solution for identifying the "census of patients," analogous to using the FHIR "List" endpoint.
        - It can acquire "Patient" resources for a group of patients associated with a query, filter, or registry in the EHR.
        - This use case is limited to identifying patients of interest and does not address broader data acquisition.

## Configuration for Data Acquisition

Data acquisition is configurable per tenant, ensuring flexibility to accommodate diverse EHR systems and data requirements. Key configurable parameters include:

- **Base FHIR URL**:
    - For general data acquisition.
    - For FHIR List (patients of interest) retrieval.

- **Authentication Information**:
    - Such as client credentials (e.g., "client id").

- **Patient Census Retrieval**:
    - FHIR List "id" or Bulk FHIR "Group ID" used for identifying the patient cohort.

- **EHR Query Throttling/Limitations**:
    - Configurable settings to respect EHR query limitations (e.g., maximum queries per minute).

---

By integrating progressive querying, exploring Bulk FHIR for specific use cases, and offering tenant-level configurability, the data acquisition process is designed to be efficient, precise, and adaptable, ensuring seamless integration within the report generation pipeline.
