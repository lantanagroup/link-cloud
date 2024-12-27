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
- **Database**: MongoDB
- **Volumes**: Azure Storage Account File Share mounted at `/Link/Submission`

See [Submission Functionality](../functionality/submission_folder.md) for more information on the role of the Submission service in the Link Cloud ecosystem.

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

### Additional Settings

| Name                                                | Value     | Description                                                                                                   | Secret? |
|-----------------------------------------------------|-----------|---------------------------------------------------------------------------------------------------------------|---------|
| SubmissionServiceConfig__SubmissionDirectory        | \<string> | The location of where to store submission files until they are ready to be submitted. i.e. `/data/Submission` | No      |
| SubmissionServiceConfig__PatientBundleBatchSize     | 1         |                                                                                                               | No      |
| SubmissionServiceConfig__MeasureNames__0__Url       | \<string> | URL of measure                                                                                                | No      |
| SubmissionServiceConfig__MeasureNames__0__MeasureId | \<string> | ID of measure                                                                                                 | No      |
| SubmissionServiceConfig__MeasureNames__0__ShortName | \<string> | Short name of the measure (used in building submission file name)                                             | No      |

## Kafka Events/Topics

### Consumed Events

- **SubmitReport**

### Produced Events

- **ReportSubmitted**

## API Operations

The **Submission** service does not provide any REST endpoints for external consumption. The service is only responsible for consuming Kafka events, and then packaging and submitting the reporting content to a configured destination.