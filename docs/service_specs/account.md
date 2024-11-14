[← Back Home](../README.md)

## Overview

- **Technology**: .NET Core
- **Image Name**: link-account
- **Port**: 8080
- **Database**: MSSQL (previously Postgres)

## Environment Variables

### App Config

| Name                                     | Value                           | Secret? |
|------------------------------------------|---------------------------------|---------|
| ExternalConfigurationSource              | AzureAppConfiguration           | No      |
| ConnectionStrings__AzureAppConfiguration | `<AzureAppConfigEndpoint>`      | Yes     |

### Kafka Connection

| Name                               | Value                    | Secret? |
|------------------------------------|--------------------------|---------|
| KafkaConnection:BootstrapServers:0  | `<KafkaBootstrapServer>` | No      |
| KafkaConnection:GroupId             | Account                  | No      |

### Database Settings (MSSQL)

| Name                   | Value                | Secret? |
|------------------------|----------------------|---------|
| Postgres:ConnectionString | `<ConnectionString>` | Yes   |

### Tenant API Settings

| Name                                       | Value                              | Secret? |
|--------------------------------------------|------------------------------------|---------|
| TenantApiSettings:TenantServiceBaseEndpoint | `<TenantServiceUrl>/api`          | No      |

## Consumed Events

- **NONE**

## Produced Events

- **Event**: `AuditableEventOccurred`