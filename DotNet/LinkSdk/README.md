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
| `MockDmrpServiceClient` | Mock DMRP API support surface (`/api/mock-dmrp`) | `IMockDmrpServiceClient` |

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

`MockDmrpServiceClient` is not part of `AddLinkSdk()`. The mock's address is held in different places by
different callers (Tenant uses `DMRP:Api:BaseUrl`, Automation uses `ServiceRegistry:MockDmrpApiUrl`), so each
registers it with the mock's root address:

```csharp
builder.Services.AddMockDmrpServiceClient(sp => /* the mock's root URL */);
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

## Validation reporting

- `GetReportCategoryCountsAsync(facilityId, reportId, submit, cancellationToken)` reads stored category counts.
- `GetCategoryIssuesAsync(facilityId, reportId, categoryId, pageNumber, pageSize, cancellationToken)`
  returns JSON containing `records` and `metadata`. Paging starts at 1; page size is 1–100.
  Empty pages preserve the successful 200 response and metadata.
- `CopyValidationResultsCsvAsync(facilityId, reportId, destination, cancellationToken)` copies the CSV download
  directly to a writable stream, leaving it open. It uses headers-first HTTP completion and disables
  the normal request timeout; supply a cancellation token with the desired download deadline.
  Error status/Problem Details are returned without writing them into the destination. Exceptions
  or cancellation during transfer may leave partial output, so use a temporary file and rename it
  only after success. The CSV contains saved findings only, one row per finding/category pair.

```csharp
await using var file = File.Create("results.csv.partial");
var response = await validationClient.CopyValidationResultsCsvAsync(facilityId, reportId, file, cancellationToken);
// Check response.IsSuccessStatusCode before accepting the downloaded file.
```
