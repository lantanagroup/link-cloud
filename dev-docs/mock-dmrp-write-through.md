# Mock DMRP write-through

In the lower environments, DMRP is played by the Mock DMRP API (`DotNet/MockDmrpApi`). With the mock
switched on, saving a facility in the Admin UI writes the facility's selected reports to the mock as
its enrollment, and Tenant then derives the facility's schedule from that enrollment the same way it
does from the real DMRP. Testers pick a facility's reports on the facility form as they do with DMRP
off, instead of seeding the mock by hand. The story is
[LEGLINK-1437](https://lantana.atlassian.net/browse/LEGLINK-1437).

## When it is on

All three must hold, read once when Tenant starts:

| Setting               | Read by                    | Meaning                                                              |
|-----------------------|----------------------------|----------------------------------------------------------------------|
| `DMRP:Enabled`        | Tenant                     | The DMRP module is hosted at all                                     |
| `MockDmrpApi:Enabled` | Tenant **and** MockDmrpApi | The mock is switched on                                              |
| `DMRP:Api:BaseUrl`    | Tenant                     | Where the mock is; the write-through has nowhere to write without it |

`MockDmrpApi:Enabled` is the mock's own switch, not a copy of it. In App Configuration it is **one
unlabeled row**, so both services see the same value and cannot disagree. A row labeled `MockDmrpApi`
would be invisible to Tenant. Tenant reads it the way the mock does (`bool.TryParse`), so a mistyped
value means off rather than a failed start. Production stores never carry the row, which is why the
write-through cannot turn on there and why the Link token is never sent to the real DMRP API.

Locally, compose gives Tenant and `mock-dmrp-api` the same variable, `MOCK_DMRP_ENABLED` (default
`true`). Changing any of the three takes effect when Tenant restarts.

### Who needs to know

`GET api/dmrp/dmrp-status` answers `{ dmrpEnabled, mockDmrpEnabled }`. It is the one DMRP route Tenant
serves while the module is off (`DmrpStatusOnlyControllerFeatureProvider` hides the others), so callers
always get an answer rather than having to read a 404 as "off".

- **Admin UI:** `DmrpStatusService` reads it once per session. It drives the Measure Mappings link,
  `DmrpGuard`, the Reporting Plans tab and the facility form. There is no UI-side DMRP setting; the old
  `LINK_DMRP_ENABLED` is gone.
- **Automation:** `FacilitySetupHelper.GetDmrpStatusAsync`. See [Automation.UI](#automationui) below.

## What a facility save does

`AddDmrpModule` registers `MockDmrpSyncFacilityOperations` around `DmrpFacilityOperations` only when
the write-through is on. With it off, nothing in this page applies and the DMRP path is unchanged.

**The selection is the whole enrollment, exactly as it is the whole schedule with DMRP off.** Saving
with nothing selected leaves the facility reporting nothing. That applies to API callers as well as the
form.

### Selection to measures

Each selected (dQM, frequency) pair is resolved through the measure mappings to **every** NHSN measure
mapped to that dQM at that frequency. Several measures sharing one dQM are all enrolled, and the
facility's schedule still lists the dQM once. A pair no mapping covers is refused with a 400 naming it
(`UnmappedDqmSelectionException`), before anything is written.

### Writing the mock

The write is a diff, so an unchanged selection writes nothing:

- **Periods:** the current reporting period in the facility's timezone and the next one, as LEGLINK-913
  seeds them. Other periods are never touched.
- **Scope:** only entries whose measure maps to a dQM, under either component. An entry for a measure
  with no dQM cannot appear on the form, so the form has no say over it.
- **Writes:** a selected measure with no reporting entry gets one (`MSC`, `isReporting = "Y"`); an entry
  the selection dropped is deleted, and so is an `N` entry, which the mock does not serve. A measure
  already reporting under `PS` stays as it is. A 409 on create means a concurrent save got there first.

### Create, update, delete

- **Create.** If the facility already exists, the mock is left alone and the host's duplicate check
  answers its 400; writing first would hand a live facility the enrollment of a create that is about to
  be refused. Otherwise the mock is written, and `DmrpFacilityOperations.CreateAsync` syncs the current
  period and derives the schedule.
- **Update.** The mock is written, then the current period is synced, then
  `IFacilityReportingPlanManager.WithdrawUnselectedAsync` marks plans for dropped measures as not
  reporting in **both** periods. The sync cannot do that part itself: it writes nothing when DMRP
  returns no entries, and it withdraws only within the components that answered. Next month's rows,
  once recorded, would otherwise survive into next month's schedule.
- **Hard delete.** After the facility is deleted, its mock entries are removed. A failure there is
  logged, not returned: the facility is already gone, and nothing reads mock entries for a facility Link
  does not have.
- **Soft delete and restore** leave the mock alone, as they leave the reporting plans alone.

A failed mock call is a `DmrpApiException`, so both the create and the update endpoint answer 502
"DMRP could not be reached".

## It is not atomic

The mock is written before Tenant's own save and is not part of its transaction:

- If Tenant refuses the save afterwards (validation, say), the mock is ahead of Link until the next
  save or nightly sync brings them back in line.
- A failure partway through the mock writes leaves some written and answers 502. Because the write is a
  diff, saving again converges.
- On update, the sync and the withdrawal save before the host's own update, as
  `DmrpFacilityOperations.UpdateAsync` already does without a transaction. If the host then refuses the
  update, Link's plans already match the mock, and the facility row keeps its old schedule until the
  next save.

This is test tooling, and these windows are accepted rather than engineered away.

## Automation.UI

Automation reads the status and saves facilities to match:

| Tenant | How Automation sets a facility's schedule |
| --- | --- |
| DMRP off | Posts it with the facility |
| DMRP on, no write-through | Creates the facility empty, writes reporting plans directly, saves it again so Tenant derives the schedule |
| DMRP on, write-through on | Ensures each measure has a mapping, then posts the schedule, as with DMRP off |
| No `dmrp-status` route (404) | Detects DMRP the older way, by the reporting-plans route, and assumes no write-through |

With the write-through on:

- DMRP-checkbox runs save the facility with its schedule (`EnsureDmrpFacilityWithScheduleAsync`)
  instead of seeding the mock and saving the facility empty, which would now un-enroll it.
- A vendor change sends the facility's stored schedule back rather than an empty one.
- The API Health step "Facility POST → 400" posts a dQM no mapping covers, so it still answers 400
  without enrolling anything.

The checkbox runs map `HOB` to the run's dQM. Once that mapping exists, every facility scheduled for the
dQM is enrolled in both `HOB` and the measure-equals-dQM mapping: two mock entries and two plans a month,
one schedule entry.

## Switching it on or off in an environment

The rows live in the private `link-cac` repository. To move an environment from today's labeled row to
the unlabeled one, **add first, then delete**:

1. Add an unlabeled `MockDmrpApi:Enabled` row.
2. Delete the `MockDmrpApi`-labeled row.

The mock reads unlabeled rows too, so it stays on throughout. Deleting first turns the mock off until the
unlabeled row exists. Restart Tenant afterwards; it reads the switch only at startup.

## Code map

| Piece | Where |
| --- | --- |
| The switch and its reading | `DotNet/DMRP/MockDmrp/MockDmrpStatus.cs` |
| The write-through | `DotNet/DMRP/MockDmrp/MockDmrpSyncFacilityOperations.cs` |
| Registration, and the status-only filter with DMRP off | `DotNet/DMRP/DependencyInjection/` |
| The status route | `DotNet/DMRP/Controllers/DmrpStatusController.cs` |
| Withdrawal | `FacilityReportingPlanManager.WithdrawUnselectedAsync` |
| The mock client | `DotNet/LinkSdk/Clients/MockDmrpServiceClient.cs` |
| 502 on update | `FacilityController.PutFacility` |
| Admin UI | `Web/Admin.UI/src/app/services/gateway/dmrp/dmrp-status.service.ts` |
| Automation | `FacilitySetupHelper.GetDmrpStatusAsync`, `EnsureDmrpFacilityWithScheduleAsync`; `RunExecutor` |
