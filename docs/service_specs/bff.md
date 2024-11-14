[← Back Home](../README.md)

> ⚠️ **Note:** This service is planned to be renamed to "admin-bff".

## Backend for Frontend (BFF) Overview

- **Technology**: .NET Core
- **Image Name**: link-bff
- **Port**: 8080
- **Database**: NONE
- **Scale**: 0-3

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Service Endpoints

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| GatewayConfig__KafkaBootstrapServers__0      | `<KafkaBootstrapServer>`       | No       |
| GatewayConfig__AuditServiceApiUrl            | `<URL> (without /api)`         | No       |
| GatewayConfig__NotificationServiceApiUrl     | `<URL> (without /api)`         | No       |
| GatewayConfig__TenantServiceApiUrl           | `<URL> (without /api)`         | No       |
| GatewayConfig__CensusServiceApiUrl           | `<URL> (without /api)`         | No       |
| GatewayConfig__ReportServiceApiUrl           | `<URL> (without /api)`         | No       |
| GatewayConfig__MeasureServiceApiUrl          | `<URL> (without /api)`         | No       |

### Identity Provider

| Name                                         | Value                          | Secret?  |
|----------------------------------------------|--------------------------------|----------|
| IdentityProviderConfig__Issuer               | ??                             | No       |
| IdentityProviderConfig__Audience             | ??                             | No       |
| IdentityProviderConfig__NameClaimType        | email                          | No       |
| IdentityProviderConfig__RoleClaimType        | roles                          | No       |
| IdentityProviderConfig__ValidTypes           | `[ "at+jwt", "JWT" ]`          | No       |
