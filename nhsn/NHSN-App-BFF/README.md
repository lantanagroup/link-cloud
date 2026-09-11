# NHSN-App-BFF

## Overview

`NHSN-App-BFF` is the backend-for-frontend foundation for the NHSN App integration proof-of-concept described by the `nhsn-app-web-component-poc` ADR.

This project is intentionally separate from the existing Link `Account` service. The Account service governs Link Admin UI access, while `NHSN-App-BFF` resolves facility-facing NHSN App user context for the embedded NHSNLink experience.

## Core responsibilities

- Validate incoming JWTs using a configured public certificate.
- Require JWT bearer authentication for all authenticated NHSNLink operations.
- Resolve the effective user from the authenticated JWT principal.
- Persist minimal NHSNLink user-observation records in SQL Server for auditability and troubleshooting.
- Derive FACADMIN authorization and facility context from the validated JWT.
- Persist mutable onboarding state for facilities.
- Expose a `/userinfo` endpoint that returns the normalized UI initialization payload.

## Why this project exists

The NHSN App integration needs a backend that can answer a question like:

> Who is the current NHSNLink user and do they have facility-facing access inside NHSNLink?

That cannot be solved entirely in the UI because:

- the App Gateway injects the JWT on the backend request path,
- the FACADMIN role and facility context come from the validated JWT,
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
- `LastAccessedOn`
- audit fields

### `Facilities`
- `Id`
- `FacilityId`
- `IsOnboarded`

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

- `AccessState`
- `Email`
- `Name`
- `IsFacilityAdmin`
- `IsOnboarded`
- `FacilityId`
- `Groups`
- `AvailableNavigation`

The UI uses this to determine whether to render no-access, missing-facility, onboarding, or configuration states.

## Localization API and runtime resources

The BFF serves localization resources to the UI at runtime so language files can be updated without rebuilding UI assets.

### Endpoint

- `GET /api/localization/{locale}/{namespaceName}`

Supported namespaces:

- `common`
- `onboarding`
- `configuration`

Default source-controlled fallback files are stored in:

- `Localization/en-US/common.json`
- `Localization/en-US/onboarding.json`
- `Localization/en-US/configuration.json`

### Configuration

- `Localization:ResourceDirectory`
- Environment override: `Localization__ResourceDirectory`

Default value is `Localization` (relative path). Relative paths are resolved from application content root; absolute paths are also supported.

Fallback chain behavior:

- requested locale (for example `es-MX`)
- neutral locale when available (for example `es`)
- final fallback `en-US`

If a translated key is missing in the requested locale, the English value from `en-US` is used.

### Container mounted-volume override

When `Localization__ResourceDirectory` points to a mounted path, files in that path become the active runtime resources.

```yaml
env:
  - name: Localization__ResourceDirectory
    value: /app/localization

volumeMounts:
  - name: localization
    mountPath: /app/localization
    readOnly: true

volumes:
  - name: localization
    configMap:
      name: nhsnlink-localization
```

For larger independently managed resource sets, a persistent volume or other mounted source may be preferable to a ConfigMap.

### Adding localization content

1. Add a locale folder (for example `Localization/es-MX/`).
2. Add namespace files (`common.json`, `onboarding.json`, `configuration.json`) using the same key structure as `en-US`.
3. Keep JSON payloads as objects and do not include executable/script content.
4. For additional namespaces beyond the initial three, update both backend allow-list handling and UI namespace configuration together.

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
- optional `NhsnJwt:MaxTokenAgeMinutes`
- optional `NhsnJwt:ExpiredTokenRedirectUrl`
- claim mapping keys such as `EmailClaimType`, `UserIdClaimType`, etc.

### Token renewal behavior

The BFF validates JWT signature, issuer, lifetime, and optionally a maximum token age when `NhsnJwt:MaxTokenAgeMinutes` is configured.

If the JWT is expired or exceeds the configured maximum token age, the BFF can emit a configurable renewal redirect URL using `NhsnJwt:ExpiredTokenRedirectUrl`.

- If the configured URL contains `{redirectUrl}`, the BFF replaces that token with the current request URL.
- Otherwise, the BFF appends `redirectUrl=<current request url>` as a query parameter.

## EF Core migrations

This project includes EF migrations that create and evolve the foundational schema for:

- users
- facilities

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

The BFF calls `app.AutoMigrateEF<NhsnAppDbContext>()` before seed logic runs, so the `Users` and `Facilities` tables should exist before seed data is inserted when `AutoMigrate` is enabled.

## Relationship to the UI project

`NHSN-App-UI` does not own roles. It asks this service for normalized facility-facing user context.

That means the component startup flow is:

1. render `NHSNLink`
2. call `/userinfo`
3. receive normalized context
4. render no-access, onboarding, or configuration behavior based on JWT-derived access and facility onboarding state

## Foundation intent

This project is a framework/foundation, not the full facility configuration product. It establishes:

- authentication shape
- identity resolution
- FACADMIN/facility-context interpretation
- facility onboarding state
- the UI initialization contract

Future work can expand this foundation with richer onboarding state, facility relationships, audit history, and configuration orchestration.
