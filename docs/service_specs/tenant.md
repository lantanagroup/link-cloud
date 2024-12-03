[← Back Home](../README.md)

## Tenant Overview

The Tenant service is the entry point for configuring a tenant into Link Cloud. The service is responsible for maintaining and generating events for the scheduled measure reporting periods that the tenant is configured for. These events contain the initial information needed for Link Cloud to query resources and perform measure evaluations based on a specific reporting period.

- **Technology**: .NET Core
- **Image Name**: link-tenant
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| Link__Audit__ExternalConfigurationSource     | AzureAppConfiguration          | No       |
| ConnectionStrings__AzureAppConfiguration     | `<AzureAppConfigEndpoint>`     | Yes      |

## App Settings

### Kafka

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| KafkaConnection__BootstrapServers__0         | `<KafkaBootstrapServer>`       | No       |
| KafkaConnection__GroupId                     | tenant-events                  | No       |

### Database

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| MongoDB__ConnectionString                    | `<ConnectionString>`           | Yes      |
| MongoDB__DatabaseName                        | `<DatabaseName>`               | No       |
| MongoDB__CollectionName                      | tenant                         | No       |

### Service Endpoints

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| MeasureServiceRegistry__MeasureServiceApiUrl | `<MeasureServiceUrl>`          | No       |

### Additional Settings

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| EnableSwagger                                | true (DEV and TEST)            | No       |

## Consumed Events

- **NONE**

## Produced Events

- **ReportScheduled**

## API Operations

The **Tenant** service provides REST endpoints for managing facility configurations and related metadata.

- **GET /api/Facility**: Retrieve a paged list of facilities based on filters such as `facilityId`, `facilityName`, and sorting options.
- **POST /api/Facility**: Create a new facility configuration.
- **GET /api/Facility/{facilityId}**: Retrieve a specific facility configuration by `facilityId`.
- **PUT /api/Facility/{id}**: Update an existing facility configuration by `id`.
- **DELETE /api/Facility/{facilityId}**: Delete a facility configuration by `facilityId`.

These operations support managing tenant-specific configurations and workflows efficiently.