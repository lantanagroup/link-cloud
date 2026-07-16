# NHSN-App-BFF

## Overview

`NHSN-App-BFF` is the backend-for-frontend foundation for the NHSN App integration proof-of-concept described by the `nhsn-app-web-component-poc` ADR.

This project is intentionally separate from the existing Link `Account` service. The Account service governs Link Admin UI access, while `NHSN-App-BFF` owns its own user-resolution and role-association model specifically for the NHSN App framework.

## Core responsibilities

- Validate incoming JWTs using a configured public certificate.
- Require JWT bearer authentication for all authenticated NHSNLink operations.
- Resolve the effective user from the authenticated JWT principal.
- Persist and manage NHSNLink-specific user records in SQL Server.
- Persist and manage NHSNLink-specific role associations in SQL Server.
- Expose a `/userinfo` endpoint that returns the normalized UI initialization payload.

## Why this project exists

The NHSN App integration needs a backend that can answer a question like:

> “Who is the current NHSNLink user and what role should they have inside NHSNLink?”

That cannot be solved entirely in the UI because:

- the App Gateway injects the JWT on the backend request path,
- the NHSNLink roles are owned by the BFF/database, not by the UI,
- the UI needs a server-shaped user context anyway.

As a result, the UI initializes from `/api/nhsn-app-bff/userinfo`, not from direct JWT parsing.

## Data model

The initial SQL schema is intentionally small:

### `Users`
- `Id`
- `ExternalUserId`
- `Email`
- `Name`
- `GroupsRaw`
- `FacilityId`
- `IsOnboarded`
- `IsActive`
- audit fields

### `Roles`
- `Id`
- `Name`
- `Description`

Seeded roles:
- `System Admin`
- `Facility Admin`
- `Facility IT`

### `UserRoles`
- `UserId`
- `RoleId`

## JWT validation

JWT validation is performed against a configured public certificate and issuer.

### Initial approach
The public certificate can be stored in configuration (`appsettings.json`, `appsettings.Development.json`, or `appsettings.Docker.json`) using the `NhsnJwt:PublicCertificatePem` setting.

### Future-ready design
The configuration model is designed so the certificate source can later move to Azure App Configuration and/or Azure Key Vault without changing the authentication flow.

## Lower-environment signed JWT flow

The standalone `NHSN-App-UI` shell can mint lower-environment bearer JWTs using a configured private key and issuer. Those JWTs are sent to the BFF through the normal `Authorization: Bearer ...` header so lower-environment testing follows the same authentication shape used in production.

## `/userinfo` endpoint contract

Endpoint:

- `GET /api/nhsn-app-bff/userinfo`

Expected response includes:

- `Email`
- `Name`
- `Roles`
- `IsOnboarded`
- `FacilityId`
- `Groups`
- `AvailableNavigation`

The UI uses this to determine whether to render onboarding or maintenance navigation.

## Configuration

### Required app settings

### Azure App Configuration support

This project follows the same platform pattern used by other backend services and already calls:

- `builder.AddExternalConfiguration(NhsnAppConstants.ServiceName)`

in `Program.cs`.

That is the standard extension method used across the platform to enable Azure App Configuration support.

To enable Azure App Configuration for this service:

- set `ExternalConfigurationSource` to `AzureAppConfiguration`
- provide `ConnectionStrings:AzureAppConfiguration`
- optionally configure `SecretManagement`/Key Vault-related values the same way other platform services do

This allows the BFF to start with local file-based configuration and later move to centralized configuration without changing the service startup flow.

#### Connection string
- `ConnectionStrings:DatabaseConnection`
- `ConnectionStrings:AzureAppConfiguration` (when using Azure App Configuration)

#### External configuration source
- `ExternalConfigurationSource`

#### Secret management
- `SecretManagement:Manager`
- `SecretManagement:ManagerUri`

#### JWT settings
- `NhsnJwt:PublicCertificatePem`
- `NhsnJwt:Issuer`
- optional `NhsnJwt:Audience`
- claim mapping keys such as `EmailClaimType`, `UserIdClaimType`, etc.

## EF Core migrations

This project includes an initial EF migration that creates the foundational schema for:

- users
- roles
- user_roles

The repo guidance requires migrations for persisted EF entities, so schema changes should always be accompanied by a new migration.

## Running locally

### Development
1. Set `ConnectionStrings:DatabaseConnection`
2. Optionally set `ExternalConfigurationSource=AzureAppConfiguration` and `ConnectionStrings:AzureAppConfiguration` if you want to use centralized configuration
3. Ensure `NhsnJwt:Issuer` and `NhsnJwt:PublicCertificatePem` are configured
4. Run the project
5. Call `/api/nhsn-app-bff/userinfo` with a signed bearer JWT

### Migration behavior

This service follows the same `AutoMigrate` pattern used by other SQL-backed Link services.

- `AutoMigrate: true` in development/docker enables startup execution of EF migrations
- `AutoMigrate: false` can be used in environments where schema is managed separately

The BFF calls `app.AutoMigrateEF<NhsnAppDbContext>()` before seed logic runs, so the `Roles`, `Users`, and `UserRoles` tables should exist before seed data is inserted when `AutoMigrate` is enabled.

## Relationship to the UI project

`NHSN-App-UI` does not own roles. It asks this service for user context.

That means the component startup flow is:

1. render `NHSNLink`
2. call `/userinfo`
3. receive normalized context
4. render onboarding/maintenance navigation based on BFF-owned state

## Foundation intent

This project is a framework/foundation, not the full facility configuration product. It establishes:

- authentication shape
- identity resolution
- role persistence
- the UI initialization contract

Future work can expand this foundation with richer onboarding state, facility relationships, audit history, and configuration orchestration.
