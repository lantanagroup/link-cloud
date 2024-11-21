[← Back Home](../README.md)

## Account Overview

The Account service is responsible for maintaining roles and permissions for Link Cloud users.

- **Technology**: .NET Core
- **Image Name**: link-account
- **Port**: 8080
- **Database**: MSSQL (previously Postgres)

## Environment Variables

| Name                                     | Value                           | Secret? |
|------------------------------------------|---------------------------------|---------|
| ExternalConfigurationSource              | AzureAppConfiguration           | No      |
| ConnectionStrings__AzureAppConfiguration | `<AzureAppConfigEndpoint>`      | Yes     |

## App Config

### Kafka Connection

| Name                                 | Value                     | Secret?  |
|--------------------------------------|---------------------------|----------|
| KafkaConnection__BootstrapServers__0 | `<KafkaBootstrapServer>`  | No       |
| KafkaConnection__GroupId             | Account                   | No       |

### Redis

| Name                     | Value                     | Secret? |
|--------------------------|---------------------------|--------|
| ConnectionStrings__Redis | `<RedisConnectionString>` | Yes    |
| Cache__Enabled           | true/false                | No     |

### Database Settings (MSSQL)

| Name                                  | Value                 | Secret?  |
|---------------------------------------|-----------------------|----------|
| ConnectionStrings__DatabaseConnection | `<ConnectionString>`  | Yes      |

### Tenant API Settings

| Name                                          | Value                               | Secret? |
|-----------------------------------------------|-------------------------------------|---------|
| TenantApiSettings__TenantServiceBaseEndpoint  | `<TenantServiceUrl>/api`            | No      |

## Consumed Events

- **NONE**

## Produced Events

- **Event**: `AuditableEventOccurred`