[← Back Home](../README.md)

## Census Overview

The Census service is primarily responsible for maintaining a tenants admit and discharge patient information needed to determine when a patient is ready for reporting. To accomplish this, the Census service has functionality in place to request an updated FHIR List of recently admitted patients. The frequency that the request is made is based on a Tenant configuration made in the Census service.

- **Technology**: .NET Core
- **Image Name**: link-census
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

| Name                                       | Value                         | Secret? |
|--------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource   | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration   | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Kafka Connection

| Name                                    | Value                    | Secret?  |
|-----------------------------------------|--------------------------|----------|
| KafkaConnection__BootstrapServers__0    | `<KafkaBootstrapServer>` | No       |
| KafkaConnection__GroupId                | census-events            | No       |
| KafkaConnection__ClientId               | census-events            | No       |

### Tenant API Settings

| Name                                         | Value                  | Secret? |
|----------------------------------------------|------------------------|---------|
| TenantApiSettings__TenantServiceBaseEndpoint | `<TenantApiUrl>/api`   | No      |

### Database Settings (MSSQL)

| Name                      | Value                | Secret? |
|---------------------------|----------------------|---------|
| MongoDB__ConnectionString | `<ConnectionString>` | Yes     |
| MongoDb__DatabaseName     | `<DatabaseName>`     | No      |
| MongoDb__CollectionName   | `census`             | No      |

## Consumed Events

- **Event**: `PatientIDsAcquired`

## Produced Events

- **Event**: `PatientCensusScheduled`