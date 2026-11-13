# Link Cloud onboarding chat (local POC)

Blazor Interactive Server chat, generated with Microsoft's **AI Chat Web App**
template and adapted to use the onboarding MCP server instead of document search.
All model calls and MCP connections run on the .NET server. No Python bridge,
cloud AI account, embeddings model, vector database, or document ingestion is needed.

## Start locally

### Docker Compose

With Docker Desktop running, from the repository root (no host Ollama install needed):

```powershell
docker compose up --build -d --wait onboarding-chat
```

This starts Ollama, installs the selected model if it is not cached, and builds/starts
`onboarding-chat` and `onboarding-mcp`. The first model download can take several
minutes. Open
**http://127.0.0.1:8091**. The compose MCP endpoint is **http://127.0.0.1:8092/mcp**
(native MCP uses port 8090, also used by the NHSN UI).
No host .NET SDK is required for this mode. Stop any standalone copies first,
or set `ONBOARDING_CHAT_PORT` / `ONBOARDING_MCP_PORT` to unused host ports.

The chat connects directly to `http://ollama:11434` on the Docker network. Ollama is
also available to native clients at `http://localhost:11434`. Stop/quit any host
Ollama app first to free this port, and disable its launch at login if enabled.
Alternatively, set `OLLAMA_PORT` to an unused host port; the container connection
stays the same. The model defaults to `gemma4:12b` and can be changed with
`ONBOARDING_CHAT_MODEL`; rerun the startup command after changing it.
Models persist in the `ollama_data` named volume across container recreation.
`docker compose down -v` deletes that cache along with the other stack volumes.
The MCP container uses Tenant and DataAcquisition on `link-nw`. Those services can be started with
the full stack; the onboarding services and Ollama also start alone.

Compose waits for Ollama's API and the model installation before starting the chat.
The chat/MCP health checks report application availability, not upstream connectivity.
Published ports bind only to host loopback. The base setup uses CPU inference.
For an NVIDIA GPU with Docker GPU support enabled, use the override on each start:

```powershell
docker compose -f docker-compose.yml -f docker-compose.ollama-gpu.yml up --build -d --wait onboarding-chat
```

To keep GPU acceleration when using plain `docker compose` commands, add these
optional settings to your local `.env` (also shown in `.env.example`):

```dotenv
COMPOSE_PATH_SEPARATOR=,
COMPOSE_FILE=docker-compose.yml,docker-compose.ollama-gpu.yml
```

The override follows [Docker's GPU reservation setup](https://docs.docker.com/compose/how-tos/gpu-support/)
for the [official Ollama image](https://docs.ollama.com/docker).
Ollama uses an 8192-token context by default; override `OLLAMA_CONTEXT_LENGTH` if needed.
Check the installed models with `docker compose exec ollama ollama list`.

To reuse models from a previous Windows host installation before the first start:

```powershell
docker compose create ollama
docker compose cp "$env:USERPROFILE/.ollama/models" ollama:/root/.ollama/
```

This copies only the model cache. It leaves the host files in place.

Stop just the POC containers with:

```powershell
docker compose stop onboarding-chat onboarding-mcp ollama
```

### Run the .NET projects natively

Requires the **.NET 10 SDK** for this chat/template and .NET 8 runtime for the MCP
server. Existing pipeline services remain on .NET 8. Start the containerized model
server first (use the GPU override above if desired):

```powershell
docker compose up -d --wait ollama-model
```

The default native Ollama endpoint is `http://localhost:11434`, which reaches the
container's published port. If you changed `OLLAMA_PORT`, set
`OnboardingChat__OllamaEndpoint` to match. A tool-capable model is required; the
default is `gemma4:12b`.

From the repository root, start MCP in one terminal:

```powershell
dotnet run --project DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj
```

If it is already running on port 8090, reuse that instance. In another terminal:

```powershell
dotnet run --project DotNet/Onboarding.Chat/Onboarding.Chat.csproj
```

Open **http://127.0.0.1:8091**. Click **Configure FHIR connection**, or ask:

> Help me configure a facility’s FHIR connection.

The chat streams the model's answer, shows the tool call, and offers expandable
tool results. Follow-up questions retain the full conversation and tool results.
**Stop** cancels the current response. **New chat** clears the in-memory conversation.
Refreshing the page starts a new conversation; nothing is persisted to a database.

Tenant and DataAcquisition are needed for their respective tool operations.
Vendor, profile, and measure catalogs are not exposed by the MCP tools.
The chat can create a Tenant facility using `create_facility`. Ask it explicitly to
create a facility with an ID, name, and IANA time zone. A vendor version is optional.
Connections, query plans, and measure enrollment remain separate steps.

## Configuration

Edit `appsettings.json` before starting, or override with environment variables:

| Setting | Default | Environment variable |
|---|---|---|
| Ollama base URL | `http://localhost:11434` | `OnboardingChat__OllamaEndpoint` |
| MCP endpoint | `http://127.0.0.1:8090/mcp` | `OnboardingChat__McpEndpoint` |
| Model | `gemma4:12b` | `OnboardingChat__Model` |
| Response timeout | 180 seconds | `OnboardingChat__ResponseTimeoutSeconds` |

For example, install and select another tool-capable model for a native chat run:

```powershell
docker compose exec ollama ollama pull your-installed-model-name
$env:OnboardingChat__Model = "your-installed-model-name"
dotnet run --project DotNet/Onboarding.Chat/Onboarding.Chat.csproj
```

Use Ollama's base URL, without `/api`. Native runs accept only loopback HTTP service
URLs. Containers additionally allow the designated `ollama` and `onboarding-mcp`
service names for their respective endpoints.
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
`create_facility`, `get_data_acquisition_configuration`,
`search_fhir_endpoints`, `validate_fhir_server_connection`, and
`save_data_acquisition_configuration` to the model. Automatic function invocation is limited
to six iterations. The MCP session stays alive for the entire tool/model loop and
is disposed on completion, failure, or cancellation.

The local UI has no authentication. Use it on a trusted local machine. Do not put
credentials or patient data in chat. The client does not log conversation/tool
content. Production hosting is outside this POC. Facility creation never updates existing
records or automatically retries an uncertain write. Check Tenant before retrying
a creation whose outcome is unknown.

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

## Data acquisition settings

Ask explicitly to add or update a facility's data acquisition configuration, for example:

> Set facility 12345's FHIR base URL to http://ehr-test:8080/fhir with 2 concurrent requests and 3 retries.

The tool requires an existing facility and a FHIR URL for new configurations.
Optional pull-window limits use HH:mm:ss in the facility's local time zone.
Omitted settings and existing authentication are preserved on update. Credentials
remain in the admin workflow; query plans remain separate. After saving a supplied FHIR URL, the tool checks
unauthenticated metadata connectivity and reports the result separately from the
save. Failed validation does not undo or retry a successful save.
An unknown write result must be checked in DataAcquisition before retrying.
