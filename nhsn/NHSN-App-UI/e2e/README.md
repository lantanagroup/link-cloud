# NHSN-App-UI end-to-end tests

Playwright, run from `nhsn/NHSN-App-UI`. Modelled on `Web/Admin.UI/e2e`, but the mocking
strategy is deliberately different — see **Why there is no `/api` interceptor** below.

```bash
npm run e2e            # mocked tier, headless — the default
npm run e2e:ui         # same, interactive
npm run e2e:report     # last HTML report
npm run e2e:bff        # BFF tier; needs NHSN-App-BFF on :8079 (preflight fails fast)
npm run typecheck:e2e  # Playwright transpiles without typechecking; this catches drift
```

One file or one test:

```bash
npx playwright test --project=mocked e2e/mocked/fhir-step.spec.ts
npx playwright test --project=mocked -g "connection test"
```

Playwright starts `npm start` itself (`webServer`, `reuseExistingServer: true`) — don't start
one first. A cold webpack compile is ~30-60s. Count tests with
`npx playwright test --project=mocked --list`; it needs no dev server. **27 tests in 7 files**
as of 2026-10-30; a warm run is ~28s, and `--repeat-each=3` is clean.

The dev server answers on `http://localhost:4300` but **not** on `http://127.0.0.1:4300`.
Preflight with the hostname.

## Layout

```
e2e/
  mocked/   *.spec.ts        default tier: the shell in its own Mock (offline) mode
  bff/      *.bff.spec.ts    opt-in smoke set against a real NHSN-App-BFF
  fixtures/
    drafts.ts                typed FacilityDraft builders (import src/, never `any`)
  support/
    test.ts                  extended `test`: harness + onboarding fixtures, console tripwire
    fields.ts                selector contract for core/fields (Kendo) + the a11y-name traps
    bff.ts                   BFF preflight and test-profile seeding
    pages/
      harness.page.ts        the standalone shell (mode switch, draft seeding)
      onboarding.page.ts     step rail, panel, footer, alerts
```

## Why there is no `/api` interceptor

Admin.UI's mocked tier intercepts `/api` and fails a test that touches an unmocked endpoint.
Nothing like that is possible — or needed — here. `MockApiClient` implements the whole
`ApiClient` port **in-process**, injected at the composition root, so in mock mode no request
leaves the page. Determinism comes from two places instead:

1. **`MockApiClient`** (`src/shell/mocks/`), which tracks the real contracts rather than
   whatever is easiest to fake. Everything it invents is marked `simulated: true`.
2. **A seeded draft** — `fixtures/drafts.ts` writes `localStorage` before the app's first
   read, which is how a spec opens on a late step instead of clicking through twelve.

A BFF-tier spec talks to a real backend, so it gets no mocks at all.

## Getting into the app

Two facts about `FacilityHarness.tsx` shape every spec; `HarnessPage.open()` exists to
absorb them.

- **The harness defaults to BFF mode, not mock** (`useState<HarnessMode>('bff')`), and BFF
  mode with no saved profile renders no client, so a fresh context lands on *"Select or
  create a test profile"*. The README at the project root calls mock mode the default; the
  code does not. `open()` flips the selector.
- **Mode is component state, not storage.** Every navigation — `goto`, `reload`,
  back/forward — returns to BFF mode and unmounts the wizard. So **no mocked spec can assert
  on reload behaviour**: in this harness a reload cannot preserve the wizard at all. The
  draft survives (it is in `localStorage`); the mode does not.

`open()` waits for `.nhsn-link__step-panel`, not just the rail. `OnboardingStepsNav` renders
throughout the draft load, against `createEmptyDraft()`, whose current step is `welcome` —
and `goTo()` returns early when the target is already current, so a rail click in that window
is **silently swallowed**. The panel is the machine's own ready signal. This cost a debug
cycle; a "flake" here is almost always a wait on the wrong element.

## Seeding a draft

```ts
await harness.open({
  draft: draftAt('fhir', {facilityInfo: facilityInfoComplete()}),
  path: '/onboarding/fhir'
});
```

`draftAt()` unlocks every step up to the target, but unlocking is not enough: `isUnlocked`
also requires every earlier visible step to be **complete**, so the fixture has to supply the
slice data those steps validate. Under-fill it and `resolveStep` quietly drops you back to
`furthestLegalStep` — which reads as a broken deep link when it is a broken fixture.

The key is `nhsn-app-ui.mockDraft.MOCK-FACILITY-001`: `DRAFT_KEY_PREFIX` plus
`MockApiClient`'s default facility id, which applies because mock mode passes
`activeProfile?.facilityId` and there is no profile. Seed a profile and the key changes.

## Selectors

Page objects in `support/pages/` are the selector contract. Prefer role/label; the step
machine's own class names are used where roles are ambiguous, for the same reasons
`walking-skeleton.test.tsx` gives (the nav rail and step rail are both lists, and
`PageHeader` renders its title more than once).

Everything visual is `@nhsn/nhsn-react-core` (Kendo). The traps, each one a real mistake:

- **`Select` is not a `<select>`.** `selectOption()` does nothing. Options render into a
  `.k-animation-container` appended to the **body**, as `role="option"` with ids
  `option-<fieldId>-guid-<n>`, and the placeholder is a real option at index 0. Use
  `chooseOption(page, fieldId, label)`.
- **The chosen label is in the host element's own text**, not a `.k-input-value-text` child —
  that class does not exist in this build.
- **`AcronymText` rewrites accessible names.** HSLOC / POI / EHR are rendered as an
  `aria-hidden` visible copy plus a visually-hidden spelled-out one, so
  `getByRole('combobox', {name: 'EHR Vendor'})` matches nothing — the name is
  `E H R Vendor`. Do not compute this with a string replace: next to punctuation the name
  also picks up element-boundary spacing, so step 8 is
  `8 Location Identification ( H S L O C )`. Match the stable part with a RegExp.
- **`textContent` includes both copies**, which is what `toHaveText` compares: step 8's label
  is `Location Identification (HSLOCH S L O C)`.
- **`HeadingPause` appends a visually-hidden `.`** to every step heading, so an `<h1>` reads
  `Facility Information .`. Assert headings with `toContainText`.
- **Completed rail entries carry a visually-hidden ` (completed)`** suffix in their name.
- **Steps render empty `role="alert"` live regions up front.** A bare
  `expect(alerts).toContainText(...)` is then a strict-mode violation — and those are *not*
  retried, so it fails instantly rather than waiting. Poll `alertTexts()` instead.
- **Validation runs on Continue, not as a disabled button.** Continue is enabled on an
  incomplete step; asserting `toBeDisabled` fails and misdescribes the gate.
- **Success is `role="status"`, errors are `role="alert"`.**
- Time zone options are labelled `America/New_York — (UTC-04:00) Eastern Time`, and **the
  offset is computed from the current date** — it reads `-05:00` outside daylight saving.
  Match the IANA id, which is also what the draft stores.

## The console tripwire

`support/test.ts` collects `console.error` and uncaught `pageerror`, dedupes, and fails the
test naming them. It is the highest-leverage thing in the Admin.UI suite and applies to every
spec here for free. Verified load-bearing on both paths.

Opt out with `test.use({allowConsoleErrors: true})`, or narrow with
`test.use({allowedConsoleErrors: [/pattern/]})`. **`test.use` at file scope applies to the
whole file**, including tests declared above it — put it inside a `test.describe` to scope it.
(That mistake produced a passing tripwire proof that proved nothing.)

Tolerated by default: `Failed to load resource:`, `favicon.ico`, and the `/shell-config.js`
MIME-type refusal. That last one is structural, not incidental: `src/app/index.html` loads
`/shell-config.js`, which only the Express runtime server generates (`server/main.js`), so
webpack-dev-server answers with `index.html` and Chrome refuses to execute it. `npm start`
can never have a clean console. Deployed builds serve the file properly.

## Characterisation tests

`mocked/draft-persistence.spec.ts` and the Days-spinner test in `mocked/fhir-step.spec.ts`
pin down behaviour that is **wrong or surprising**, not behaviour to preserve. Each says so
in a comment. If one starts failing, read the comment before changing the test — a failure
probably means the defect was fixed, and the assertion should be inverted rather than
relaxed.

The no-dirty-transition case is not merely surprising: `walking-skeleton.test.tsx` asserts
that a transition **does** persist the draft, and that unit test is already failing on this
branch with "expected null to be truthy". The intended behaviour is persistence; the e2e spec
pins what actually happens. Fixing `OnboardingProvider` should flip both at once.

Unrelated pre-existing reds, for reference when running the project's own checks: `npm test`
has 35 failures (mostly `styles.test.ts` expecting HSLOC classes that `NHSNLink.css` does not
define) and `npm run lint` reports an unused `user` in `WelcomeStep.tsx`. None of them involve
this suite, and `npm run typecheck` is clean.

## The BFF tier

Thin on purpose. The BFF's onboarding endpoints do not exist yet — the project README is
explicit that mock mode is the only way any step runs until its phase 1 lands — so specs
against them would encode a fiction. BFF mode also needs a signed-JWT profile whose issuer,
`kid` and PKCS#8 key match the BFF's `NhsnJwt` config; `docker-compose.yml` supplies those
to the shell's runtime server as `NHSN_APP_UI_DEFAULT_JWT_*`, served at `/shell-config.js`,
which the dev server does not serve. A real BFF run therefore wants the composed shell
(:8090), not `npm start`. `support/bff.ts` holds the preflight and `seedTestProfile()` for
when that lands.

## CI

`tests.yaml` runs the mocked tier as its own job, **NHSN App UI Tests (mocked)**, gated on
changes under `nhsn/NHSN-App-UI/` or to the workflow itself — mirroring Admin.UI's job. It
typechecks the suite before downloading Chromium, so a drifted fixture fails in seconds, and
publishes `test-results/junit.xml` through `dorny/test-reporter` plus the HTML report and
failure traces as artifacts.

The one step that is not in Admin.UI's job: `nhsn-react-core-2.4.1.tgz` is a gitignored
`file:` dependency kept in the private `nhsnlink` Azure repo, so the job downloads it (using
the `AZDO_PAT` secret) before `npm ci`, exactly as `backend-e2e-tests` already does for the
compose build. The lockfile pins its integrity hash, so `npm ci` doubles as a check that the
download is the same artifact the `nhsn-app-ui` image is built from.

The `bff` project is **not** run in CI.

## Not built

- **No demo tier.** Admin.UI has a paced, headed walkthrough for presenting. Nothing here
  needs one yet; add it the way Admin.UI did if a demo is wanted.
- **Steps 6-13** (Patients of Interest onward) have no specs. They are the richest — census
  vendor branching, HSLOC and encounter mapping, report generation and the results tables
  with their modals and xlsx export — and each wants the same treatment: walk it in a browser
  first, then encode what was observed. The fixtures and page objects here are the starting
  point; extend `fixtures/drafts.ts` with the slice data each step's `isComplete` requires.
