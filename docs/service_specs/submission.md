[← Back Home](../README.md)

## Submission Overview

The Submission service is responsible for packaging a tenant's reporting content and submitting them to a configured destination. Currently, the service only writes the submission content to its local file store. The submission package for a reporting period includes the following files:

| File | Description | Multiple Files? |
| ---- | ---- | ---- |
| Aggregate | A [MeasureReport](https://hl7.org/fhir/R4/measurereport.html) resource that contains references to each patient evaluation for a specific measure | Yes, one per measure | 
| Patient List | A [List](https://hl7.org/fhir/R4/list.html) resource of all patients that were admitted into the facility during the reporting period | No |
| Device | A [Device](https://hl7.org/fhir/R4/device.html) resource that details the version of Link Cloud that was used | No |
| Organization | An [Organization](https://hl7.org/fhir/R4/organization.html) resource for the submitting facility | No |
| Other Resources | A [Bundle](https://hl7.org/fhir/R4/bundle.html) resource that contains all of the shared resources (Location, Medication, etc) that are referenced in the patient Measure Reports | No |
| Patient | A [Bundle](https://hl7.org/fhir/R4/bundle.html) resource that contains the MeasureReports and related resources for a patient | Yes, one per evaluated patient |

An example of the submission package can be found at `\link-cloud\Submission Example`.

- **Technology**: .NET Core
- **Image Name**: link-submission
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Volumes**: Azure Storage Account File Share mounted at `/Link/Submission`

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |
| KafkaConnection:BootstrapServers:0          | `<KafkaBootstrapServer>`      | No      |
| KafkaConnection:GroupId                     | submission-events             | No      |
| KafkaConnection:ClientId                    | submission-events             | No      |
| MongoDB:ConnectionString                    | `<ConnectionString>`          | Yes     |
| MongoDb:DatabaseName                        | `<DatabaseName>`              | No      |
| SubmissionServiceConfig:ReportServiceUrl    | `<ReportServiceUrl>/api/Report/GetSubmissionBundle` | No      |
| FileSystemConfig:FilePath                   | `/data/Submission`            | No      |
| EnableSwagger                               | true (DEV and TEST)           | No      |

## Consumed Events

- **SubmitReport**

## Produced Events

- **ReportSubmitted**
