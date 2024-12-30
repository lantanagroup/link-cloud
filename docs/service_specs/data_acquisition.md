[← Back Home](../README.md)

## Data Acquisition Overview

The Data Acquisition service is responsible for connecting and querying a tenant's endpoint for FHIR resources that are needed to evaluate patients for a measure. For Epic installations, Link Cloud is utilizing the [Epic FHIR STU3 Patient List](https://fhir.epic.com/Specifications?api=879) resource to inform which patients are currently admitted in the facility. While this is the current solution to acquiring the patient census, there are other means of patient acquisition being investigated (ADT V2, Bulk FHIR) to provide universal support across multiple EHR vendors.

- **Technology**: .NET Core
- **Image Name**: link-dataacquisition
- **Port**: 8080
- **Database**: MSSQL

See [Data Acquisition Functionality](../functionality/data_acquisition.md) for more information on the role of the Data Acquisition service in the Link Cloud ecosystem.

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

- **PatientEvent**
- **PatientBulkAcquisitionScheduled**

### Produced Events

- **PatientIdsAcquired**
- **PatientAcquired**
- **NotificationRequested**

## API Operations

The **Data Acquisition** service provides REST endpoints to manage and execute data acquisition processes, configuration settings, and connection validations for facilities.

### Authentication Configuration

- **GET /api/data/{facilityId}/{queryConfigurationTypePathParameter}/authentication**: Retrieves the authentication settings for a specified facility and configuration type.
- **POST /api/data/{facilityId}/{queryConfigurationTypePathParameter}/authentication**: Creates authentication settings for a specified facility.
- **PUT /api/data/{facilityId}/{queryConfigurationTypePathParameter}/authentication**: Updates authentication settings for a specified facility.
- **DELETE /api/data/{facilityId}/{queryConfigurationTypePathParameter}/authentication**: Deletes authentication settings for a specified facility.

### Connection Validation

- **GET /api/data/connectionValidation/{facilityId}/$validate**: Validates the connection between a facility and Link services.

### Query Configuration

- **GET /api/data/{facilityId}/fhirQueryConfiguration**: Retrieves the FHIR Query Configuration for a specified facility.
- **POST /api/data/fhirQueryConfiguration**: Creates a FHIR Query Configuration for a specified facility.
- **PUT /api/data/fhirQueryConfiguration**: Updates the FHIR Query Configuration for a specified facility.
- **DELETE /api/data/{facilityId}/fhirQueryConfiguration**: Deletes the FHIR Query Configuration for a specified facility.

### Query List

- **GET /api/data/{facilityId}/fhirQueryList**: Retrieves the FHIR Query List for a specified facility.
- **POST /api/data/fhirQueryList**: Creates a FHIR Query List for a specified facility.
- **PUT /api/data/fhirQueryList**: Updates the FHIR Query List for a specified facility.
- **DELETE /api/data/{facilityId}/fhirQueryList**: Deletes the FHIR Query List for a specified facility.

### Query Plan

- **GET /api/data/{facilityId}/QueryPlan**: Retrieves the Query Plan Configuration for a specified facility.
- **POST /api/data/{facilityId}/QueryPlan**: Creates a Query Plan Configuration for a specified facility.
- **PUT /api/data/{facilityId}/QueryPlan**: Updates a Query Plan Configuration for a specified facility.
- **DELETE /api/data/{facilityId}/QueryPlan**: Deletes a Query Plan Configuration for a specified facility.

### Query Results

- **GET /api/data/{facilityId}/QueryResult/{correlationId}**: Retrieves the query results for a given correlation ID.

These operations support the configuration and execution of data acquisition processes, ensuring that facilities can efficiently retrieve and manage their data.