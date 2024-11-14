[← Back Home](../README.md)

## Notification

### Overview

- **Technology**: .NET Core
- **Image Name**: link-notification
- **Port**: 8080
- **Database**: MSSQL
- **Scale**: 0-3

### Environment Variables

| Name                                                     | Value                         | Secret? |
|----------------------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource                 | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration                 | `<AzureAppConfigEndpoint>`    | Yes     |
| Link:Notification:ServiceRegistry:TenantServiceApiUrl    | `<TenantServiceUrl>`          | No      |
| Link:Notification:KafkaConnection:BootstrapServers:0     | `<KafkaBootstrapServer>`      | No      |
| Link:Notification:KafkaConnection:GroupId                | notification-events           | No      |
| Link:Notification:KafkaConnection:ClientId               | notification-events           | No      |
| Link:Notification:SmtpConnection:Host                    |                               | No      |
| Link:Notification:SmtpConnection:Port                    |                               | No      |
| Link:Notification:SmtpConnection:EmailFrom               |                               | No      |
| Link:Notification:SmtpConnection:UseBasicAuth            | false or true                 | No      |
| Link:Notification:SmtpConnection:Username                |                               | No      |
| Link:Notification:SmtpConnection:Password                |                               | Yes     |
| Link:Notification:SmtpConnection:UseOAuth2               | false or true                 | No      |
| Link:Notification:EnableSwagger                          | true (DEV and TEST)           | No      |

### Consumed Events

- **NotificationRequested**

### Produced Events

- **AuditableEventOccurred**
