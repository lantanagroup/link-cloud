[← Back Home](../README.md)

## Report Overview

The Report service is responsible for persisting the Measure Reports and FHIR resources that the Measure Eval service generates after evaluating a patient against a measure. When a tenant's reporting period end date has been met, the Report Service performs various workflows to determine if all of the patient MeasureReports are accounted for that period prior to initiating the submission process.

- **Technology**: .NET Core
- **Image Name**: link-report
- **Port**: 8080
- **Database**: MongoDB

## Common Configurations

* [Swagger](../config/csharp.md#swagger)
* [Azure App Configuration](../config/csharp.md#azure-app-config-environment-variables)
* [Kafka Configuration](../config/csharp.md#kafka)
* [Kafka Consumer Retry Configuration](../config/csharp.md#kafka-consumer-settings)
* [Service Registry Configuration](../config/csharp.md#service-registry)
* [CORS Configuration](../config/csharp.md#cors)
* [Token Service Configuration](../config/csharp.md#token-service-settings)
* [Service Authentication](../config/csharp.md#service-authentication)
* [Mongo Database](../config/csharp.md#mongo-database)

## Kafka Events/Topics

### Consumed Events

- **ReportScheduled**
- **MeasureEvaluated**
- **PatientsToQuery**
- **ReportSubmitted**

### Produced Events

- **SubmitReport**
- **DataAcquisitionRequested**
- **NotificationRequested**

## API Operations

The **Report** service provides REST endpoints for retrieving serialized patient submission data based on reporting criteria.

- **GET /api/Report/Bundle/Patient**: Retrieve a serialized `PatientSubmissionModel` containing all patient-level resources and associated resources for all measure reports, filtered by `facilityId`, `patientId`, and a specified reporting period (`startDate` and `endDate`).

This operation supports reporting and analytics workflows by enabling access to comprehensive patient-level data for specified timeframes.
