# Local facility onboarding MCP POC

.NET 8 MCP server, using the official C# MCP SDK and Streamable HTTP.
It is standalone: no Docker, database, Ollama, or other Link projects are required
to start it.
The create_facility tool creates a facility through the existing Tenant API.

## Run

From the repository root, with .NET 8 SDK/runtime (or a newer SDK with .NET 8 runtime):

```powershell
dotnet build DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj
dotnet run --no-build --project DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj
```

Connect an MCP client using Streamable HTTP at `http://127.0.0.1:8090/mcp`.
The server deliberately binds to loopback and restricts allowed host names.
It has no incoming authentication and is intended only for a trusted local machine.
Stop it with Ctrl+C. Alternatively run it in Docker Compose:

```powershell
docker compose up --build -d --wait onboarding-chat
```

This starts both onboarding services, exposing MCP on host loopback port 8090
and chat on port 8091. Stop standalone copies before using the same ports, or
override `ONBOARDING_MCP_PORT` / `ONBOARDING_CHAT_PORT`. Inside Docker, the MCP
server binds port 8080 and accepts the designated `tenant` and `measureeval`
service hosts in addition to loopback. The compose settings supply those URLs.
Optional tokens come from `ONBOARDING_TENANT_BEARER_TOKEN` and
`ONBOARDING_MEASUREEVAL_BEARER_TOKEN`; no credentials are baked into images.

## Tool

`create_facility` requires `facilityId`, `facilityName`, and an IANA `timeZone`
(for example `America/New_York`). Optional `vendorVersionId` is an existing UUID
supplied by the user; omit it or pass an empty string for no vendor version.
It calls Tenant's `POST /api/Facility` with empty reporting schedules. Tenant's
DMRP scheduling behavior still applies. This creates the facility record only;
connections, query plans, and measure enrollment require separate steps.

The tool checks for an existing facility and never updates it. Results use
`created`, `already_exists`, `rejected`, `unavailable`, or `unknown` status.
Only HTTP 201 with a valid response confirms creation. An uncertain POST outcome
is never automatically retried; check Tenant before retrying manually.
Credentials and upstream error bodies are excluded from tool results.

The default Tenant URL is `http://localhost:8074/api/` (local compose).
Override it with `Onboarding__TenantApiBaseUrl` if needed.
Native runs accept only loopback HTTP URLs ending in `/`; redirects are disabled.
For Tenant with authentication enabled, supply an authorized Link admin bearer
token through `Onboarding__BearerToken` in the launching process
environment. Never put tokens into the committed configuration or chatbot prompt.
This POC does not mint tokens or use Azure App Configuration.
Its optional local settings require no environment-store provisioning.

## Look up Luxera endpoints

`search_fhir_endpoints` searches [Luxera's API](https://fhir.luxera.io/docs/api/endpoints)
with `vendor=epic`. `search` matches organization names; do not append city/state.
`pageNumber` defaults to 1 and `pageSize` to 10 (maximum 25). Results include
organization, URL, vendor, location, FHIR version, access types, upstream status and
last-update timestamp, source, retrieval time, and paging metadata. Paging is limited
to the API's first 100,000 matches. The directory is exposed exclusively through search.

Following [Luxera's rate-limit guidance](https://fhir.luxera.io/docs/guides/rate-limits),
the MCP process fetches 100 records per upstream page and serves small chat pages
from a shared one-hour cache (up to 128 upstream pages; oldest entries are evicted
at capacity). Identical concurrent searches share a request. Cache and quota state
are process-local and reset on restart; multiple replicas sharing an IP must coordinate
quota separately. Requests are serialized and paced using `RateLimit-Remaining` and
the Unix timestamp in `RateLimit-Reset`, without assuming a fixed request quota.
Cached pages can be served during a quota cooldown.

On HTTP 429, `Retry-After` (seconds or HTTP date) and any later quota reset determine
the shared cooldown; missing headers use a conservative 60-second pause. There is
no automatic retry. The tool returns `rate_limited` with `retryAfterSeconds`; callers
must wait before searching again. Timeouts, bad responses, and upstream failures
return `unavailable`, distinct from an `available` empty search. Requests have a
20-second timeout and propagate caller cancellation. No API key is required or sent.
Outbound HTTPS access to `fhir-api.luxera.io` is required; the bundled snapshot has
been removed.
Published patient-access URLs do not establish backend-service access or connectivity;
confirm suitability with the facility and use the connection-test tool separately.

## Test a FHIR connection

To read existing settings, call `get_data_acquisition_configuration` with `facilityId`.
It returns `available` with the FHIR base URL, concurrency, retry limit, and facility-local
pull times; authentication details and internal fields are excluded. `not_found` means
the service confirmed no configuration exists. `unavailable` means the read could not
be completed safely, including an ambiguous route-level 404. No settings are changed
and no connection test is performed.

`validate_fhir_server_connection` accepts `fhirServerBaseUrl` and calls
DataAcquisition's `ValidateFhirServerConnection` without saving settings or requiring
a facility record. Supply an absolute HTTP(S) base URL without credentials, query,
or fragment. Results use `connected`, `failed`, or `unavailable`, with `isConnected`,
`httpStatus`, and a safe message. This checks unauthenticated metadata/CapabilityStatement
access only; it does not verify authenticated patient-data access. It uses the same
DataAcquisition API URL and optional bearer token as the save tool below.

## Save data acquisition configuration

`save_data_acquisition_configuration` adds or updates the facility's FHIR connection
configuration through DataAcquisition. Supply `facilityId` and, when creating,
`fhirServerBaseUrl`. Optional settings are `maxConcurrentRequests` (positive),
`maxRetries` (0–10), and `minAcquisitionPullTime` / `maxAcquisitionPullTime`
(`HH:mm:ss` in the facility's time zone). Omitted settings are preserved on update.
Use `clear` to remove a pull-time limit. New configurations default concurrency to 1;
other omitted settings use service defaults.

The tool reads the facility and existing configuration before choosing POST or PUT.
Existing authentication remains private and is preserved; configure credentials
through the normal admin workflow. The tool does not apply query plans. After saving a supplied FHIR URL, it calls
`ValidateFhirServerConnection` and returns `connectionValidation` with status
`connected`, `failed`, or `unavailable`. If no URL is supplied, validation is
`not_requested`. The check requests metadata without authentication; it does not
verify patient-data access. Validation failure does not undo or repeat the save. Updates are read-modify-write without concurrency protection;
avoid concurrent edits to the same facility. Unknown write outcomes require checking
DataAcquisition before a manual retry.

The native API default is `http://localhost:8065/api/data/`.
Override with `Onboarding__DataAcquisitionApiBaseUrl` and supply an authorized token
with `Onboarding__DataAcquisitionBearerToken` when needed. Docker uses `dataacq:8065`
and `ONBOARDING_DATA_ACQUISITION_BEARER_TOKEN`. Tokens never enter tool results.

## Verify the protocol

With the server stopped and port 8090 free, run after building:

```powershell
python DotNet/Onboarding.Mcp/smoke_test.py
```

The test starts isolated mock Tenant/MeasureEval services and an MCP process, initializes an MCP
session, discovers the tools, and exercises structured output, credential redaction,
empty catalogs, independent upstream failures, live measure-definition projection,
ignored legacy manual measure configuration, facility creation, duplicate checks, input validation, and uncertain write outcomes.
It terminates its own server when finished. It does not call your live Tenant.

For interactive inspection, use the official MCP Inspector:

```powershell
npx @modelcontextprotocol/inspector
```

Select Streamable HTTP, enter `http://127.0.0.1:8090/mcp`, connect, list tools,
and list the available tools. Inspector needs Node/npm
and may download its package. See https://modelcontextprotocol.io/docs/tools/inspector.

## Where Ollama fits

Your Ollama endpoint `http://localhost:11434/api` serves the model. It does not
itself act as this server's MCP client. The next POC component is a chatbot host
that connects to both services:

```text
Chatbot host -> Ollama /api/chat (conversation and model tool calls)
Chatbot host -> MCP /mcp (discover tools and execute selected calls)
MCP server   -> Tenant API (read vendor catalogs and create facilities)
```

The sibling [Onboarding.Chat](../Onboarding.Chat/README.md) Blazor project provides
this host. It connects to your local Ollama and this MCP endpoint, discovers tools,
executes model-requested calls, and returns the results to the model. Start both
projects separately; open the chat at `http://127.0.0.1:8091`.

The local DataAcquisition image currently exposes `/api/data/`. Source builds
with the newer routes use `/api/data-acquisition/`; set the API base URL to match
the deployed version. An ambiguous route-level 404 never triggers creation.

### Tenant vendor catalog

The read-only `get_vendors` tool retrieves vendors and vendor versions through the configured Tenant API. It returns vendor IDs and names with nested version IDs and version names, including vendors without versions. Authentication settings are excluded. An empty catalog returns `available` with an empty array; failed requests return `unavailable` without partial results. The onboarding chat can use these version UUIDs for facility associations after the user selects a vendor/version.

