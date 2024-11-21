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

| Name                                         | Value                                                | Secret?  |
|----------------------------------------------|------------------------------------------------------|----------|
| Link__Audit__ExternalConfigurationSource     | AzureAppConfiguration                                | No       |
| ConnectionStrings__AzureAppConfiguration     | `<AzureAppConfigEndpoint>`                           | Yes      |

## App Settings

### Kafka

| Name                                         | Value                                                | Secret?  |
|----------------------------------------------|------------------------------------------------------|----------|
| KafkaConnection__BootstrapServers__0         | `<KafkaBootstrapServer>`                             | No       |
| KafkaConnection__GroupId                     | submission-events                                    | No       |
| KafkaConnection__ClientId                    | submission-events                                    | No       |

### Database
| Name                                         | Value                                                | Secret?  |
|----------------------------------------------|------------------------------------------------------|----------|
| MongoDB__ConnectionString                    | `<ConnectionString>`                                 | Yes      |
| MongoDb__DatabaseName                        | `<DatabaseName>`                                     | No       |

### Service Endpoints

| Name                                         | Value                                                | Secret?  |
|----------------------------------------------|------------------------------------------------------|----------|
| SubmissionServiceConfig__ReportServiceUrl    | `<ReportServiceUrl>/api/Report/GetSubmissionBundle`  | No       |

### Additional Settings

| Name                                         | Value                                                | Secret?  |
|----------------------------------------------|------------------------------------------------------|----------|
| FileSystemConfig__FilePath                   | `/data/Submission`                                   | No       |
| EnableSwagger                                | true (DEV and TEST)                                  | No       |

## Consumed Events

- **SubmitReport**

## Produced Events

- **ReportSubmitted**
