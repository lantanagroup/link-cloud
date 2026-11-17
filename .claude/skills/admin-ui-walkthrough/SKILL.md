---
name: admin-ui-walkthrough
description: Drive the running Admin.UI in a real browser via the Playwright MCP server to explore flows as a user would — surfacing defects with source file:line, and drafting Playwright specs from what was actually observed. Use when asked to walk, explore, or exercise Admin UI flows live, to hunt UI bugs, or to turn real behavior into e2e specs.
---

# Admin.UI live walkthrough

Explore the running app in a real browser, unscripted. Two deliverables — a **defect report**
and/or **draft specs**. Ask which is wanted if the request doesn't say; often it's both.

This is exploration, not test execution. If the goal is "do the existing tests pass",
run `npm run e2e` instead — don't drive the browser by hand.

## 1. Preflight

Check what's actually running before assuming a target:

```powershell
foreach ($p in 4200,8066,8063,5218) { $r = Test-NetConnection -ComputerName localhost -Port $p -InformationLevel Quiet -WarningAction SilentlyContinue; "{0,-6} {1}" -f $p, $(if ($r) { 'LISTENING' } else { 'closed' }) }
```

| Port | What | Notes |
|------|------|-------|
| 8066 | admin-ui docker container | **Serves a prebuilt bundle** — source edits are invisible here until rebuilt |
| 8063 | admin-bff | What the UI calls cross-origin |
| 4200 | `ng serve` | Proxies `/api` → **5218**, per `proxy.conf.json` |
| 5218 | local BFF | Usually not running |

Auth is off locally (`LOCAL_ADMIN_UI_AUTH_REQUIRED` defaults false in `docker-compose.yml`),
so there is normally no login gate. If one appears, that itself is worth reporting.

Load MCP tools in **one** ToolSearch call:

```text
select:mcp__playwright__browser_navigate,mcp__playwright__browser_snapshot,mcp__playwright__browser_click,mcp__playwright__browser_type,mcp__playwright__browser_press_key,mcp__playwright__browser_console_messages,mcp__playwright__browser_network_requests,mcp__playwright__browser_network_request,mcp__playwright__browser_take_screenshot
```

## 2. Rules of engagement

- **Never click `delete`, `Resubmit`, or `Deactivate` on data you did not create.** This is a
  shared dev stack.
- **Clean up what you do create**, and prefer the app's own delete flow to do it — that
  exercises another flow for free. Verify removal independently via the BFF afterward.
- **Never trigger native `alert`/`confirm`/`prompt`** — they freeze the MCP connection.
  Angular Material dialogs are DOM and perfectly safe.
- Confirm before any write the user didn't ask for.

## 3. The loop

For each step: act → snapshot → **check console and network**. Most defects here are
swallowed Angular errors that leave the page looking fine.

```text
browser_console_messages { level: "error" }          // all:false = this page only
browser_network_requests { static: false, filter: "/api/" }
browser_network_request  { index: N, part: "request-body" }
```

`all: true` on console returns the whole session including earlier origins — scope to the
current page before claiming an error is live.

Snapshots of this app are large. Always scope them: `depth: 10`, or `target` with a
selector (`#cdk-accordion-child-1`, `[role="dialog"]`). Element refs go stale after
navigation — re-snapshot rather than reusing an old `ref`.

Selector notes for this Material shell, each one a mistake worth not repeating:

- **Accordion headers** are not plain buttons. Use
  `button[aria-label*="Query Dispatch"], mat-expansion-panel-header:has-text("Query Dispatch")`.
- **Dialog action buttons carry no `aria-label`** — their accessible name is the visible text,
  so `button[aria-label*="Create…"]` matches nothing. Snapshot
  `[role="dialog"] .mat-mdc-dialog-actions` and click the ref it returns.
- **Refs go stale when a dialog opens or closes**, not just on navigation — the panel behind
  the dialog re-renders. Re-snapshot the panel after the dialog closes.
- Material keeps inactive tab panels in the DOM, so `[role="tabpanel"]:not([hidden])` is a
  strict-mode violation. Target the id (`#mat-tab-group-0-content-1`).
- Don't escape quotes inside `:has-text()` / `:text-is()` — an escaped quote fails with
  "engine expects a single string".

**Config panels fetch lazily on expand** (`(opened)` handlers). A panel's endpoint is not
called until its header is clicked — never conclude "the app never calls X" from a page load.

To find the source behind a minified stack trace, grep for the property name in the message,
not the chunk name:

```text
Grep { pattern: "dispatchSchedules", path: "Web/Admin.UI/src" }
```

### Probing the API directly

Useful for picking a facility in the right state. In PowerShell 5.1 always pass
`-UseBasicParsing` — without it `Invoke-WebRequest` tries the IE engine and dies with a
NonInteractive prompt error that *looks* like an HTTP failure.

## 4. Deliverable A — defect report

Per defect: what you did, observed vs expected, `file:line` of the cause, and an honest
severity. Distinguish "console error, page still works" from "user is blocked" — say which.
Include the request/response bodies when the defect is in an API contract.

## 5. Deliverable B — draft specs

Read `Web/Admin.UI/e2e/README.md` first; it is the contract. Conventions that matter:

- **Mocked** specs (`e2e/mocked/*.spec.ts`) import `test` from `../support/test`. An `/api`
  catch-all is auto-installed; any unmocked call 404s and **fails the test by name**, so
  every endpoint the flow touches needs an `api.mock(...)`.
- Register handlers as `api.mock('GET /api/facility/*', fixture)` — key is `METHOD /path`,
  trailing `*` is a prefix wildcard. Handler may be an object (200 JSON), a
  `{status, json}`, or a `(route) => …` function.
- Assert request shape with `api.callsTo('POST', path)` and `JSON.parse(postData)`.
- **Fixtures** (`e2e/fixtures/`) are typed TypeScript against `src/app/interfaces/...` so
  backend drift breaks the build. Never inline an untyped literal.
- **Page objects** (`e2e/support/pages/`) are the selector contract, shared by both tiers.
  Extend one rather than putting raw selectors in a spec. Prefer `getByTestId`, then
  role/label.
- **Create flows need a stateful mock** — the dialog re-fetches on close, so a static fixture
  leaves the panel looking empty. Have the GET 404 until the POST lands, then serve what was
  posted. See `e2e/mocked/query-dispatch-create.spec.ts` for the pattern.
- **Live** specs (`e2e/live/*.live.spec.ts`) import from `@playwright/test` directly, make
  shape-based assertions only, and **must assume nothing about seeded data**. If a flow needs
  a resource in a particular state, the spec has to create it and delete it again —
  self-cleaning, or it only passes once.

Encode what you *observed*, including exact error copy and payload shapes. A spec asserting
what you assumed is worse than no spec.

Run just the new file: `npx playwright test --project=mocked <file> --reporter=list`

## 6. Verifying a fix

The 8066 container will not show source edits. Either rebuild
(`docker compose up -d --build admin-ui`, full `npm ci` + `ng build`), or — much faster —
serve the edited source against the real BFF:

```powershell
# proxy config in the scratchpad, so proxy.conf.json stays untouched
# { "/api": { "target": "http://localhost:8063", "changeOrigin": true } }
npx ng serve --port 4300 --proxy-config <scratchpad>/proxy.8063.json
```

Then re-drive the exact flow that failed and confirm the console is clean. Full check:

1. `npx tsc --noEmit -p tsconfig.app.json`
2. re-drive the flow on the dev server, console scoped to the current page
3. `npx playwright test --project=mocked <related spec>`

## 7. Housekeeping

`.playwright-mcp/` (snapshots, screenshots, console logs) is gitignored. Screenshots can only
be written under the repo root — the scratchpad is outside the MCP server's allowed roots.
