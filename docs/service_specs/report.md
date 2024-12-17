[← Back Home](../README.md)

## Report Overview

The Report service is responsible for persisting the Measure Reports and FHIR resources that the Measure Eval service generates after evaluating a patient against a measure. When a tenant's reporting period end date has been met, the Report Service performs various workflows to determine if all of the patient MeasureReports are accounted for that period prior to initiating the submission process.

- **Technology**: .NET Core
- **Image Name**: link-report
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Kafka

| Name                                          | Value                          | Secret? |
|-----------------------------------------------|--------------------------------|---------|
| KafkaConnection__BootstrapServers__0          | `<KafkaBootstrapServer>`       | No      |
| KafkaConnection__GroupId                      | report-events                  | No      |
| KafkaConnection__ClientId                     | report-events                  | No      |

### Database

| Name                                          | Value                          | Secret? |
|-----------------------------------------------|--------------------------------|---------|
| MongoDB__ConnectionString                     | `<ConnectionString>`           | Yes     |
| MongoDB__DatabaseName                         | `<DatabaseName>`               | No      |
| MongoDB__CollectionName                       | report                         | No      |

### Service Endpoints

| Name                                          | Value                          | Secret? |
|-----------------------------------------------|--------------------------------|---------|
| TenantApiSettings__TenantServiceBaseEndpoint  | `<TenantApiUrl>/api`           | No      |

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
