# Functionality

Link Cloud offers a comprehensive workflow for acquiring, processing, and submitting clinical data. The features described below highlight how the platform can be configured to meet the needs of different healthcare organizations.

## Table of Contents

1. [General](#general)
2. [Census Acquisition](#census-acquisition)
3. [Patient Data Acquisition](#patient-data-acquisition)
4. [Normalization](#normalization)
5. [Evaluation](#evaluation)
6. [Validation](#validation)
7. [Submission](#submission)
8. [User Interface](#user-interface)

## General

Link Cloud coordinates services that ingest data from electronic health record (EHR) systems, evaluate that data against regulatory measures, and deliver compliant reports. Institutions are configured as individual tenants so that authentication, scheduling, and data storage can be tailored to their environment.

- **Flexible data persistence** – Clinical resources are stored in MongoDB or CosmosDB while service and tenant settings are kept in SQL Server.
- **Progressive queries** – The platform differentiates between required and supplemental data, retrieving only what is needed for a given report.
- **Automated operations** – Built‑in schedulers can trigger data acquisition and reporting tasks on a recurring basis.
- **Authentication options** – Supports Basic, OAuth, and specific EHR implementations (Epic and Cerner) with plans to add JWKS certificate validation.
- **Observability** – OpenTelemetry metrics can be visualized through tools such as Grafana for system monitoring.
- **User management** – Administrators can manage access and configure services for each tenant.
- **Future integrations** – Plans include connecting to external systems that help determine facility reportability.

## Census Acquisition

A reliable patient census is critical for accurate reporting. Link Cloud acquires census data from hospital EHRs using configurable methods so that facilities can select the approach that matches their workflow.

- **FHIR List** – Retrieve lists of active patients via a standard FHIR endpoint by specifying the appropriate `listId` for each tenant.
- **Planned methods** – Support for ADT feeds and CSV files over SFTP is on the roadmap.
- **Exploring Bulk FHIR** – Investigating the use of the Bulk FHIR specification for high‑volume exports.

## Patient Data Acquisition

Once a census has been gathered, Link Cloud pulls encounter‑level data for each patient. Acquisition follows a tenant-defined query plan which determines how resources are fetched and how far back in time the system should look.

- **Native FHIR API** – Uses the standard API of the source EHR to retrieve patient data.
- **Configurable query plans** – Administrators define which resources to request and how those requests depend on previously acquired data.
- **Lookback period** – Reporting periods can be adjusted to pull historical information when needed.
- **Resource references** – The acquisition process follows references to shared resources and uses `POST /:resourceType/_search` for bulk retrieval.
- **Future automation** – Work is underway to derive query plans automatically from digital quality measure (dQM) logic.

## Normalization

After acquisition, Link Cloud normalizes resources to ensure consistent structure and terminology across different EHR implementations. Changes are recorded for traceability, and normalized data is stored for downstream evaluation.

- **Core transformations** – Supports concept mapping, fixing resource identifiers, conditional transformations, location identifier adjustments, and correcting date precision.
- **Audit trail** – Modifications are captured in FHIR extensions so that every change is transparent.
- **Planned improvements** – Upcoming features include defining the order of transformations and logging changes separately from the clinical data to reduce storage size.

## Evaluation

The evaluation stage checks patient data against quality measures specified in Implementation Guides (IGs). By leveraging the cqframework library, Link Cloud can evaluate multiple measures during the same reporting period.

- **Measure resources** – Loads Measure, Library, ValueSet, and CodeSystem resources from IG packages.
- **Patient-level assessment** – Runs CQL logic for each patient and for each configured measure.

## Validation

Before data is packaged for submission, Link Cloud validates the resulting FHIR bundles. Validation rules can halt submission if critical issues are found.

- **Profile conformance** – Validates resources against core FHIR R4 rules and any profiles asserted in the data.
- **Configurable categories** – Validation results are categorized so that organizations can prioritize issues that matter most to them.
- **Future enforcement** – Planned functionality will allow submissions to be blocked when validation fails.

## Submission

When evaluation and validation are complete, Link Cloud organizes the data into a structured folder for auditing and final transmission.

- **CQL-driven output** – Evaluation returns the resources that need to be submitted (typically via `MeasureReport.contained`).
- **Debug-friendly structure** – Patient data is organized in a clear folder hierarchy before final submission to cloud storage or other destinations.

## User Interface

An internal admin UI gives authorized users visibility into system operations and configuration options. Future work aims to expose additional reporting and review capabilities.

- **Tenant administration** – Manage users and configure components for each organization.
- **Upcoming features** – Planned enhancements include census review tools, reporting metadata views, and validation dashboards. An external facility-facing interface is also under consideration.

