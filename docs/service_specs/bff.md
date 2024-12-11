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

## API Operations

The **BFF** service provides REST endpoints to support user authentication, session management, and integration testing. These endpoints serve as a bridge between the frontend and backend systems.

### Available REST Operations

#### Authentication and Session Management

- **GET /api/login**: Initiates the login process for Link.
- **GET /api/user**: Retrieves information about the currently logged-in user.
- **GET /api/logout**: Logs out the currently logged-in user.
- **GET /api/auth/token**: Generates a bearer token for the current user to interact with Link services.
- **GET /api/auth/refresh-key**: Refreshes the signing key used for Link bearer tokens.

#### Integration Testing

- **POST /api/integration/patient-event**: Produces a patient event for testing purposes.
- **POST /api/integration/report-scheduled**: Produces a report scheduled event for testing purposes.
- **POST /api/integration/data-acquisition-requested**: Produces a data acquisition requested event for testing purposes.

### Service Information

- **GET /api/info**: Retrieves basic service information for the BFF service.

These operations enable robust session management, integration testing, and backend connectivity for frontend applications.