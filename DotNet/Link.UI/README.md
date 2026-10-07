# Link.UI (phase 1)

ASP.NET Core 8 MVC shell that merges toward a single Admin + Automation console.
Stack and styling follow **Automation.UI**; browser auth follows **Admin.BFF**.

## Run

From repo root (or this folder):

```bash
dotnet run --project DotNet/Link.UI --launch-profile http
```

Default URL: `http://localhost:5280`

Health: `GET /health`

Development profile sets `Authentication:EnableAnonymousAccess=true` so the shell is browsable without a proxy.
Use the `docker` launch profile (or compose later) when Tenant + Admin.BFF are up and Link bearer tokens are configured.

## Auth (Admin.BFF cookie contract)

**Choice for phase 1: keep Admin.BFF as a separate deploy unit**, and expose its auth HTTP API
same-origin on Link.UI via **YARP reverse proxy**.

Both the proxy and the server-side user chip use **`ServiceRegistry:AdminBffServiceUrl`**
(default `http://localhost:8063`). There is not a second destination setting.

| Browser path on Link.UI | Proxied to Admin.BFF |
|-------------------------|----------------------|
| `/api/login`            | `{AdminBff}/api/login` |
| `/api/user`             | `{AdminBff}/api/user` |
| `/api/logout`           | `{AdminBff}/api/logout` |
| `/api/{**catch-all}`    | `{AdminBff}/api/...` |

- MVC `AuthController` Login/Logout redirect to `/api/login` and `/api/logout`.
- Admin.BFF registers those auth routes only when its own `Authentication:EnableAnonymousAccess` is false. A local compose BFF left in anonymous mode answers `/api/login` and `/api/user` with 404.
- Admin.BFF sends the browser to `/dashboard` after login and `/logout` after logout. This host maps those to Home.
- The login proxy sets `Referer` to the Link.UI origin root so Admin.BFF's post-login redirect is `{origin}/dashboard` from every page.
- Server-side user display uses `AdminBffUserService` (`HttpClient` to Admin.BFF `/api/user` with the browser `Cookie` header forwarded). An empty 200 body (Development Admin.BFF anonymous mode) stays signed out.
- `Authentication:RequireBffSession=true` redirects unauthenticated MVC requests to `/api/login`, whether or not anonymous access is enabled. If Admin.BFF cannot be reached, those pages return 503.
- `Authentication:EnableAnonymousAccess=false` with `RequireBffSession=false` returns 503 for everything except `/health` and `/api` (Automation.UI posture).
- Cookie `Domain` / `SameSite` and IdP redirect allow-lists must include the Link.UI origin when auth is required in a real environment. Admin.BFF cookies are `SameSite=Strict` and `Path=/`.

This prefers **same-site reverse proxy** over embedding BFF auth into the MVC host, so Admin.BFF stays independently deployable.

## Tenants

**Choice: LinkSDK**, not Admin.BFF, for facility, census, and query dispatch. Those clients already speak the service APIs. Admin.BFF stays the browser session.

- **Tenants** lists facilities (`IFacilityServiceClient.GetFacilityListAsync`). A row opens the facility hub.
- The hub creates and updates facility identity (name, IANA timezone, vendor version). While `DMRP:Enabled` is true, scheduled reports are read-only because Tenant rejects a caller-supplied schedule. While it is false, daily/weekly/monthly measure ids are editable and must be unique across periods.
- Census (`ICensusServiceClient`) and query dispatch (`IQueryDispatchServiceClient`) panels create, update, and delete that facility's configuration. A missing `ServiceRegistry` URL leaves the panel as a configuration message and does not take down the identity form.
- `FacilityIdSettings:NumericOnlyFacilityId` defaults to false, matching Tenant. Set it true only when that Tenant rejects alphanumeric ids.
- Calls use the same LinkSDK bearer path as Automation.UI (`AddLinkSdk` + `ServiceRegistry` + `LinkTokenService`).

## Phase scope

In:

- MVC + Razor, static files, SignalR stub hub (`/hubs/link`)
- LinkSDK registration, env + Azure App Config hooks, health
- `--au-*` CSS tokens + Bootstrap + **left vertical nav**
- Home, tenant list, facility hub (identity, census, query dispatch)
- Automation runs dashboard and run detail. Summaries are read from Automation.UI's `automation_runs` collection. Live status proxies that process's `/hubs/runs` hub.

Out:

- Data acquisition and normalization panels, facility view, reports, logs, configuration, system pages
- Product landing dashboard
- Automation new-run launch, scenarios, configuration editors, metrics, API health, and cleanup
- No changes to Automation.UI or Web/Admin.UI behavior beyond solution registration

## Solution

Project: `DotNet/Link.UI/Link.UI.csproj`  
Tests: `DotNet/Link.UI.Tests`  
Registered in `link-cloud.sln` as **Link.UI** and **Link.UI.Tests**.
