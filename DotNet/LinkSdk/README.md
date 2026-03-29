# LinkSdk

`LinkSdk` provides Flurl-based API clients for Link service interactions using a shared `ApiClientSettings` and a base `LinkApiClientBase` pattern.

## Current clients

- `FacilityServiceClient`
- `NormalizationServiceClient`
- `DataAcquisitionServiceClient`
- `ReportServiceClient`
- `ValidationServiceClient`

These clients currently cover the controller endpoints used by `DotNet/Automation`.

## Design

- Uses `Flurl.Http` for request execution.
- Uses an ApiClient pattern (`LinkApiClientBase`) to centralize base URL and auth header setup.
- Supports bearer token authentication via `ApiClientSettings.BearerToken`.

## DI

Use `AddLinkSdk(ApiClientSettings)` from `LantanaGroup.Link.Sdk.DependencyInjection` to register all clients.
