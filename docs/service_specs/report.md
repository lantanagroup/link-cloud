[← Back Home](../README.md)

## Report

### Overview

- **Technology**: .NET Core
- **Image Name**: link-report
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |
| KafkaConnection:BootstrapServers:0          | `<KafkaBootstrapServer>`      | No      |
| KafkaConnection:GroupId                     | report-events                 | No      |
| KafkaConnection:ClientId                    | report-events                 | No      |
| MongoDB:ConnectionString                    | `<ConnectionString>`          | Yes     |
| MongoDB:DatabaseName                        | `<DatabaseName>`              | No      |
| MongoDB:CollectionName                      | report                        | No      |
| TenantApiSettings:TenantServiceBaseEndpoint | `<TenantApiUrl>/api`          | No      |

### Consumed Events

- **ReportScheduled**
- **MeasureEvaluated**
- **PatientsToQuery**
- **ReportSubmitted**

### Produced Events

- **SubmitReport**
- **DataAcquisitionRequested**
- **NotificationRequested**
