# LinkSdk

`LinkSdk` provides Flurl-based API clients for Link service interactions using a shared `LinkApiClientBase` pattern with automatic bearer token authentication.

## Clients

| Client | Service | Interface |
|---|---|---|
| `FacilityServiceClient` | Tenant (Facility, Vendor, VendorVersion) | `IFacilityServiceClient` |
| `CensusServiceClient` | Census | `ICensusServiceClient` |
| `DataAcquisitionServiceClient` | Data Acquisition | `IDataAcquisitionServiceClient` |
| `NormalizationServiceClient` | Normalization | `INormalizationServiceClient` |
| `QueryDispatchServiceClient` | Query Dispatch | `IQueryDispatchServiceClient` |
| `ReportServiceClient` | Report | `IReportServiceClient` |
| `MeasureEvalServiceClient` | MeasureEval (Java) | `IMeasureEvalServiceClient` |
| `ValidationServiceClient` | Validation (Java) | `IValidationServiceClient` |
| `SubmissionServiceClient` | Submission | `ISubmissionServiceClient` |

## Design

- Uses `Flurl.Http` for request execution.
- `LinkApiClientBase` centralizes base URL resolution, JSON serialization configuration, and bearer token attachment.
- Each client resolves its own base URL from `ServiceRegistry` (bound from configuration).
- Supports automatic bearer token authentication via `ICreateSystemToken` and `LinkTokenServiceSettings.SigningKey`.
- Anonymous mode is supported via `LinkBearerServiceOptions.AllowAnonymous` for development environments.
- `GetOrDefaultAsync<T>` returns `null` on 404 instead of throwing — standard pattern for existence checks.

## DI registration

Use `AddLinkSdk()` from `LantanaGroup.Link.Sdk.DependencyInjection` to register all clients:

```csharp
builder.Services.AddLinkSdk();
```

### Prerequisites in DI

- `IOptions<ServiceRegistry>` — service base URLs
- `IOptions<LinkBearerServiceOptions>` — anonymous access flag
- `IOptions<LinkTokenServiceSettings>` — signing key for token generation
- `ICreateSystemToken` — token creation service

## Consumers

- `DotNet/Automation.Link` — pipeline orchestration and validation
- `DotNet/Automation.UI` — interactive web UI for automation runs
- `Tests/BackendE2ETests` — E2E test suites

## Notes

- Targets `.NET 8`.
- All clients are registered as singletons (Flurl clients are thread-safe).
