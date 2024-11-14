[← Back Home](../README.md)

## Normalization Overview

FHIR resources queried from EHR endpoints can vary from location to location. There will be occasions where data for specific resources may need to be adjusted to ensure that Link Cloud properly evaluates a patient against dQM’s. The Normalization service is a component in Link Cloud to help make those adjustments in an automated way. The service operates in between the resource acquisition and evaluation steps to ensure that the tenant data is in a readied state for measure evaluation.

- **Technology**: .NET Core
- **Image Name**: link-normalization
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Kafka Connection

| Name                                     | Value                     | Secret? |
|------------------------------------------|---------------------------|---------|
| KafkaConnection__BootstrapServers__0     | `<KafkaBootstrapServer>`  | No      |
| KafkaConnection__GroupId                 | normalization-events      | No      |
| KafkaConnection__ClientId                | normalization-events      | No      |

### Database Settings (MSSQL)

| Name                      | Value                         | Secret?  |
|---------------------------|-------------------------------|----------|
| MongoDB__ConnectionString | `<DatabaseConnectionString>`  | Yes      |
| MongoDb__DatabaseName     | `<NormalizationDatabaseName>` | No       |

## Consumed Events

- **PatientDataAcquired**

## Produced Events

- **PatientNormalized**
- **NotificationRequested**
