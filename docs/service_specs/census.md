[← Back Home](../README.md)

## Census

- **Technology**: .NET Core
- **Image Name**: link-census
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

### App Settings

| Name                                       | Value                         | Secret? |
|--------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource   | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration   | `<AzureAppConfigEndpoint>`    | Yes     |

### Kafka Connection

| Name                                    | Value                | Secret? |
|-----------------------------------------|----------------------|---------|
| KafkaConnection:BootstrapServers:0       | `<KafkaBootstrapServer>` | No  |
| KafkaConnection:GroupId                  | census-events        | No      |
| KafkaConnection:ClientId                 | census-events        | No      |

### Tenant API Settings

| Name                          | Value                           | Secret? |
|-------------------------------|---------------------------------|---------|
| TenantApiSettings:TenantServiceBaseEndpoint | `<TenantApiUrl>/api` | No      |

### Database Settings (MSSQL)

| Name                    | Value                | Secret? |
|-------------------------|----------------------|---------|
| MongoDB:ConnectionString | `<ConnectionString>` | Yes     |
| MongoDb:DatabaseName     | `<DatabaseName>`     | No      |
| MongoDb:CollectionName   | `census`             | No      |

## Consumed Events

- **Event**: `PatientIDsAcquired`

## Produced Events

- **Event**: `PatientCensusScheduled`