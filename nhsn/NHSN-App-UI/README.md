# NHSN-App-UI

## Overview

`NHSN-App-UI` is the React-based foundation for the NHSN App integration framework described by the `nhsn-app-web-component-poc` ADR.

This project intentionally produces **two different outputs**:

1. a **standalone shell application** for lower/test environments
2. a **separate embeddable webpack bundle** for NHSN App integration

The shell exists to generate signed lower-environment JWTs, while the embeddable bundle exists to be loaded by the NHSN App.

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

The BFF resolves who the user is, whether their JWT includes `FACADMIN`, whether a facility claim is present, and whether the facility is in onboarding or maintenance mode.

The `/userinfo` response also drives:

- whether the user has the required `FACADMIN` role
- whether a facility context is present
- whether onboarding is required
- the navigation options that should be available

## Shell signed-JWT model

The standalone shell is designed for lower-environment testing.

### First launch
If there are no saved test profiles, the shell asks the tester to create a lower-environment JWT test profile.

It captures:

- email
- name
- group(s)
- facilityId
- issuer
- key id (`kid`)
- private key PEM

### Saved profile library
The shell stores a library of previously used test profiles in browser local storage so the tester can easily switch among JWT contexts.

The shell tracks:

- multiple saved test profiles
- the currently active test profile
- last-used timestamps

### Request behavior
When the shell makes a request to the BFF, it signs a real bearer JWT using the configured issuer, key id, and private key PEM and sends it through the normal `Authorization: Bearer ...` header.

This mimics the production request shape as closely as possible while still allowing lower-environment negative testing by changing the issuer, key id, or private key in the harness.

## Top-navigation behavior

`NHSNLink` uses the `/userinfo` response to determine whether to render:

- no-access messaging when `FACADMIN` is missing
- missing-facility messaging when no facility claim is present
- onboarding navigation
- maintenance/configuration navigation

This is the initial framework/foundation behavior. It is intentionally light-weight but establishes the long-term UI shape.

### Navigation component

The navigation rail is implemented as its own component:

- `src/components/NavigationRail.tsx`

The rail currently shows:

- app title
- available navigation options
- the current end-user identity at the bottom of the rail

### Current routes

The UI currently supports component-controlled routes relative to `baseUrl`:

- `/` ? Home
- `/onboard` ? Onboarding
- `/admin/users` ? System administrator user management

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
<NHSNLink baseUrl="/" apiBaseUrl="/api" />
```

### Build both outputs

```bash
npm run build
```

### Harness JWT signing mode

For signed JWT mode:

- configure a test user with the JWT issuer that matches `NhsnJwt:Issuer` in the BFF config
- configure the `kid` that matches the key id/thumbprint expected by the BFF signing certificate
- provide the matching PKCS#8 private key PEM used to sign the token
- configure the matching public certificate PEM in `NHSN-App-BFF` development/docker appsettings

The shell runtime server also supports these environment variables for default harness JWT signing settings:

- `NHSN_APP_UI_DEFAULT_JWT_ISSUER`
- `NHSN_APP_UI_DEFAULT_JWT_KEY_ID`
- `NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM`

These are exposed to the lower-environment shell through `/shell-config.js` so testers can avoid pasting the key information repeatedly. Saved test-user profiles may still override any of these values for negative testing.

## Docker usage

The Dockerfile builds and publishes the **standalone shell only**.

That container is intended for lower-environment deployment where testers need to:

- save multiple signed-JWT test users
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

If the BFF provides an `AccessRequestUrl`, the UI renders it as a direct link.
