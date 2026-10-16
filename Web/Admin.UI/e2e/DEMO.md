# Demoing the Admin.UI Playwright suite

A 10-minute walkthrough for a mixed audience — engineers who will maintain these tests and
stakeholders who want to know what it buys us. Three acts: watch it drive the app, watch the
real suite run, then why the results can be trusted.

Everything runs from `Web/Admin.UI`. No backend, no Docker, no seeded data — the mocked tier
intercepts every `/api` call and Playwright boots `ng serve` itself.

## Pre-flight (do this before the meeting, not during)

```powershell
cd Web\Admin.UI
npm ci                                  # only if node_modules is stale
npx playwright install chromium         # only on a fresh machine
npm start                               # leave ng serve running — see note below
```

Leave `ng serve` up in its own terminal. `playwright.config.ts` sets
`reuseExistingServer: true`, so an already-warm dev server removes the 30-60s first-compile
pause from the middle of the demo. Then rehearse once:

```powershell
$env:E2E_KEEP_OPEN='0'; npm run e2e:demo   # ~15s with pacing disabled, ~4 min at demo pacing
npm run e2e                                # 64 tests, ~30s warm
```

Both should be green. Clear `E2E_KEEP_OPEN` afterwards (`Remove-Item Env:\E2E_KEEP_OPEN`) so
the real demo holds the window open at the end.

If you have a second screen, park the demo browser on it — otherwise it opens over your notes.
Ask Windows where your screens are rather than reusing coordinates from someone else's setup:

```powershell
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Screen]::AllScreens | Select-Object DeviceName,Primary,Bounds
```

`Bounds` gives each screen's `X,Y` top-left in the virtual desktop. Pass the one you want as
the window position:

```powershell
$env:E2E_WINDOW_POSITION='<X>,<Y>'   # the top-left from Bounds, e.g. '2560,0'
$env:E2E_WINDOW_SIZE='1600,1300'
```

The primary screen's origin is `0,0`; screens placed above or to the left of it have negative
coordinates, which is normal. Re-run the command whenever you rearrange or reconnect
displays — the numbers move.

## Act 1 — watch it drive the app (~4 min)

```powershell
npm run e2e:demo
```

Run it **in your own terminal, in the foreground**. Do not background it, pipe it, or launch it
from a tool or agent: the walkthrough is one long-lived headed browser, and anything that
reaps the parent process closes the window mid-stage. That failure looks exactly like the demo
"finishing early" — the browser vanishes partway through with no error on screen, and because a
pipe buffers the reporter's output you do not even get a verdict. Pipe nothing; watch the
terminal.

A real Chromium window opens and drives itself. This is the `demo` Playwright project: headed,
`slowMo`-paced, test timeout disabled, and it waits on the window's close event at the end so
you can talk over the finished screen. It is deliberately **not** part of `npm run e2e`, so CI
never blocks on it.

What it does, and what to say while it happens:

| On screen | Talking point |
|---|---|
| Tenant dashboard loads | Nothing is running but the UI. Every API response here is a fixture. |
| Drills into a facility | These are the same page objects the real tests use — one selector contract. |
| Filters reports by status, searches by id, toggles deleted | This is the behaviour we used to verify by hand every release. |
| Locations / Encounters tabs | |
| Opens configuration, expands each panel | |
| Back, then **creates** "Riverside Demo Hospital" through the dialog | The payoff: it fills the real form, and the POST body is asserted field by field. |
| The new facility appears in the list, and opens | The mocks are **stateful** — the POST writes into the same store the list reads. A static fixture would send the app to a facility the list never gained. |
| Its census panel offers to create one | That prompt only appears because the mock 404s for a facility with no config — we exercise the empty path on purpose. |
| Gives it a census cron and a dispatch schedule | End to end, through the UI, with the payloads checked. |

Pacing levers, if it is running too fast or too slow to narrate:

```powershell
$env:E2E_SLOW_MO='1800'     # slower individual clicks (default 700)
$env:E2E_STEP_PAUSE='3000'  # longer dwell between stages (default 1500)
```

Close the browser window to end the run.

## Act 2 — the real suite (~3 min)

```powershell
npm run e2e          # 64 tests, 13 spec files, ~30s warm
npm run e2e:report   # opens the HTML report
```

Let it run while you talk. The point to land: the walkthrough in Act 1 is for humans, this is
what actually gates merges — same page objects, same fixtures, headless and parallel.

The HTML report is the artifact to show. Worth clicking into one test to show the step
breakdown. If you want to show the trace viewer, break a selector in a spec beforehand —
`trace: 'retain-on-failure'` is already configured, so a failure yields a full DOM-level replay
with network and console attached. That is the "how long does it take to debug a CI failure"
answer.

CI wiring, in one breath: the mocked tier runs on every PR as its own job (`Admin UI Tests
(mocked)`) and needs no backend, so it is cheap enough to gate everything. The live tier — 12
specs, 3 files — runs inside `backend-e2e-tests` against the compose stack once it is healthy,
and uploads its report as the `admin-ui-e2e-report` artifact. Both in
`.github/workflows/tests.yaml`.

## Act 3 — why the results can be trusted (~3 min)

Three design decisions, each with a bug to show for it. For a mixed room, lead with the bugs.

**It caught real defects.** Mention them by name; they are the ROI slide.

- A `TypeError` fired on *every* render of the query dispatch panel while the suite sat green.
  Playwright does not fail on console errors by default, so an Angular lifecycle throw left the
  page looking fine and the assertions passed. Adding a console-error tripwire
  (`e2e/support/test.ts`) turned it red and surfaced two bugs at once: the `ngOnChanges`
  dereference, and an `NG0100` from the loading indicator. Commit `4fb1d11f8`.
- Facility search was broken in a way nobody had noticed: the tenant dashboard's one search box
  fed two endpoints with different matching rules, so typing part of a name listed the facility
  in the dropdown while the table underneath said "No tenants found", and typing a facility ID
  matched nothing at all. It stayed hidden because nearly every seeded facility uses the same
  value for its ID and its name. Commit `2666b43e6` — a backend fix, found by a UI test.
- Rehearsing this runbook turned up another: the ad-hoc report fixture returned `{ id }` while
  the API returns `{ reportId }` (`GenerateAdhocReportResponse`), so the post-submit navigation
  routed to an `undefined` path segment. The spec's own payload assertions all passed; only the
  tripwire noticed. Fixed in `generate-report.spec.ts`, which now also asserts the redirect.

**The three mechanisms behind that**, for the engineers in the room:

1. **Typed fixtures, enforced in CI.** `e2e/fixtures/` is TypeScript against the app's own
   interfaces in `src/app/interfaces/...`, so renaming or retyping a field makes every stale
   fixture a compile error. Playwright transpiles with esbuild and does *not* typecheck, so
   that only bites if `tsc` actually runs — `npm run typecheck:e2e` (config in
   `e2e/tsconfig.json`) is wired into the mocked CI job ahead of the browser download. Demo it
   by renaming a field in an interface and running the script: one rename to
   `IReportSchedule.censusCount` produces three errors across the fixtures and the specs that
   read it. Precise scope, if someone asks: this catches drift between the UI's interfaces and
   the fixtures, not between the .NET models and the UI's interfaces.
2. **The `/api` catch-all.** Any endpoint a spec forgot to mock returns 404 and lands in
   `api.unmatched`, which is asserted empty automatically. A missing fixture cannot pass quietly.
3. **The console-error tripwire.** `console.error` and uncaught exceptions fail the test, deduped
   so one lifecycle error does not print fifty times. Specs opt out explicitly
   (`allowConsoleErrors`) — `api-errors.spec.ts` does, since driving failure paths is its job.

Closing line: the mocked tier is fast and deterministic enough to run on every PR, and the live
tier proves the same page objects still work against a real BFF. The selector contract lives in
`e2e/support/pages/`, so adding a screen means adding a page object, not rewriting selectors.

## If something goes wrong mid-demo

| Symptom | Cause / fix |
|---|---|
| Long pause on first navigation | `ng serve` compiles each route on first hit. Pre-warm by visiting the routes once before the meeting. |
| Demo browser opens on the wrong screen | `E2E_WINDOW_POSITION` no longer matches your display layout — re-run the `AllScreens` command. `0,0` is the primary screen. |
| Window closes immediately at the end | `E2E_KEEP_OPEN=0` is still set from the rehearsal. |
| Window vanishes partway through, no error visible | The run was interrupted, not failed — something reaped the parent process. Check `test-results/`: a real failure leaves `test-failed-1.png`, an interruption leaves only an orphaned `.playwright-artifacts-0`. Re-run in the foreground, unpiped. |
| Flaky "the click did nothing" in the mocked tier | Too many workers starving one `ng serve`; the config caps at 4 locally for this reason. |
| Live tier fails to start | It needs the compose stack — a preflight pings the BFF and prints instructions. Skip it; the live tier is not part of this demo. |

## One-line summary

`npm run e2e:demo` to watch it, `npm run e2e` to trust it.
