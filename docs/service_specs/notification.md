[← Back Home](../README.md)

## Notification Overview

The Notification service is responsible for emailing configured users when a notifiable event occurs when the Link Cloud services attempt to perform their work.

- **Technology**: .NET Core
- **Image Name**: link-notification
- **Port**: 8080
- **Database**: MSSQL
- **Scale**: 0-3

See [Notification Functionality](../functionality/notifications.md) for more information on the role of the Notification service in the Link Cloud ecosystem.

## Common Configurations

* [Swagger](../config/csharp.md#swagger)
* [Azure App Configuration](../config/csharp.md#azure-app-config-environment-variables)
* [Kafka Configuration](../config/csharp.md#kafka)
* [Service Registry Configuration](../config/csharp.md#service-registry)
* [CORS Configuration](../config/csharp.md#cors)
* [Token Service Configuration](../config/csharp.md#token-service-settings)
* [Service Authentication](../config/csharp.md#service-authentication)
* [SQL Server Database Configuration](../config/csharp.md#sql-server-database)

## Custom Configurations

### SMTP

| Name                         | Value         | Secret? |
|------------------------------|---------------|---------|
| SmtpConnection__Host         |               | No      |
| SmtpConnection__Port         |               | No      |
| SmtpConnection__EmailFrom    |               | No      |
| SmtpConnection__UseBasicAuth | false or true | No      |
| SmtpConnection__Username     |               | No      |
| SmtpConnection__Password     |               | Yes     |
| SmtpConnection__UseOAuth2    | false or true | No      |

### Channels

| Name                         | Value         | Description                                                                                                        | Secret? |
|------------------------------|---------------|--------------------------------------------------------------------------------------------------------------------|---------|
| Channels__Email              | true or false | Whether or not to use the email channel for notifications                                                          | No      |
| Channels__IncludeTestMessage | true or false | If true, overrides the body and subject of the message with the values from `TestMessage` and `SubjectTestMessage` | No      |
| Channels__TestMessage        | \<string>     | The body of the email to send when configured for testing                                                          | No      |
| Channels__SubjectTestMessage | \<string>     | The subject of the email to send when configured for testing                                                       | No      |

## Kafka Events/Topics

### Consumed Events

- **NotificationRequested**

### Produced Events

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
