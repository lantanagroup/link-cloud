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
`npx playwright test --project=mocked --list`; it needs no dev server. **80 tests in 15 files**
as of 2026-11-09, all passing against a warm dev server, and `--repeat-each=3` is clean.

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
    fields.ts                selector contract for core/fields (Kendo), a11y names, typeDate
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

`open()` waits for three things, each for its own reason. A "flake" on arrival is almost
always one of them having been skipped.

1. **`.nhsn-link__step-panel`**, not just the rail. `OnboardingStepsNav` renders throughout
   the draft load, against `createEmptyDraft()`, whose current step is `welcome` — and
   `goTo()` returns early when the target is already current, so a rail click in that window
   is **silently swallowed**. The panel is the machine's own ready signal.
2. **`.nhsn-link__step-actions`**, the footer each step installs through `useStepChrome`
   *after* its own first render — so its arrival means the step, not just the host, is up.
3. **A 250ms settle.** A navigation requested in the first moments after landing on a step by
   URL is **silently dropped**: no navigation, no unsaved-changes dialog, nothing written to
   the draft or announced, and an identical second click works. Measured on Report Results'
   "+ Generate New Report", the window closes within 200ms of the footer appearing, which
   fits `completeGoTo`'s `startTransition` losing to the deep link's own resolution as it
   finishes. **This is an app-side race, not a test artefact** — a real user meets it in the
   embedded app, which mounts the wizard at a URL. Nothing observable in the DOM separates
   the two moments, so no assertion can replace the wait; it lives in one place rather than
   as a sleep in every spec.

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
- **A step-level message announced on a `setTimeout` needs a retried expectation.** The HSLOC
  duplicate summary is one; `alertTexts()` is a one-shot read and misses it. Wait on the panel
  first (`expect(stepPanel).toContainText(...)`), then snapshot the alerts for counting.
- **`getByRole`'s `name` matches a substring, case-insensitively.** On the encounter step
  `{name: 'Encounter.type code'}` also matches that group's **Encounter.type Code System**
  input, which comes first in the DOM — so an inexact locator types the local code into the
  code system and the step rejects a form that looks filled in. Pass `exact: true` wherever
  one field's name is a prefix of another's.
- **A masked Kendo date field cannot be `fill()`ed.** The mask consumes the string from
  wherever the caret happens to be and leaves `mm-dd-0006`, which validation reads as empty.
  Use `typeDate(page, fieldId, 'MM-dd-yyyy')`: it presses `Home`, types digits as trusted key
  presses (Kendo ignores synthetic `KeyboardEvent`s) and **blurs afterwards** — a date's error
  renders on blur, and leaving that blur to the next click moves the page out from under it,
  so the click is lost.
- **Validation runs on Continue, not as a disabled button.** Continue is enabled on an
  incomplete step; asserting `toBeDisabled` fails and misdescribes the gate.
- **A control can look disabled without being disabled.** At `maxItems` the report step's add
  button gets `nhsn-link__button--disabled` and nothing else — no `disabled` attribute, no
  `aria-disabled` — so `toBeDisabled()` fails while the button is genuinely inert. Assert the
  class, and that clicking changes nothing.
- **Success is `role="status"`, errors are `role="alert"`, and toasts are neither** — they
  render into `.notification-stack`, which carries one `aria-live` for all of them, so there
  is no per-toast role to match.
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

Several specs pin down behaviour that is **wrong or surprising**, not behaviour to preserve.
Each says so in a comment. If one starts failing, read the comment before changing the test —
a failure probably means the defect was fixed, and the assertion should be inverted rather
than relaxed. The current set:

| Spec | What it pins |
|---|---|
| `draft-persistence.spec.ts` | a step transition does not persist the draft (see below) |
| `fhir-step.spec.ts` | the Days spinner's accessible name is doubled |
| `census-step.spec.ts` | the 24-hour frequency ceiling is unreachable from the inputs: 0-23h and 0-59m cap the total at 1,439, one minute under the 1,440 that `frequencyTooLong` exists to catch |
| `location-org-step.spec.ts` | `locationOrg.noneAdded` and `errors.invalidFhirPath` are copy that no component renders, and switching resolution method silently discards the rows of the method left behind |
| `hsloc-step.spec.ts` | rows hydrate from `getHslocMappings`, so a facility whose mappings are saved still arrives at one empty row in mock mode; and the reference pane's "select a code" prompt is unreachable except with zero search matches, because an effect auto-selects the first row |
| `encounter-step.spec.ts` | `flow.ts` declares the step optional (`isComplete: () => true`) while the step itself requires a code system, so the rail's next entry stays disabled either way; and a blank second code system is reported as a *mapping-row* problem, leaving `messages.missingCodeSystem` unused |
| `report-step.spec.ts` | `patientIdsIncomplete` is computed but suppressed, and at the ten-id ceiling the add button looks disabled without being disabled |
| `report-results-step.spec.ts` | zero reports render no table **and** no empty state, because "No reports have been generated yet." is a `<tr>` inside the table that only renders when there are rows |
| `mrn-intake-step.spec.ts` | a rule is required even from a facility that has just declared it has a single MRN, while every sibling section is gated on its own answer |
| `complete-step.spec.ts` | "Reports Generated: 0" sits beside the id of the report the enrollment completed from — the two rows read from different sources; and the recap reports an unqualified "Passed" for a connection test the mock only simulated, because `connectionTested` is a bare boolean and the result's `simulated` flag reaches nothing |

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
- **The Report Results detail view.** `ReportDetailView` and its modals — query plan,
  acquisition log, patient detail and timeline, the mapping-evidence panels — plus the xlsx
  export are untouched, because mock mode cannot get a report into the list at all
  (`MockApiClient.listReports` returns a hard-coded empty page and never records what
  `requestReport` built). Giving the mock a reports store would unblock both those specs and
  a single-session walk from step 1 through to 13; until then the detail view is only
  reachable against a real BFF.
- **Cerner's branches.** Every fixture seeds Epic, so the sFTP census branch with its
  credential fields and connection test, and Location Org's Managing Organization and
  Location Type methods, are covered only by the assertions saying they are *absent* for
  Epic. Setting `facilityInfo.vendor` is all a Cerner spec needs to start.
- **The capability-on paths.** Every flag in the mock's `capabilities` is false, so the live
  halves of the census step (per-list validation, the results preview and its accuracy
  acknowledgement), HSLOC's third column and the report step's two census-pull tabs are also
  pinned only as absences. They want a mock whose capabilities a spec can turn on.
