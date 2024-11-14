[← Back Home](../README.md)

## Normalization

### Overview

- **Technology**: .NET Core
- **Image Name**: link-normalization
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
| KafkaConnection:GroupId                  | normalization-events     | No      |
| KafkaConnection:ClientId                 | normalization-events     | No      |

### Database Settings (MSSQL)

| Name                   | Value                      | Secret? |
|------------------------|----------------------------|---------|
| MongoDB:ConnectionString | `<DatabaseConnectionString>` | Yes   |
| MongoDb:DatabaseName     | `<NormalizationDatabaseName>` | No  |

### Consumed Events

- **PatientDataAcquired**

### Produced Events

- **PatientNormalized**
- **NotificationRequested**
