
## Overview

This page and its references include documentation for Link's services and the functionality those services supports.

The following flow chart describes the critical reporting events, services and persistence-points in the Link workflow.

```mermaid
flowchart

    Tenant["«service» <br> Tenant"] --> ReportScheduled["«event» <br> Report Scheduled"]
    Tenant --> GenerateReportRequested["«event» <br> Generate Report Requested"]
    GenerateReportRequested --> R
    R -- Regeneration requested--> EvaluationRequested["«event» <br> EvaluationRequested"]
    EvaluationRequested --> ME
    ME -- Normalized resources--> MeasureEvalPersistence["«persistence» <br> MeasureEvalPersistence"]
    ReportScheduled --> QueryDispatch["«service» <br> Query Dispatch"]
    QueryDispatch -- Initial Population data requests --> DAR["«event» <br> Data Acquisition Requested"]
    DAR --> DA
    PE["«event» <br> Patient Event"] --> QueryDispatch
    C["«service» <br> Census"] --> PE
    C -- patientEvents --> CensusPersistence["«persistence» <br> Census Persistence"]
    C --> PCS["«event» <br> Patient Census Scheduled"]
    PIA["«event» <br> PatientIDs Acquired"] --> C
    PCS --> DA["«service» <br> Data Acquisition"]
    DA --> PIA
    DA -- FHIR resource references--> DataAcquisitionPersistence["«persistence» <br> DataAcq Persistence"]
    DA --> EHR["«service» <br> EHR"]
    DA --> PA["«event» <br> Resource Acquired"]
    PA --> N["«service» <br> Normalization"]
    N --> PN["«event» <br> Resource Normalized"]
    PN --> ME["«service» <br> Measure Eval"]
    ME -- evaluate CQL with--> CQL["«service» <br> CQL Engine"]
    ME --> MEV["«event» <br> Resource Evaluated"]
    ME -- Supplemental data elements requests--> DAR
    MEV --> R["«service» <br>Report"]
    R --> EvaluatedResourcePersistence["«persistence» <br> EvaluatedResourcePersistence"]
    ReportScheduled --> R
    R -- Initial Population data requests --> DAR
    R --> SR["«event» <br> Submit Report"]
    SR --> S["«service» <br> Submission"]
    S --> R
    S --> SD["«service» <br> Submission Destination"]
    S --> ReportSubmitted["«event» <br> ReportSubmitted"]
    ReportSubmitted-->R
    R --> ReadyForValidate["«event» <br> ReadyForValidation"]
    ReadyForValidate --> Validation["«service» <br> Validation"]
    Validation --> ValidationComplete["«event» <br> ValidationComplete"]
    Validation -- Validation Issues and Categories --> ValidationPersistence["«persistence» <br> ValidationPersistence"]
    ValidationComplete --> R

    %% Define a reusable style
    classDef service fill:#aaf,stroke:#333,stroke-width:2px,color:#000;
    classDef event fill:#afa,stroke:#333,stroke-width:2px,color:#000;
    classDef external fill:#f99,stroke:#333,stroke-width:2px,color:#000;
    classDef persistence fill:#ddd,stroke:#333,stroke-width:2px,color:#000;

    %% Apply the class to multiple nodes
    class N,S,Validation,ME,CQL,C,DA,Tenant,R,QueryDispatch service;
    class DAR,PCS,PIA,SR,MEV,PN,PA,ReportScheduled,PE,EvaluationRequested,GenerateReportRequested,ReportSubmitted,ReadyForValidate,ValidationComplete event;
    class EHR,SD external;
    class ValidationPersistence,CensusPersistence,DataAcquisitionPersistence,EvaluatedResourcePersistence,MeasureEvalPersistence persistence;
```

## [Functionality](functionality/README.md)

* [Admin UI](functionality/admin_ui.md)
* [Census Management](functionality/census_management.md)
* [Data Acquisition](functionality/data_acquisition.md)
* [Kafka Retry Topics](functionality/retry_topics.md)
* [Measure Evaluation](functionality/measure_eval.md)
* [Notifications](functionality/notifications.md)
* [Normalization](functionality/normalization.md)
* [Report](functionality/report.md)
* Security
  * [Security Overview](functionality/security_overview.md)
  * [OAuth & Cookie Flow](functionality/oauth_flow.md)
* [Submission Folder Structure](functionality/submission_folder.md)
* [Telemetry](functionality/telemetry.md)
* [Tenant/Facility Management](functionality/tenant_mgmt.md)
* [Validation](functionality/validation.md)

## Service Specifications

* [Account](service_specs/account.md)
* [Admin UI](service_specs/admin_ui.md)
* [Audit](service_specs/audit.md)
* [Backend For Frontend (BFF)](service_specs/bff.md)
* [Census](service_specs/census.md)
* [Data Acquisition](service_specs/data_acquisition.md)
* [Measure Evaluation](service_specs/measure_eval.md)
* [Normalization](service_specs/normalization.md)
* [Notification](service_specs/notification.md)
* [Query Dispatch](service_specs/query_dispatch.md)
* [Report](service_specs/report.md)
* [Submission](service_specs/submission.md)
* [Tenant](service_specs/tenant.md)
* [Validation](service_specs/validation.md)

### Development

Documentation for contributing to and developing in the Link project can be found [here](development/README.md).

### Service Swagger Specifications

When deployed, each service provides a Swagger UI for exploring its API. The Swagger UI is available at the `/swagger` endpoint for most of the services. For example, the Swagger UI for the **Account** service is available at `https://link-account/swagger`. However, the endpoint for swagger specifications varies between .NET and Java.

* .NET Services
  * UI: `/swagger`
  * JSON: `/swagger/v1/swagger.json`
* Java Services
  * UI: `/swagger-ui.html`
  * JSON: `/v3/api-docs`

## Java

### Kafka Authentication

If Kafka requires authentication (such as SASL_PLAINTEXT) the Java services use the following (example) properties:

| Property Name                             | Value                                                                                               |
|-------------------------------------------|-----------------------------------------------------------------------------------------------------|
| spring.kafka.properties.sasl.jaas.config  | org.apache.kafka.common.security.plain.PlainLoginModule required username=\"XXX\" password=\"XXX\"; |
| spring.kafka.properties.sasl.mechanism    | PLAIN                                                                                               |
| spring.kafka.properties.security.protocol | SASL_PLAINTEXT                                                                                      |

These properties can be applied when running/debugging the services locally by passing them as VM arguments, such as `-Dspring.kafka.properties.sasl.mechanism=PLAIN`.

### Azure App Config

Note: If a Java service is configured to use Azure App Config, keys in ACA take precedence over Java VM args _and_ Environment Variables.