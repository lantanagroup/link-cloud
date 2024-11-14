[← Back Home](../README.md)

## Query Dispatch

### Overview

- **Technology**: .NET Core
- **Image**: link-querydispatch
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

### Kafka Connection

| Name                                    | Value                    | Secret? |
|-----------------------------------------|--------------------------|---------|
| KafkaConnection:BootstrapServers:0       | `<KafkaBootstrapServer>` | No      |
| KafkaConnection:GroupId                  | query-dispatch-events    | No      |
| KafkaConnection:ClientId                 | query-dispatch-events    | No      |

### Database Settings (MSSQL)

| Name                   | Value                | Secret? |
|------------------------|----------------------|---------|
| MongoDB:ConnectionString | `<ConnectionString>` | Yes   |
| MongoDB:DatabaseName     | `<DatabaseName>`     | No    |
| MongoDB:CollectionName   | `<CollectionName>`   | No    |

### Additional Settings

| Name         | Value                          | Secret? |
|--------------|--------------------------------|---------|
| EnableSwagger | true (DEV and TEST)           | No      |

### Consumed Events

- **ReportScheduled**
- **PatientEvent**

### Produced Events

- **DataAcquisitionRequested**
