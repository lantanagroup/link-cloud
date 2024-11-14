[← Back Home](../README.md)

## Tenant Overview

The Tenant service is the entry point for configuring a tenant into Link Cloud. The service is responsible for maintaining and generating events for the scheduled measure reporting periods that the tenant is configured for. These events contain the initial information needed for Link Cloud to query resources and perform measure evaluations based on a specific reporting period.

- **Technology**: .NET Core
- **Image Name**: link-tenant
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |
| KafkaConnection:BootstrapServers:0          | `<KafkaBootstrapServer>`      | No      |
| KafkaConnection:GroupId                     | tenant-events                 | No      |
| MongoDB:ConnectionString                    | `<ConnectionString>`          | Yes     |
| MongoDB:DatabaseName                        | `<DatabaseName>`              | No      |
| MongoDB:CollectionName                      | tenant                        | No      |
| MeasureServiceRegistry:MeasureServiceApiUrl | `<MeasureServiceUrl>`         | No      |
| EnableSwagger                               | true (DEV and TEST)           | No      |

## Consumed Events

- **NONE**

## Produced Events

- **ReportScheduled**
