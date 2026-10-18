# Demoing the Admin.UI Playwright suite

A 10-minute walkthrough for a mixed audience — engineers who will maintain these tests and
stakeholders who want to know what it buys us. Three acts: watch it drive the app, watch the
real suite run, then why the results can be trusted.

Everything runs from `Web/Admin.UI`. No backend, no Docker, no seeded data — the mocked tier
intercepts every `/api` call and Playwright boots `ng serve` itself.

## The commands, in order

Everything below runs from `Web/Admin.UI`. Two terminals: one holding `ng serve`, one you
type in. This section is the whole demo — the rest of the file is why, and what to say.

**Terminal A, before the meeting — leave it running, never touch it again:**

```powershell
cd Web\Admin.UI
npm start
```

**Terminal B, before the meeting — rehearse, then close it:**

```powershell
cd Web\Admin.UI
$env:E2E_SLOW_MO='0'; $env:E2E_STEP_PAUSE='0'; $env:E2E_KEEP_OPEN='0'; npm run e2e:demo
npm run e2e
```

Close that terminal afterwards. Those three variables are set for the rehearsal and would
make the real run sprint past and close itself; a fresh terminal is the reliable way to be
rid of them.

**Terminal B, in the meeting — one command per act:**

| Act | Type this | What happens |
|---|---|---|
| 1 | `npm run e2e:demo` | ~4.5 min. Opens on your largest external screen, paces itself, holds the window open at the end. **Close the window to finish.** |
| 2 | `npm run e2e` | 67 tests, ~20s warm |
| 2 | `npm run e2e:report` | Opens the HTML report in a browser |
| 3 | — | Nothing to run; Act 3 is the report plus the three mechanisms |

That is the whole set. No pacing variables, no window coordinates, no permission clicks.

**Optional, only if you want a specific monitor** — by name, not coordinates, and it must be
in the same terminal as the run:

```powershell
$env:E2E_MONITOR='<part of the monitor name>'; npm run e2e:demo
```

The walkthrough prints the screen it chose as its first line, so you can see before the
browser appears whether it took the variable. Leave it unset and it picks the largest screen
that is not your laptop.

**Optional, for the typed-fixtures point in Act 3** — rename a field in an interface under
`src/app/interfaces/`, then:

```powershell
npm run typecheck:e2e
```

One rename to `IReportSchedule.censusCount` produces three errors across the fixtures and the
specs that read it. Undo the rename afterwards.

## Pre-flight (do this before the meeting, not during)

The commands are in the section above. Two more that only matter on a machine that has not
run this before:

```powershell
npm ci                            # only if node_modules is stale
npx playwright install chromium   # only on a fresh machine
```

`playwright.config.ts` sets `reuseExistingServer: true`, which is why Terminal A matters: an
already-warm dev server removes the 30-60s first-compile pause from the middle of the demo.

**The pacing needs no setting.** `npm run e2e:demo` paces itself for a room out of the box:
~4.5 minutes, about eleven seconds a stage (measured 2026-10-17 on a 2400px-wide window).
The knobs are there if you want them:

```powershell
$env:E2E_STEP_PAUSE='3000'  # longer hold on each finished screen (default 2000)
$env:E2E_SLOW_MO='1500'     # quicker clicks within a stage (default 2200)
```

**You should not need to do anything about which monitor it opens on.** The walkthrough asks
Chrome which screens exist, picks the largest one that is not your laptop, and centres itself
on it. It prints what it chose as its first line:

```
Demo window on LG ULTRAWIDE (3440x1392) at 1690,-1407, 2418x1302
```

To choose a different screen, name it — any part of the label Chrome reports, case
insensitive:

```powershell
$env:E2E_MONITOR='vx2718'      # or 'ultrawide', 'dell', whatever yours is called
```

Get a wrong name and it says so and lists the real ones, rather than putting the window
somewhere you have to go and find it. After moving, it asks the browser which screen the
window actually ended up on and tells you if that is not the one it aimed for.

The window opens off-screen and moves into place once the page can report where the screens
are — it cannot be launched in the right spot, because the coordinate is not knowable until
the browser is running. A moment of nothing is better than the window appearing on your
laptop and jumping.

**Zoom is available but off.** `E2E_ZOOM='1.25'` enlarges the whole UI for a large room —
but it costs vertical space where this app can least afford it. On a 27" 1440p screen at
1.25 the facility page ends at the Weekly field, with all four configuration panels, the
things the walkthrough expands, pushed off the bottom. Prefer moving the monitor closer to
the room over zooming past what fits.

> **Do not hand it coordinates.** `E2E_WINDOW_POSITION` still works and still overrides
> everything, but every obvious way of obtaining a coordinate gives a *different* answer, and
> only one of them is the space Chrome positions windows in. On one workstation a monitor's
> left edge reads as `-1087` from `[System.Windows.Forms.Screen]::AllScreens`, `-1359` from
> the same call with DPI awareness enabled, and `-1381` from Chrome. Copying the first number
> put the window 294px inside the screen and its right-hand edge onto the next monitor, which
> is the kind of thing you discover with an audience watching. This runbook used to tell you
> to do exactly that.

Window size follows the chosen screen. `E2E_WINDOW_SIZE` overrides it if you want a specific
size, and is ignored when auto-detect is doing the placing.

## Act 1 — watch it drive the app (~4.5 min)

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
| Org Locations / Encounters tabs | |
| Opens configuration, expands each panel | |
| Back, then **creates** "Riverside Demo Hospital" through the dialog | The payoff: it fills the real form, and the POST body is asserted field by field. |
| The new facility appears in the list, and opens | The mocks are **stateful** — the POST writes into the same store the list reads. A static fixture would send the app to a facility the list never gained. |
| Its census panel offers to create one | That prompt only appears because the mock 404s for a facility with no config — we exercise the empty path on purpose. |
| Gives it a census cron and a dispatch schedule | End to end, through the UI, with the payloads checked. |
| Adds a FHIR query configuration — the EHR endpoint it will query, and how hard | The facility goes from empty to fully configured on screen: census, dispatch, EHR connection, query plan. |
| **Imports a query plan** — clicks **Import JSON** and picks `Scripts/seed_data/query_plan.json`, the real plan `seed.py` uses | Not a toy plan: two initial queries and ten supplemental resource types. That file is the seeder's *template* — its FacilityId is the placeholder `{{tenant-name}}` — and the walkthrough makes the same one-line substitution `seed.py` makes, so the plan arrives as this facility's own rather than something the dialog had to correct. |
| Back on the dashboard, both facilities listed | |
| Nav bar → **Reports**: every facility's reports in one table | Where an analyst actually starts, and the ad-hoc form is one button from here — the walkthrough reaches it the way the app does rather than by a URL typed from nowhere. |
| **Requests an ad-hoc report** for the new facility — report type, custom date range, census-based patient selection | The form the analysts actually use. The POST body is asserted field by field, same as the create dialog. |
| Lands on the report it just created — Adhoc cadence, Census source, the period you typed | The response carries `reportId`; reading the wrong field here is a bug the suite already caught once. The report itself is simulated from the request, the way the Report service builds one, so the header describes what was actually asked for instead of a stock fixture. |
| Its measure reports: a row per patient, with resource counts and reporting/submission status | Fixture data, but the real shape — the table, the donut charts and the statuses all come from the interfaces the app itself uses. |
| **Query Analysis** tab — total queries, per-resource timings, retries | The data acquisition logs behind that report: how many queries ran, how long each resource type took, what had to be retried. Only fetched when the tab is opened. |

**If someone asks about the tabs it does not fill in:** the new facility gets a census, a
dispatch schedule, a FHIR query configuration and a query plan. SFTP, FHIR List and Reporting
Organization are deliberately left empty — each shows its own "not configured" state, which is
honest rather than incomplete, and filling them would add minutes without making a new point.

If you skipped the pacing in pre-flight, this is the moment you will regret it — set
`E2E_SLOW_MO` and `E2E_STEP_PAUSE` as above. Measured durations, so you can pick a different
target: defaults ~1.5 min, `2200`/`2000` ~4.5 min, `2200`/`3000` ~5.4 min, pacing off
(`0`/`0`) ~15s.

`E2E_STEP_PAUSE` is the hold on each finished screen — raise it if the transitions feel
rushed. `E2E_SLOW_MO` is the gap between individual clicks *within* a stage, and it also
paces each scroll step, so it moves the total more than you expect. Below about 1500 the
form filling reads as a flicker rather than something a person did, which costs Act 1 its
point.

Close the browser window to end the run.

## Act 2 — the real suite (~3 min)

```powershell
npm run e2e          # 71 tests, 14 spec files, ~25s warm
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

**What to say, in this order.** The rest of this section is the material; this is the spoken
shape of it.

Open with the problem, in one sentence: *we had a UI nobody could verify without clicking
through it by hand — now every PR clicks through it in 25 seconds, and it has already found
five bugs that got past code review.*

Then the defects, named, with the detail that makes each one land — they are the ROI, and
they are checkable by SHA. Facility search is the one to lead with for a mixed room: broken
in a way nobody had noticed, hidden by seeded data that used the same value for ID and name,
and a *backend* bug found by a UI test. Then `[object Object]` shown to the user on save, the
query plan import that produced blank rows, and the `TypeError` firing on every render while
the suite sat green. The point to land is not that tests exist — it is that **none of these
were caught by review, and all of them were visible to anyone actually using the app.**

Then the three mechanisms, for the engineers: typed fixtures, the unmatched-API catch-all,
the console-error tripwire. Frame them as *why it will keep working*, not as features. Three
of the five defects above were found by the tripwire alone.

Expect the question "then why the headed walkthrough at all, if Act 2 is what gates merges?"
Two answers, and the second is the better one:

1. It makes the suite legible to people who will never open a spec file. A green checkmark
   persuades nobody; watching the app create a facility and request a report does.
2. **It found a defect the assertions missed.** The query plan import bug was found by asking
   what the demo *showed* rather than what it asserted — the payload assertion counted keys
   and passed throughout. A person watching caught what a passing test did not.

If someone pushes on coverage, concede the real limit before they find it: the mocked tier
proves the UI behaves correctly *given* a contract; it cannot prove the backend honours that
contract. That is what the live tier is for. Saying it unprompted buys more credibility than
it costs.

---

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
- Saving a census configuration told the user `[object Object]`. The dialog closed with the
  whole response while the edit screen interpolates that value into a snackbar; every other
  configuration dialog closes with `outcome.message`, and this one had drifted to `any` and
  lost the field. A console-error tripwire cannot catch this — `[object Object]` is a
  successful render of the wrong thing. Commit `54b67a5c5`.
- Importing a query plan produced the right *number* of queries with every field inside them
  empty. The file format is PascalCase — it is what the form's own **Export JSON** writes —
  while the API serialises camelCase, and the tables and both client-side checks read
  camelCase. Import mapped the plan's own fields and passed the query records through
  untouched, so Export → Import did not round-trip: blank rows, `Params: 0`, and an ordering
  check that saw no `queryConfigType` to check. Found by asking what the demo *showed* rather
  than what it asserted — the payload assertion counted keys and passed throughout.

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

## If someone asks whether an AI is driving this

They are two different things, and the distinction is worth getting right in the room —
the credibility of Act 3 rests on it.

**What you are showing is a script.** `e2e:demo` and `npm run e2e` are plain Playwright
tests: a `.spec.ts` file, the same on every machine and in CI, no model and no agent in the
loop. That is precisely why the results can be trusted and why the mocked tier can gate
merges. If you call the walkthrough "the AI driving the app", the first person who knows the
difference will ask, and the answer undercuts the point you just made.

**The agent's role was authoring, not running.** Specs here are written against the app as it
actually behaves: click through the running UI, watch what the network and the DOM do, then
read the component to find out *why* it does it. The Playwright **MCP server** is one way to
do that first half — an agent drives a real browser ad hoc, deciding what to click from what
is on screen, with the console and network in view. `tools/capture-fixtures.md` describes the
same loop for payloads. Nothing in the repo records which specs were authored that way, so
claim it only for work you did that way.

Both halves matter, and the shortest illustration is `mocked/account-create.spec.ts`:

- Driving the live UI is what found the **empty Roles dropdown** — required, opens onto
  nothing, leaves Create permanently disabled, with an empty `GET /api/account/role` behind
  it and **no console error**, because an empty list is not a failure. No amount of reading
  the component would have surfaced that; it depends on the state of the environment.
- Reading the component is what found the **409 duplicate-email branch** and the fact that
  the username is never typed — `account-config-form.component.ts:134` derives it as
  `firstName + '.' + lastName`. Clicking would never have reached either.

So: **the agent explores, the suite verifies.** One finds what is worth testing, the other
proves it still works on every PR.

**Do not run the walkthrough through an agent or tool.** Same reason it must not be
backgrounded or piped (see Act 1): the headed browser is reaped mid-stage and it looks like
the demo finishing early. Run `e2e:demo` in your own terminal. If you want to show live
exploration, do it as a separate beat, against the running app, after Act 3 — and let it find
something real rather than retracing a path the suite already covers.

## If something goes wrong mid-demo

| Symptom | Cause / fix |
|---|---|
| Long pause on first navigation | `ng serve` compiles each route on first hit. Pre-warm by visiting the routes once before the meeting. |
| Demo browser opens on the wrong screen | Read its first line of output, which names the screen it chose. Pick another with `E2E_MONITOR=<part of the name>`. If it says "window left where Chromium put it", auto-detect found one screen or none, and a stale `E2E_WINDOW_POSITION` in that shell overrides everything. |
| Window hangs off the edge of a screen | Something is passing `E2E_WINDOW_POSITION`. Clear it (`Remove-Item Env:\E2E_WINDOW_POSITION`) and let auto-detect place it. |
| Window closes immediately at the end | `E2E_KEEP_OPEN=0` is still set from the rehearsal. |
| An "Allow this site to manage windows?" prompt appears | The CDP permission grant did not take on this Chrome build. Click Allow once; the window then moves to the right screen. |
| Window vanishes partway through, no error visible | The run was interrupted, not failed — something reaped the parent process. Check `test-results/`: a real failure leaves `test-failed-1.png`, an interruption leaves only an orphaned `.playwright-artifacts-0`. Re-run in the foreground, unpiped. |
| It sits on one stage doing nothing | A selector stopped matching. The demo project caps actions at 30s (`actionTimeout`), so it now fails with the locator named instead of waiting forever — run `npm run e2e` to see whether it is a real break. |
| Flaky "the click did nothing" in the mocked tier | Too many workers starving one `ng serve`; the config caps at 4 locally for this reason. |
| Live tier fails to start | It needs the compose stack — a preflight pings the BFF and prints instructions. Skip it; the live tier is not part of this demo. |

## One-line summary

`npm run e2e:demo` to watch it, `npm run e2e` to trust it.
