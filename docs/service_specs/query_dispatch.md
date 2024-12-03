[← Back Home](../README.md)

## Query Dispatch Overview

The Query Dispatch service is primarily responsible for applying a lag period prior to making FHIR resource query requests against a facility endpoint. The current implementation of the Query Dispatch service handles how long Link Cloud should wait before querying for a patient’s FHIR resources after being discharged. To ensure that the encounter related data for the patient has been settled (Medications have been closed, Labs have had their results finalized, etc), tenants are able to customize how long they would like the lag from discharge to querying to be.

- **Technology**: .NET Core
- **Image Name**: link-querydispatch
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Kafka Connection

| Name                                     | Value                     | Secret? |
|------------------------------------------|---------------------------|---------|
| KafkaConnection__BootstrapServers__0     | `<KafkaBootstrapServer>`  | No      |
| KafkaConnection__GroupId                 | query-dispatch-events     | No      |
| KafkaConnection__ClientId                | query-dispatch-events     | No      |

### Database Settings (MSSQL)

| Name                      | Value                | Secret?  |
|---------------------------|----------------------|----------|
| MongoDB__ConnectionString | `<ConnectionString>` | Yes      |
| MongoDB__DatabaseName     | `<DatabaseName>`     | No       |
| MongoDB__CollectionName   | `<CollectionName>`   | No       |

### Additional Settings

| Name          | Value                           | Secret? |
|---------------|---------------------------------|---------|
| EnableSwagger | true (DEV and TEST)             | No      |

## Consumed Events

- **ReportScheduled**
- **PatientEvent**

## Produced Events

- **DataAcquisitionRequested**

## API Operations

The **Query Dispatch** service provides REST endpoints for managing query dispatch configurations for each facility. These configurations define schedules and triggers for dispatching queries.

- **GET /api/querydispatch/configuration/facility/{facilityId}**: Retrieve a query dispatch configuration for a specific facility by `facilityId`.
- **POST /api/querydispatch/configuration**: Create a new query dispatch configuration.
- **PUT /api/querydispatch/configuration/facility/{facilityId}**: Update the query dispatch configuration for a specific facility by `facilityId`.
- **DELETE /api/querydispatch/configuration/facility/{facilityId}**: Delete the query dispatch configuration for a specific facility by `facilityId`.

Each operation supports the customization and management of query dispatch schedules and triggers, ensuring efficient and accurate query dispatching across workflows.