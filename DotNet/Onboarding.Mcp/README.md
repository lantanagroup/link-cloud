# Local facility onboarding MCP POC

Read-only .NET 8 MCP server, using the official C# MCP SDK and Streamable HTTP.
It is standalone: no Docker, database, Ollama, or other Link projects are required
to start it. Tenant supplies live vendors/versions; MeasureEval supplies live measure definitions.
No facility configuration is changed.

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

`get_onboarding_options` accepts `{}` to list all configured environments or
`{"environmentId":"local"}` to select one (case insensitive).
Unknown IDs return an MCP tool error. Results include structured content plus
the SDK's text representation.

- Environments and connection/query-plan profiles come from the local
  `Onboarding` configuration section. Profiles are catalog entries only at this
  stage; no provisioning templates are applied.
- Measure definitions are fetched live from MeasureEval's
  `GET /api/measureeval/measure-definition` endpoint. `measureDefinitions` contains
  definition IDs and versions from its summary response, with source `measureeval_api`.
  They are never manually configured. FHIR Bundles are not included in tool results.
- Vendors and vendor versions are read from Tenant's existing `GET /api/Vendor`
  and `GET /api/VendorVersion` endpoints. Only IDs, names, and versions are returned;
  upstream authentication settings are never exposed.
- A successful empty Tenant catalog is `available` with empty `options`.
  HTTP failures, timeouts, and invalid responses are `unavailable`, yielding an
  overall `partial` result. A `complete` result means both Tenant reads and the MeasureEval read succeeded,
  not that the facility is ready for onboarding.
- Local catalogs start empty (`not_configured`) so there are no fabricated choices.

Edit `appsettings.json` and rebuild to add approved local catalog entries:

```json
"ConnectionProfiles": [{"Id":"your-profile-id","DisplayName":"Your approved connection"}],
"QueryPlanProfiles": [{"Id":"your-plan-id","DisplayName":"Your approved query plan"}]
```

Each entry must be a JSON object, not a string containing escaped JSON. Invalid
entries now fail startup with a configuration validation error instead of silently
appearing as an empty catalog. After changing the source file in Docker mode, run
`docker compose up --build -d --wait onboarding-mcp` to rebuild and reload the MCP
container. Start a new chat or request fresh onboarding options to see the update.

The default Tenant URL is `http://localhost:8074/api/` (local compose).
Override it with `Onboarding__Environments__0__TenantApiBaseUrl` if needed.
Native runs accept only loopback HTTP URLs ending in `/`; redirects are disabled.
MeasureEval defaults to `http://localhost:8067/api/measureeval/`; override with
`Onboarding__Environments__0__MeasureEvalApiBaseUrl`. If authentication is enabled,
set `Onboarding__Environments__0__MeasureEvalBearerToken` to an authorized token.
MeasureEval failures are reported independently of Tenant failures; a successful
empty list is `available` with empty `options`.
For Tenant with authentication enabled, supply an authorized Link admin bearer
token through `Onboarding__Environments__0__BearerToken` in the launching process
environment. Never put tokens into the committed configuration or chatbot prompt.
This POC does not mint tokens or use Azure App Configuration.
Its optional local settings require no environment-store provisioning.

## Verify the protocol

With the server stopped and port 8090 free, run after building:

```powershell
python DotNet/Onboarding.Mcp/smoke_test.py
```

The test starts isolated mock Tenant/MeasureEval services and an MCP process, initializes an MCP
session, discovers the tool, and exercises structured output, credential redaction,
empty catalogs, independent upstream failures, live measure-definition projection,
ignored legacy manual measure configuration, and unknown environment errors.
It terminates its own server when finished. It does not call your live Tenant.

For interactive inspection, use the official MCP Inspector:

```powershell
npx @modelcontextprotocol/inspector
```

Select Streamable HTTP, enter `http://127.0.0.1:8090/mcp`, connect, list tools,
and call `get_onboarding_options` with no arguments. Inspector needs Node/npm
and may download its package. See https://modelcontextprotocol.io/docs/tools/inspector.

## Where Ollama fits

Your Ollama endpoint `http://localhost:11434/api` serves the model. It does not
itself act as this server's MCP client. The next POC component is a chatbot host
that connects to both services:

```text
Chatbot host -> Ollama /api/chat (conversation and model tool calls)
Chatbot host -> MCP /mcp (discover tools and execute selected calls)
MCP server   -> Tenant API (read vendor catalogs)
```

The sibling [Onboarding.Chat](../Onboarding.Chat/README.md) Blazor project provides
this host. It connects to your local Ollama and this MCP endpoint, discovers tools,
executes model-requested calls, and returns the results to the model. Start both
projects separately; open the chat at `http://127.0.0.1:8091`.
