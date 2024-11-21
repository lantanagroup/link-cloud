[← Back Home](../README.md)

## Notification Overview

The Notification service is responsible for emailing configured users when a notifiable event occurs when the Link Cloud services attempt to perform their work.

- **Technology**: .NET Core
- **Image Name**: link-notification
- **Port**: 8080
- **Database**: MSSQL
- **Scale**: 0-3

## Environment Variables

| Name                                                     | Value                         | Secret? |
|----------------------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource                 | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration                 | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Service Endpoints

| Name                                                        | Value                         | Secret? |
|-------------------------------------------------------------|-------------------------------|---------|
| Link__Notification__ServiceRegistry__TenantServiceApiUrl    | `<TenantServiceUrl>`          | No      |

### Kafka

| Name                                                        | Value                         | Secret? |
|-------------------------------------------------------------|-------------------------------|---------|
| Link__Notification__KafkaConnection__BootstrapServers__0    | `<KafkaBootstrapServer>`      | No      |
| Link__Notification__KafkaConnection__GroupId                | notification-events           | No      |
| Link__Notification__KafkaConnection__ClientId               | notification-events           | No      |

### SMTP

| Name                                                        | Value                         | Secret? |
|-------------------------------------------------------------|-------------------------------|---------|
| Link__Notification__SmtpConnection__Host                    |                               | No      |
| Link__Notification__SmtpConnection__Port                    |                               | No      |
| Link__Notification__SmtpConnection__EmailFrom               |                               | No      |
| Link__Notification__SmtpConnection__UseBasicAuth            | false or true                 | No      |
| Link__Notification__SmtpConnection__Username                |                               | No      |
| Link__Notification__SmtpConnection__Password                |                               | Yes     |
| Link__Notification__SmtpConnection__UseOAuth2               | false or true                 | No      |

### Additional Settings

| Name                                                        | Value                         | Secret? |
|-------------------------------------------------------------|-------------------------------|---------|
| Link__Notification__EnableSwagger                           | true (DEV and TEST)           | No      |

## Consumed Events

- **NotificationRequested**

## Produced Events

- **AuditableEventOccurred**
