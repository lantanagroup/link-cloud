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

## API Operations

The **Notification** service provides REST endpoints for managing notifications and their configurations. These endpoints enable users to create, retrieve, update, and delete notifications and configurations for specific facilities.

- **GET /api/notification/info**: Retrieve general information about the notification service.
- **GET /api/notification**: Retrieve a list of notifications based on filters, with pagination and sorting options.
- **GET /api/notification/{id}**: Retrieve a specific notification by its unique ID.
- **GET /api/notification/facility/{facilityId}**: Retrieve notifications associated with a specific facility, with pagination and sorting options.
- **GET /api/notification/configuration**: Retrieve a list of notification configurations with filters, pagination, and sorting options.
- **GET /api/notification/configuration/facility/{facilityId}**: Retrieve the notification configuration for a specific facility by `facilityId`.
- **GET /api/notification/configuration/{id}**: Retrieve a specific notification configuration by its unique ID.
- **POST /api/notification/configuration**: Create a new notification configuration.
- **PUT /api/notification/configuration**: Update an existing notification configuration.
- **DELETE /api/notification/configuration/{id}**: Delete a notification configuration by its unique ID.

Each operation supports the management of notification delivery and configurations, ensuring timely and accurate communication across facilities and workflows.
