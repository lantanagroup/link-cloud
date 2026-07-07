# NHSN-App-UI

## Overview

`NHSN-App-UI` is the React-based foundation for the NHSN App integration framework described by the `nhsn-app-web-component-poc` ADR.

This project intentionally produces **two different outputs**:

1. a **standalone shell application** for lower/test environments
2. a **separate embeddable webpack bundle** for NHSN App integration

The shell exists to simulate user contexts, while the embeddable bundle exists to be loaded by the NHSN App.

## Key architectural rule

The standalone app/framework shell must **not** be included in the webpack bundle intended for the NHSN App.

That means:

- shell-only code lives under `src/app-shell`
- reusable component code lives under `src/components`
- the webpack embed build only packages the reusable NHSNLink component layer and its adapter

## Main reusable component

The main integration entry point is:

- `src/components/NHSNLink.tsx`

This is the same component used by:

- the standalone lower-environment shell
- the embeddable NHSN App integration package

### Public component contract

The component supports a configurable routing base via:

- `baseUrl`
- `apiBaseUrl`

Examples:

```tsx
<NHSNLink baseUrl="/" apiBaseUrl="/api" />
<NHSNLink baseUrl="/nhsnlink" apiBaseUrl="https://some-host/api" />
```

The standalone app host uses `/` while the embedded NHSN App scenario is expected to use `/nhsnlink`.

`apiBaseUrl` controls which backend API root the component targets for endpoints like `/userinfo`. This is especially important for NHSN App integration where the UI may need to call an externally routed BFF path.

## How the UI initializes

The UI does **not** take a JWT prop.

Instead, `NHSNLink` initializes by calling the BFF using `apiBaseUrl`:

- `GET /api/nhsn-app-bff/userinfo`

The BFF resolves who the user is, what NHSNLink role they have, and whether they are in onboarding or maintenance mode.

The `/userinfo` response also drives:

- whether the user is active or disabled
- whether onboarding is required
- whether the user is a System Administrator
- the navigation options that should be available

## Shell simulation model

The standalone shell is designed for lower-environment testing.

### First launch
If there are no saved test users, the shell asks:

> “Who are you simulating?”

It captures:

- email
- name
- group(s)
- facilityId

### Saved profile library
The shell stores a library of previously used test-user profiles in browser local storage so the tester can easily switch among user contexts.

The shell tracks:

- multiple saved test users
- the currently active simulated user
- last-used timestamps

### Request behavior
When the shell makes a request to the BFF, it injects a `jwt` header containing the active simulated user payload.

The BFF only honors that header when its lower-environment simulation feature flag is enabled.

## Top-navigation behavior

`NHSNLink` uses the `/userinfo` response to determine whether to render:

- onboarding navigation
- maintenance/configuration navigation
- system-administrator-specific navigation

This is the initial framework/foundation behavior. It is intentionally light-weight but establishes the long-term UI shape.

### Navigation component

The navigation rail is implemented as its own component:

- `src/components/NavigationRail.tsx`

It is responsible for:

- rendering the `NHSNLink` heading
- rendering route-aware navigation buttons
- highlighting the active route
- rendering current-user identity at the bottom of the rail

### Current routes

The UI currently supports component-controlled routes relative to `baseUrl`:

- `/` → Home
- `/onboard` → Onboarding
- `/admin/users` → System administrator user management

Under an embedded base of `/nhsnlink`, those resolve as:

- `/nhsnlink`
- `/nhsnlink/onboard`
- `/nhsnlink/admin/users`

## Project structure

### `src/components`
Reusable UI components intended for the embeddable package.

Important file:
- `src/components/NHSNLink.tsx`

Other key components:
- `src/components/NavigationRail.tsx`
- `src/components/OnboardingScreen.tsx`
- `src/components/SystemAdminUsersScreen.tsx`
- `src/components/notifications/NotificationProvider.tsx`

### `src/services`
Client-side service layer.

Important file:
- `src/services/user-info-service.ts`

### `src/web-component`
Adapter layer for web-component registration and NHSN App hosting.

Important file:
- `src/web-component/register.ts`

The custom element supports a `baseurl` attribute so the embedded host can control the routing base path.

### `src/app-shell`
Standalone lower-environment shell.

Important files:
- `src/app-shell/App.tsx`
- `src/app-shell/main.tsx`

### `src/shared`
Shared models and local storage helpers.

## Build outputs

### Standalone shell build
Command:

- `npm run build:app`

Output:

- `dist/...`

This output is what the Dockerfile publishes for lower/test environments.

### Embeddable webpack build
Command:

- `npm run build:embed`

Output:

- `dist/embed/nhsn-link.js`

This is the artifact intended for NHSN App integration.

## Local development

### Start the standalone shell

```bash
npm install
npm start
```

This starts the lower-environment shell on port `4300` and proxies `/api` requests to the BFF on `http://localhost:8079`.

In the standalone shell, the app host renders:

```tsx
<NHSNLink baseUrl="/" />
```

### Build both outputs

```bash
npm run build
```

## Docker usage

The Dockerfile builds and publishes the **standalone shell only**.

That container is intended for lower-environment deployment where testers need to:

- save multiple simulated user profiles
- switch among them
- re-run the same shared `NHSNLink` component against the BFF

The Docker image is not the delivery mechanism for the embeddable webpack artifact.

When launched through `docker compose`, the shell is exposed on port `8090` so it does not conflict with the webpack development server port used locally.

Inside Docker, the standalone shell runtime server proxies `/api` requests to the BFF using the `BFF_BASE_URL` environment variable. By default in compose this is set to `http://nhsn-app-bff:8079/api` so browser calls like `/api/nhsn-app-bff/userinfo` reach the BFF with the expected `/api/...` route prefix.

The Docker-hosted shell also renders with a base URL of `/`, while the custom element wrapper defaults to `/nhsnlink` for embedded usage.

## Notifications

The UI includes a reusable notification provider:

- `src/components/notifications/NotificationProvider.tsx`

Behavior:

- notifications appear in the bottom-right corner
- multiple notifications stack vertically
- success/info notifications auto-dismiss after 5 seconds
- error notifications remain visible until explicitly dismissed
- all notifications can be manually closed

This notification system is used for real-time persisted updates such as system administrator role/status changes.

## System administrator behavior

System administrators have a separate route and screen:

- `/admin/users`

This screen allows them to:

- view users
- change user roles
- enable/disable users

Safeguards currently in place:

- a system administrator cannot change their own role
- a system administrator cannot disable their own account

The UI prevents those actions and the BFF also enforces them server-side.

## Disabled-user behavior

If `/userinfo` indicates that the current user is disabled, the UI blocks normal usage and shows:

- `Your account does not have access to NHSNLink. Submit a request to restore access`

If the BFF provides an access-request URL, the user is shown a clickable `Submit a request` link.

## Why the shell exists

The shell is intentionally a testing harness and framework host. It proves:

- how multiple user contexts can be exercised
- how the reusable component behaves without the real NHSN App shell
- how the BFF-driven `/userinfo` contract shapes the UI

## Why the embed bundle exists

The embed bundle exists to prove that the reusable UI can be loaded separately by the NHSN App without pulling in shell-only simulation logic.

## Foundation intent

This project is a framework/foundation, not the full facility configuration product.

It establishes:

- React as the UI framework
- webpack packaging for the embed artifact
- a shared `NHSNLink` entry point
- lower-environment simulated user switching
- BFF-driven user/role initialization
- onboarding vs maintenance navigation scaffolding

Future work can add:

- richer onboarding workflow screens
- configuration maintenance details
- shared NHSN React core components once available
- more sophisticated navigation and role-based experiences

## Temporary `nhsn-react-core` package usage

Until `nhsn-react-core` is published to a shared internal package location, this project can consume the package from a local `.tgz` placed inside the UI project.

### Expected folder

Create this folder locally:

- `D:\Code\link-cloud\nhsn\NHSN-App-UI\packages\`

Place the package there, for example:

- `D:\Code\link-cloud\nhsn\NHSN-App-UI\packages\nhsn-react-core-1.2.3.tgz`

The `packages/` folder should remain gitignored so the private package is never committed to the public repository.

### Install pattern

Install the package from the local file path inside the project:

```bash
npm install --save .\packages\nhsn-react-core-1.2.3.tgz
```

This allows npm to record a local file dependency while keeping the actual `.tgz` out of source control.

### Local developer setup

1. Obtain the private `nhsn-react-core` `.tgz`
2. Copy it into:
   - `D:\Code\link-cloud\nhsn\NHSN-App-UI\packages\`
3. Run:

```bash
npm install --save .\packages\nhsn-react-core-1.2.3.tgz
npm run build
```

### CI / secured build agent setup

A build agent should:

1. Download or copy the `.tgz` from a secure internal location
2. Place it in:
   - `D:\Code\link-cloud\nhsn\NHSN-App-UI\packages\`
3. Run `npm install --save .\packages\nhsn-react-core-<version>.tgz`
4. Run the normal build

### Docker build implications

This approach works better with Docker than a host-only environment variable path because the package is expected to live **inside the UI project build context**.

As long as the `.tgz` has been placed in `packages/` before the Docker build starts, the Docker build can access it naturally.

### Source-control safety

The following are intentionally **not** committed:

- the `packages/` contents
- the private `.tgz` package itself

This keeps the public repository free of private artifacts while still allowing local, CI, and Docker builds to consume the package temporarily.