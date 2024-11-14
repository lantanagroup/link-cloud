[← Back Home](../README.md)

## Tenant

### Overview

- **Technology**: .NET Core
- **Image Name**: link-tenant
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

### Environment Variables

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

### Consumed Events

- **NONE**

### Produced Events

- **ReportScheduled**
