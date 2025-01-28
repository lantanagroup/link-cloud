[← Back Home](../README.md)

## Query Dispatch Overview

The Query Dispatch service is primarily responsible for applying a lag period prior to making FHIR resource query requests against a facility endpoint. The current implementation of the Query Dispatch service handles how long Link Cloud should wait before querying for a patient’s FHIR resources after being discharged. To ensure that the encounter related data for the patient has been settled (Medications have been closed, Labs have had their results finalized, etc), tenants are able to customize how long they would like the lag from discharge to querying to be.

- **Technology**: .NET Core
- **Image Name**: link-querydispatch
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Common Configurations

* [Swagger](../config/csharp.md#swagger)
* [Azure App Configuration](../config/csharp.md#azure-app-config-environment-variables)
* [Kafka Configuration](../config/csharp.md#kafka)
* [Kafka Consumer Retry Configuration](../config/csharp.md#kafka-consumer-settings)
* [Service Registry Configuration](../config/csharp.md#service-registry)
* [CORS Configuration](../config/csharp.md#cors)
* [Token Service Configuration](../config/csharp.md#token-service-settings)
* [Service Authentication](../config/csharp.md#service-authentication)
* [SQL Server Database Configuration](../config/csharp.md#sql-server-database)

## Kafka Events/Topics

### Consumed Events

- **ReportScheduled**
- **PatientEvent**

### Produced Events

- **DataAcquisitionRequested**

## API Operations

The **Query Dispatch** service provides REST endpoints for managing query dispatch configurations for each facility. These configurations define schedules and triggers for dispatching queries.

- **GET /api/querydispatch/configuration/facility/{facilityId}**: Retrieve a query dispatch configuration for a specific facility by `facilityId`.
- **POST /api/querydispatch/configuration**: Create a new query dispatch configuration.
- **PUT /api/querydispatch/configuration/facility/{facilityId}**: Update the query dispatch configuration for a specific facility by `facilityId`.
- **DELETE /api/querydispatch/configuration/facility/{facilityId}**: Delete the query dispatch configuration for a specific facility by `facilityId`.

Each operation supports the customization and management of query dispatch schedules and triggers, ensuring efficient and accurate query dispatching across workflows.