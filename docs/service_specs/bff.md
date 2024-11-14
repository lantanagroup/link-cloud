## Backend For Frontend (BFF)

> ⚠️ **Note:** This service is planned to be renamed to "admin-bff".

### Overview

- **Technology**: .NET Core
- **Image Name**: link-bff
- **Port**: 8080
- **Database**: NONE
- **Scale**: 0-3

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |
| GatewayConfig:KafkaBootstrapServers:0       | `<KafkaBootstrapServer>`      | No      |
| GatewayConfig:AuditServiceApiUrl            | `<URL> (without /api)`        | No      |
| GatewayConfig:NotificationServiceApiUrl     | `<URL> (without /api)`        | No      |
| GatewayConfig:TenantServiceApiUrl           | `<URL> (without /api)`        | No      |
| GatewayConfig:CensusServiceApiUrl           | `<URL> (without /api)`        | No      |
| GatewayConfig:ReportServiceApiUrl           | `<URL> (without /api)`        | No      |
| GatewayConfig:MeasureServiceApiUrl          | `<URL> (without /api)`        | No      |
| IdentityProviderConfig:Issuer               | ??                            | No      |
| IdentityProviderConfig:Audience             | ??                            | No      |
| IdentityProviderConfig:NameClaimType        | email                         | No      |
| IdentityProviderConfig:RoleClaimType        | roles                         | No      |
| IdentityProviderConfig:ValidTypes           | `[ "at+jwt", "JWT" ]`         | No      |
