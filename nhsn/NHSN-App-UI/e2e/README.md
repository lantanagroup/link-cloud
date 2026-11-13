# NHSN-App-UI end-to-end tests

Playwright, run from `nhsn/NHSN-App-UI`. Modelled on `Web/Admin.UI/e2e`, but the mocking
strategy is deliberately different — see **Why there is no `/api` interceptor** below.

```bash
npm run e2e            # mocked tier, headless — the default
npm run e2e:ui         # same, interactive
npm run e2e:report     # last HTML report
npm run e2e:bff        # BFF tier; needs NHSN-App-BFF on :8079 (preflight fails fast).
                       # The 13-step walk needs env vars too — see The BFF tier
npm run e2e:demo       # headed, paced walkthrough of onboarding — to be watched, not to verify
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
  bff/      *.bff.spec.ts    against a real NHSN-App-BFF: a smoke set, plus two exploration
                             specs (explore = wire capture, walk = all 13 steps) that assert
                             nothing — see The BFF tier
  demo/     *.demo.spec.ts   headed, paced walkthrough meant to be watched
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

Thin, but **not** because the endpoints are missing. This section and `support/bff.ts` both
used to say they were, following `NHSN-App-UI/README.md`, which is stale: phase 1 landed in
LEGPROG-515 (`9771759e9`, 2026-10-23), and `OnboardingEndpoints` maps the draft read and write,
import, export and completion under `/api/nhsn-app-bff/onboarding`.

BFF mode needs a signed-JWT profile whose issuer, `kid` and PKCS#8 key match the BFF's
`NhsnJwt` config. `docker-compose.yml` supplies those to the shell's runtime server as
`NHSN_APP_UI_DEFAULT_JWT_*`, served at `/shell-config.js`, which the dev server does not
serve — which is why this section used to say a real BFF run needs the composed shell (:8090).
**It does not.** `seedTestProfile()` writes the profile straight into `localStorage`,
bypassing `/shell-config.js` entirely, and `webpack.app.config.js` already proxies `/api` to
`http://localhost:8079`. The dev server on :4300 plus the composed BFF is enough, and that is
what the tier runs against. The keypair is the committed local-development one from
`docker-compose.yml`; the BFF holds its public half as `NhsnJwt__PublicCertificatePem`.

**Use a fresh facility id per run.** The real BFF persists the draft server-side, keyed on the
token's `facility` claim, so re-using an id resumes where the last run stopped — which is
exactly how `explore.bff.spec.ts` failed the first time it was re-run. The specs mint
`` `E2E-WALK-${Date.now()}` ``. Nothing cleans up after itself: every run leaves a facility and
a draft behind in the BFF, Tenant and DMRP.

### Walking all 13 steps against a live stack

`walk.bff.spec.ts` completes the whole of onboarding — welcome through Enrollment Complete —
in about 26s, but only when told which FHIR server to use:

```powershell
$env:E2E_VENDOR   = 'Epic'
$env:E2E_FHIR_URL = 'https://ehr-test.nhsnlink.org/fhir'
$env:E2E_LIST_IDS = '49412c14-b350-44f4-ab5d-88595dddfa35,49ad68f2-f1cb-4e02-b83f-62ea0935eaeb,4e059567-4ccc-4de7-900e-a8e99a7b74b0,5f902961-ce84-49d1-a37d-3e9c8522812c,69978a74-7f4f-4199-9021-09c29013d3ba,8446bb4e-bba9-4680-ae64-5a7002fc7f7d'
npx playwright test --project=bff e2e/bff/walk.bff.spec.ts --reporter=list
```

To watch it: add `--headed`, with `E2E_SLOW_MO=400` to pace it and `E2E_KEEP_OPEN=1` to hold
the window on step 13. Both default off, so a CI run is unaffected.

**The compose HAPI server cannot get past census**, which is the whole reason for `E2E_FHIR_URL`.
Census needs Epic-style patient lists, HAPI has none, so `/patients-of-interest/list-queries`
answers 424 ("Unable to read list …") and the accuracy acknowledgement silently refuses to tick —
`handleAckChange` needs all six lists to have *returned*. Point it at `ehr-test.nhsnlink.org`
with six real `List` ids and census clears: 200, 3-5 patients per list, `simulated: false`.
Regenerate the ids if they ever go away:

```bash
curl -s -H "Accept: application/fhir+json" "https://ehr-test.nhsnlink.org/fhir/List?_count=10" \
  | grep -oE 'List/[0-9a-f-]{36}' | sed 's#List/##' | sort -u | head -6 | paste -sd, -
```

Cerner stays blocked either way: its `censusAcquisition` is `Sftp`, wanting a host, a port and a
live file listing, and the stack has no SFTP server. Those two are the only vendors the BFF offers.

**The walk goes backwards on purpose** between steps 4 and 2, logging `RE-SAVING reporting-plan
now that the facility exists`. That is [LEGLINK-1775](https://lantana.atlassian.net/browse/LEGLINK-1775):
measure auto-enrollment runs on the reporting-plan step's save, which for a new facility happens
*before* facility-info creates it, so DMRP 400s, the BFF swallows it, and step 10's measure picker
is empty with no error. Without the detour the walk dies at step 10. Remove it when 1775 is fixed.

**`walk.bff.spec.ts` and `explore.bff.spec.ts` are exploration, not verification.** Their only
assertion is `expect(true)` — they log where they got to and what the wire said, because a
failure there is information rather than a defect. So `npm run e2e:bff` goes green whether the
walk reaches step 13 or stops at census; read the `===== REACHED =====` block to find out which.
Only `smoke.bff.spec.ts` actually asserts anything.

One trap in the smoke test, and it is general: **an authorization assertion must name a route
that exists, and must not accept 404.** Routing rejects an unmatched path before authorization
runs, so `GET /api/nhsn-app-bff/onboarding/draft` — a path the BFF does not map — returns 404
whether the endpoints are protected or wide open, and a spec that accepts 404 passes either
way. The real draft read is `GET /api/nhsn-app-bff/onboarding`, which answers 401 unauthenticated.
`Authentication:AllowAnonymous`, true in the Development and Docker settings, does not change
that: it governs the BFF's own outbound calls to Link services, not its endpoints.

## The demo tier

`e2e/demo/onboarding-walkthrough.demo.spec.ts` is **not a verification tier**. It is one
continuous session walking a facility from the welcome screen to the recap, headed and paced so
a person can follow it, and it records a `.webm` for anyone who was not at the machine. Each
beat is wrapped in its `stage()` helper — a `test.step` plus a dwell — with a lowercase,
human-readable title, because those titles are the narration.

```bash
npm run e2e:demo                              # default pacing, stays open, records video
$env:E2E_SLOW_MO='1800'; npm run e2e:demo     # slower individual actions (default 700ms)
$env:E2E_STEP_PAUSE='3000'; npm run e2e:demo  # longer dwell between stages (default 1500ms)
$env:E2E_KEEP_OPEN='0'; npm run e2e:demo      # run straight through and close
$env:E2E_VIDEO='off'; npm run e2e:demo        # skip the recording
$env:E2E_WINDOW_SIZE='1600,1000'              # and E2E_WINDOW_POSITION for a chosen monitor
```

The recording lands at `test-results/<test-dir>/video.webm` (~770KB for a zero-paced run,
larger when paced). `test-results/` is gitignored, so copy it out before the next run.

**Verifying a change to it without watching it:** run at zero pacing —
`$env:E2E_KEEP_OPEN='0'; $env:E2E_SLOW_MO='0'; $env:E2E_STEP_PAUSE='0'; npm run e2e:demo`.
The full walk finishes in ~10s. The browser is still visible; the project forces
`headless: false` and there is no flag to force it back.

**It does not walk 1 → 13 unbroken, and cannot.** Step 11 is the dead end described under
*Characterisation tests*: no report ever reaches `listReports`, so the acknowledgement Continue
needs can never be given. The walkthrough stops there, shows the wall deliberately as its own
stage, and re-enters at step 12 from a seeded draft — the same seam the specs for steps 12 and
13 live with. **LEGLINK-1771** is the enabler that removes it; when it lands, those two stages
collapse into a plain Continue.

Three stages show filed defects rather than intended behaviour (**LEGLINK-1762** the missing
table and empty state, **LEGLINK-1767** the rule demanded of a single-MRN facility,
**LEGLINK-1768** the simulated connection test recorded as a plain "Passed"). Their titles name
the ticket, so nobody watching mistakes them for features — and so that whoever fixes one knows
to come back here. Keep that convention if you add a stage over a known defect.

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

- **The Report Results detail view.** `ReportDetailView` and its modals — query plan,
  acquisition log, patient detail and timeline, the mapping-evidence panels — plus the xlsx
  export are untouched, because mock mode cannot get a report into the list at all
  (`MockApiClient.listReports` returns a hard-coded empty page and never records what
  `requestReport` built). Giving the mock a reports store would unblock both those specs and
  a single-session walk from step 1 through to 13; until then the detail view is only
  reachable against a real BFF. It genuinely is reachable there — against the live BFF a
  just-requested report is in `listReports` on the first poll, so this is a mock limitation
  and nothing more.
- **Cerner's branches.** Every fixture seeds Epic, so the sFTP census branch with its
  credential fields and connection test, and Location Org's Managing Organization and
  Location Type methods, are covered only by the assertions saying they are *absent* for
  Epic. Setting `facilityInfo.vendor` is all a Cerner spec needs to start.
- **The capability-on paths.** Every flag in the mock's `capabilities` is false, so the live
  halves of the census step (per-list validation, the results preview and its accuracy
  acknowledgement), HSLOC's third column and the report step's two census-pull tabs are also
  pinned only as absences. They want a mock whose capabilities a spec can turn on.
  This is the widest gap in the suite: the **live BFF has at least `patientListWithNames`,
  `sftpFileListing` and `fhirConnectionProbe` on**, so those halves are real, shipped surfaces
  that no tier exercises — and the absence assertions read as a contract while only recording
  the mock's configuration.
