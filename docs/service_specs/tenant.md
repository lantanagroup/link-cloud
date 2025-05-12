[← Back Home](../README.md)

## Tenant Overview

The Tenant service is the entry point for configuring a tenant into Link Cloud. The service is responsible for maintaining and generating events for the scheduled measure reporting periods that the tenant is configured for. These events contain the initial information needed for Link Cloud to query resources and perform measure evaluations based on a specific reporting period.

- **Technology**: .NET Core
- **Image Name**: link-tenant
- **Port**: 8080
- **Database**: MSSQL
- **Scale**: 0-3

See [Tenant Functionality](../functionality/tenant_mgmt.md) for more information on the role of the Tenant service in the Link Cloud ecosystem.

## Common Configurations

* [Swagger](../config/csharp.md#swagger)
* [Azure App Configuration](../config/csharp.md#azure-app-config-environment-variables)
* [Kafka Configuration](../config/csharp.md#kafka)
* [Service Registry Configuration](../config/csharp.md#service-registry)
* [CORS Configuration](../config/csharp.md#cors)
* [Token Service Configuration](../config/csharp.md#token-service-settings)
* [Service Authentication](../config/csharp.md#service-authentication)
* [SQL Server Database](../config/csharp.md#sql-server-database)

### Additional Settings

| Name                                | Value         | Secret? |
|-------------------------------------|---------------|---------|
| MeasureConfig__CheckIfMeasureExists | true or false | No      |

## Kafka Events/Topics

### Consumed Events

- **NONE**

### Produced Events

- **ReportScheduled**
- **AuditableEventOccurred**
- **GenerateReportRequested**

## API Operations

The **Tenant** service provides REST endpoints for managing facility configurations and related metadata.

- **GET /api/Facility**: Retrieve a paged list of facilities based on filters such as `facilityId`, `facilityName`, and sorting options.
- **POST /api/Facility**: Create a new facility configuration.
- **GET /api/Facility/{facilityId}**: Retrieve a specific facility configuration by `facilityId`.
- **PUT /api/Facility/{id}**: Update an existing facility configuration by `id`.
- **DELETE /api/Facility/{facilityId}**: Delete a facility configuration by `facilityId`.

These operations support managing tenant-specific configurations and workflows efficiently.