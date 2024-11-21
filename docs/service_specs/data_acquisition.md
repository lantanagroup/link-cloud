[← Back Home](../README.md)

## Data Acquisition Overview

The Data Acquisition service is responsible for connecting and querying a tenant's endpoint for FHIR resources that are needed to evaluate patients for a measure. For Epic installations, Link Cloud is utilizing the [Epic FHIR STU3 Patient List](https://fhir.epic.com/Specifications?api=879) resource to inform which patients are currently admitted in the facility. While this is the current solution to acquiring the patient census, there are other means of patient acquisition being investigated (ADT V2, Bulk FHIR) to provide universal support across multiple EHR vendors.

- **Technology**: .NET Core
- **Image Name**: link-dataacquisition
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Kafka Connection

| Name                                     | Value                     | Secret?  |
|------------------------------------------|---------------------------|----------|
| KafkaConnection__BootstrapServers__0     | `<KafkaBootstrapServer>`  | No       |
| KafkaConnection__GroupId                 | data-acquisition-events   | No       |

## Consumed Events

- **PatientEvent**
- **PatientBulkAcquisitionScheduled**

## Produced Events

- **PatientIdsAcquired**
- **PatientAcquired**
- **NotificationRequested**