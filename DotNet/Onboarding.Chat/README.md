# Link Cloud onboarding chat (local POC)

Blazor Interactive Server chat, generated with Microsoft's **AI Chat Web App**
template and adapted to use the onboarding MCP server instead of document search.
All model calls and MCP connections run on the .NET server. No Python bridge,
cloud AI account, embeddings model, vector database, or document ingestion is needed.

## Start locally

### Docker Compose

With Docker Desktop and Ollama running, from the repository root:

```powershell
docker compose up --build -d --wait onboarding-chat
```

This builds/starts `onboarding-chat` and its `onboarding-mcp` dependency. Open
**http://127.0.0.1:8091**. The MCP endpoint is **http://127.0.0.1:8090/mcp**.
No host .NET SDK is required for this mode. Stop any standalone copies first,
or set `ONBOARDING_CHAT_PORT` / `ONBOARDING_MCP_PORT` to unused host ports.

Ollama remains on the host at `host.docker.internal:11434`; the model defaults to
`gemma4:e2b` and can be changed with `ONBOARDING_CHAT_MODEL`. The MCP container
reads catalogs from `tenant:8074` and `measureeval:8067` on `link-nw`. Those services
can be started with the full stack; the two onboarding containers also start alone
and report unavailable catalog sources when upstream services are absent.

The health checks report application availability, not Ollama/upstream connectivity.
Published ports bind only to host loopback. On Linux, Ollama must listen on an
interface reachable from the Docker bridge; the compose host-gateway mapping alone
does not make a loopback-only Ollama listener reachable. Docker Desktop provides
the `host.docker.internal` host connection. If model requests fail, verify Ollama
is reachable from the chat container before changing the application settings.

Stop just the POC containers with:

```powershell
docker compose stop onboarding-chat onboarding-mcp
```

### Without Docker

Requires the **.NET 10 SDK** for this chat/template and .NET 8 runtime for the MCP
server. Existing pipeline services remain on .NET 8. Ollama must already be running
at `http://localhost:11434` with a model that supports tools. The default is
`gemma4:e2b`, matching the locally installed model at implementation time.

From the repository root, start MCP in one terminal:

```powershell
dotnet run --project DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj
```

If it is already running on port 8090, reuse that instance. In another terminal:

```powershell
dotnet run --project DotNet/Onboarding.Chat/Onboarding.Chat.csproj
```

Open **http://127.0.0.1:8091**. Click **Show onboarding options**, or ask:

> Use get_onboarding_options for the local environment and explain what is available.

The chat streams the model's answer, shows the tool call, and offers expandable
tool results. Follow-up questions retain the full conversation and tool results.
**Stop** cancels the current response. **New chat** clears the in-memory conversation.
Refreshing the page starts a new conversation; nothing is persisted to a database.

Tenant on port 8074 and MeasureEval on port 8067 are optional for starting the POC.
Tenant supplies live vendor/version options; MeasureEval supplies live measure
definitions (IDs and versions). Each source reports its own availability. Measure
definitions are never manually configured. Connection/query-plan profiles are
operator-maintained catalogs in the MCP configuration; initially they are empty.
This POC cannot provision facilities.

## Configuration

Edit `appsettings.json` before starting, or override with environment variables:

| Setting | Default | Environment variable |
|---|---|---|
| Ollama base URL | `http://localhost:11434` | `OnboardingChat__OllamaEndpoint` |
| MCP endpoint | `http://127.0.0.1:8090/mcp` | `OnboardingChat__McpEndpoint` |
| Model | `gemma4:e2b` | `OnboardingChat__Model` |
| Response timeout | 180 seconds | `OnboardingChat__ResponseTimeoutSeconds` |

For example, select another installed tool-capable model in PowerShell:

```powershell
ollama list
$env:OnboardingChat__Model = "your-installed-model-name"
dotnet run --project DotNet/Onboarding.Chat/Onboarding.Chat.csproj
```

Use Ollama's base URL, without `/api`. Native runs accept only loopback HTTP service
URLs. Containers additionally allow the designated `host.docker.internal` Ollama
host and `onboarding-mcp` MCP service name.
The browser UI itself binds only to `127.0.0.1:8091` and uses restricted host names.
These are optional local settings with working defaults; the POC does not consume
Azure App Configuration or require any environment-store provisioning.

Connection failures and timeouts are shown in the chat and can be retried. Failed
or cancelled turns are removed from model history so incomplete tool calls do not
corrupt subsequent requests. A missing model can be installed through Ollama or
selected using the setting above. Large local models may require a longer response
timeout (up to 600 seconds).

## Integration and limits

```text
Browser -> Blazor Interactive Server
             -> OllamaSharp / Microsoft.Extensions.AI -> Ollama /api/chat
             -> official C# MCP client -> Onboarding.Mcp /mcp -> Tenant API
```

Each response opens an MCP session, discovers tools, and exposes only
`get_onboarding_options` to the model. Automatic function invocation is limited
to six iterations. The MCP session stays alive for the entire tool/model loop and
is disposed on completion, failure, or cancellation. A small adapter presents MCP's
nullable `environmentId` schema as an optional string to OllamaSharp 5.x, which
cannot serialize array-valued JSON Schema types. Execution still uses the real MCP tool.

The local UI has no authentication. Use it on a trusted local machine. Do not put
credentials or patient data in chat. The client does not log conversation/tool
content. Production hosting and provisioning writes are outside this POC.

## Verify

```powershell
dotnet build DotNet/Onboarding.Chat/Onboarding.Chat.csproj
dotnet test Tests/Onboarding.Chat.Tests/Onboarding.Chat.Tests.csproj
```

The tests use an isolated HTTP MCP server, a scripted model, and a mock Ollama
HTTP endpoint. They verify tool filtering, actual MCP execution, the OllamaSharp
schema/response round trip, retained tool results on follow-up turns, and cancellation.
They do not require a running Ollama instance or live Link services.

After starting the published Docker chat, check that every startup script and
stylesheet is actually served (the health endpoint alone cannot detect missing
Blazor assets):

```powershell
python Tests/Onboarding.Chat.Tests/published_smoke_test.py
```

The project explicitly sets `RequiresAspNetWebAssets` so Docker's project-only
restore includes the Blazor runtime assets before the Razor source files are copied.

## Template provenance

Generated using `Microsoft.Extensions.AI.Templates` version
`10.11.0-preview.3.26513.7`:

```powershell
dotnet new aichatweb --Framework net10.0 --provider ollama --vector-store local --ChatModel gemma4:e2b -n Onboarding.Chat -o DotNet/Onboarding.Chat
```

Retains the template's chat layout, input behavior, auto-scroll, and sanitized
Markdown renderer. Removes the sample documents, document-search tools, ingestion
services, survey, and extra model-generated suggestions.

References: [Microsoft AI template](https://learn.microsoft.com/dotnet/ai/quickstarts/ai-templates),
[official C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk).
