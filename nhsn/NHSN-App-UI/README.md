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

## How the UI initializes

The UI does **not** take a JWT prop.

Instead, `NHSNLink` initializes by calling the BFF:

- `GET /api/nhsn-app-bff/userinfo`

The BFF resolves who the user is, what NHSNLink role they have, and whether they are in onboarding or maintenance mode.

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

This is the initial framework/foundation behavior. It is intentionally light-weight but establishes the long-term UI shape.

## Project structure

### `src/components`
Reusable UI components intended for the embeddable package.

Important file:
- `src/components/NHSNLink.tsx`

### `src/services`
Client-side service layer.

Important file:
- `src/services/user-info-service.ts`

### `src/web-component`
Adapter layer for web-component registration and NHSN App hosting.

Important file:
- `src/web-component/register.ts`

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

- `dist/app-shell/...`

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