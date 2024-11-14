[← Back Home](../README.md)

## Audit

### Overview

- **Technology**: .NET Core
- **Image Name**: link-audit
- **Port**: 8080
- **Database**: MSSQL

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

### Kafka Connection

| Name                                    | Value                    | Secret? |
|-----------------------------------------|--------------------------|---------|
| Link:Audit:KafkaConnection:BootstrapServers:0 | `<KafkaBootstrapServer>` | No      |
| Link:Audit:KafkaConnection:GroupId          | audit-events             | No      |
| Link:Audit:KafkaConnection:ClientId         | audit-events             | No      |

### Consumed Events

- **AuditableEventOccurred**

### Produced Events

- **NONE**
