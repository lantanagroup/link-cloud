# NHSN-App-UI

The facility-facing NHSNLink onboarding UI. One codebase, two build outputs:

1. **`dist/embed/nhsn-link.js`** — the web component the CDC NHSN App loads.
2. **A standalone shell** — a local harness for developing and testing it.

Architecture: [`documentation/NHSNLink-UI-Architecture.md`](../documentation/NHSNLink-UI-Architecture.md).
Backend: [`documentation/NHSN-App-BFF-Architecture-and-Plan.md`](../documentation/NHSN-App-BFF-Architecture-and-Plan.md).

---

## Running it locally

### Prerequisite, once

`package.json` depends on `file:packages/nhsn-react-core-2.4.1.tgz`, which is gitignored and not in the repo. Place the tarball at `packages/nhsn-react-core-2.4.1.tgz` before installing — nothing builds without it.

```bash
npm install
npm start          # http://localhost:4300
```

### Two modes, chosen in the UI

The harness sidebar has a **Harness Mode** selector.

| Mode | Needs | Use it for |
|---|---|---|
| **Mock (offline)** — default | Nothing | Step development. No BFF, no Docker, no database. |
| **NHSN-App-BFF (:8079)** | The BFF running | Real JWT validation, real endpoints. |

**Mock mode is fully offline.** `MockApiClient` serves the whole `ApiClient` port from in-memory fixtures and persists the draft to `localStorage`, and en-US strings are bundled into the artifact, so nothing is fetched. This is the mode to develop steps in — the BFF's onboarding endpoints do not exist yet.

Everything a fixture invents is marked `simulated: true` and uses obviously synthetic values, so a screenshot can never be mistaken for real facility data.

### BFF mode

Start the BFF (see [`documentation/SERVICES.md`](../documentation/SERVICES.md)), then switch the selector. `/api` proxies to `http://localhost:8079`.

BFF mode signs a real ES256 JWT in the browser from the active test profile, so it exercises the same validation path as production. Create a profile in the sidebar with:

- email, name, groups (`FACADMIN`), facilityId
- issuer matching `NhsnJwt:Issuer` in the BFF config
- `kid` matching the BFF's signing certificate
- the matching PKCS#8 private key PEM

Change any of them to test the negative paths. Defaults can come from the shell runtime server via `/shell-config.js`:

- `NHSN_APP_UI_DEFAULT_JWT_ISSUER`
- `NHSN_APP_UI_DEFAULT_JWT_KEY_ID`
- `NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM`

---

## Commands

```bash
npm start              # standalone shell on :4300
npm run build          # both outputs
npm run build:embed    # dist/embed/nhsn-link.js only

npm run verify         # typecheck + lint + tests — run before pushing
npm run typecheck
npm run lint
npm test               # unit + component tests
npm run test:watch
npm run test:boundary  # builds the embed bundle and inspects it (~20s)
```

---

## The rule that shapes the structure

**Shell code must never reach the embed bundle.** That artifact runs inside the CDC NHSN App with their user's session, and the shell handles private keys — if it leaks, we ship a token-forging harness into their application.

```
src/
  core/      embed-safe. The only thing embed/register.tsx may reach.
  shell/     standalone only. Never imported by core.
  embed/     -> core
  app/       -> shell + core
```

Three layers enforce it:

1. `no-restricted-imports` in `eslint.config.js` — fast feedback.
2. A webpack alias that fails the embed build if `shell/` appears on its graph.
3. **`tests/bundle-boundary.test.ts`** — inspects the built artifact for shell markers. This is the one that actually holds: a lint rule can be disabled inline and a re-export can carry code across without tripping it.

Run `npm run test:boundary` in CI on every change to `src/`.

---

## Layout

| Path | Holds |
|---|---|
| `core/onboarding/` | The step machine — `types`, `reducer`, `flow`, `gating`, `navigation`, provider, `StepHost` |
| `core/onboarding/steps/` | Thirteen step directories, in flow order |
| `core/fields/` | Design-system adapter — the **only** place `@nhsn/nhsn-react-core` components are imported |
| `core/api/` | `ApiClient` port, `BffApiClient`, `http.ts`, contracts |
| `core/localization/bundled/` | en-US fallback shipped in the artifact |
| `shell/auth/` | `jose` signing, private keys, `TestUserProfile` |
| `shell/mocks/` | `MockApiClient` |
| `shell/facilities/` | The harness — **throwaway**, deleted once the BFF resolves facility from the token |

### Element contract

```html
<script src="https://<env-host>/nhsn-link.js"></script>
<nhsn-link baseurl="/nhsnlink" apibaseurl="/api" locale="en-US"></nhsn-link>
```

`apibaseurl` is the only knob for backend location: the component appends `/nhsn-app-bff/…` to it. If the gateway mounts us elsewhere, the attribute changes and no code does.

The component **never sets an `Authorization` header** — the NHSN gateway injects the JWT in transit. It also never sends a facility: the BFF resolves it from the `facility` claim, and the onboarding routes carry no facility segment.

---

## Working on a step

**→ `documentation/NHSNLink-UI-Step-Implementation-Guide.md`** — hand that to your coding agent along with the user story. It is distributed offline, since `documentation/` is gitignored.

The short version: each step is a separate story, and the machine around them is done — don't rebuild it.

Already wired: draft access and patching (`useOnboarding()`), navigation (`onNext`/`onBack`), gating and URL sync, draft persistence with ETag concurrency, and every control through `core/fields`.

To implement one:

1. Replace the stub in `core/onboarding/steps/<step>/`.
2. Replace its `COMPLETION_PENDING_STORY` placeholder in `flow.ts` with a real `isComplete`. Grep that symbol for what's outstanding.
3. Add fields to the step's slice in `onboarding/types.ts`.
4. Add i18n keys to the BFF's `Localization/en-US/` **and** `core/localization/bundled/`.

Three things never go in the draft: **secrets** (sFTP credentials are write-only; the draft holds `hasCredentials` only), **MRN intake** (normalized server-side, mirrored for rendering), and **reference data** (fetched per session).

Vendor branching is field-level and driven by the `VendorProfile` the BFF serves — never `vendor === 'Epic'` in a component.
