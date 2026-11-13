# LEGLINK-1761 Concurrent and multi-EHR report generation QA

Cases for concurrent report generation on one EHR system, and for one launch that runs reports on more than one EHR system. The product behavior is on `feature/LEGLINK-1717-concurrent-report-generation`. This pass is branch `nm/LEGLINK-1761-qa-test-cases`.

An EHR system is a named FHIR base URL and its auth. The concurrency cap is an in-app setting (default 2, allowed 1 through 8). A launch may lower that cap for its batches. It cannot raise it. The settings page at `/Automation/ReportBatches` is the only place that saves a new cap.

## Unit execution

Command, from the worktree:

`dotnet test DotNet/Link.UI.Tests/Link.UI.Tests.csproj --nologo`

Date: 2026-11-12. First run: Passed 896, Failed 0, Skipped 0, Duration 26 s. After the restart-message fix: Passed 897, Failed 0, Skipped 0, Duration 24 s. After the closed-batch badge fix: Passed 899, Failed 0, Skipped 0, Duration 17 s.

These tests use an in-memory batch store and a fake run manager. They do not start Docker and they do not call a FHIR server. Every case below passed inside that run. The live pass is the separate section.

| Case | What it checks | Test | Result |
| --- | --- | --- | --- |
| S1 | Cap defaults to 2. A third run stays queued and starts when a slot frees. | `The_cap_defaults_to_two_until_a_value_is_saved`, `The_queued_run_starts_when_a_slot_frees` | Pass |
| S2 | A batch may lower the cap and cannot raise it. The saved cap must be 1 through 8. The settings page saves it. | `A_batch_may_lower_the_cap_and_cannot_raise_it`, `The_saved_cap_must_be_from_one_to_eight`, `A_cap_above_the_saved_cap_is_rejected`, `The_settings_api_reads_and_rejects_the_cap`, `The_settings_page_shows_the_default_and_saves_a_new_cap` | Pass |
| S3 | A saved cap applies to the next batch, not the one already dispatched. | `Saving_the_cap_is_what_the_next_read_returns`, `A_saved_cap_applies_to_the_next_batch_and_not_the_one_already_dispatched` | Pass |
| S4 | Copies repeat each selected scenario. Each copy is its own run. | `Starting_a_batch_repeats_each_scenario_and_keeps_the_lowered_cap` | Pass |
| S5 | One batch mixes an ad-hoc scenario and a scheduled scenario. Regenerate is grouped with scheduled. | `The_launch_page_defaults_copies_to_one_and_concurrency_to_the_saved_cap`, `Starting_a_batch_repeats_each_scenario_and_keeps_the_lowered_cap` | Pass |
| S6 | A second metrics run in the same batch waits. A metrics refusal from outside fails that item only. A normal run is still allowed. | `A_metrics_run_in_the_batch_waits_for_the_batches_own_metrics_run`, `A_metrics_refusal_from_outside_the_batch_fails_that_item_only`, `A_second_metrics_run_is_blocked_and_a_normal_run_is_not` | Pass |
| S7 | A failed start, or a failed run, does not stop the other items. | `A_failed_start_does_not_stop_the_other_runs`, `A_run_that_fails_does_not_stop_the_queued_run` | Pass |
| S8 | Cancel does not start queued items and cancels the running run. Finished items stay. The tracking page does the same. | `Cancel_does_not_start_queued_runs_and_cancels_the_running_one`, `Cancel_on_the_tracking_page_stops_an_open_batch` | Pass |
| S9 | Restart does not resume an in-flight run. It starts an item that never started. A finished run stays finished. If startup recovery already cancelled the run, the batch item still says it was interrupted by the restart. | `Restart_does_not_resume_an_in_flight_run_and_does_start_one_that_never_started`, `Restart_keeps_a_run_that_already_finished`, `Restart_keeps_interrupted_when_startup_recovery_already_cancelled_the_run` | Pass |
| S10 | Two runs on one EHR get different facilities. One success cleanup does not expunge the other run's creates. Two names on the same base URL share that hold. | `Two_runs_on_the_same_ehr_get_different_facilities`, `One_runs_success_cleanup_does_not_expunge_the_other_runs_imported_resources`, `Cleanup_keeps_a_tracked_create_another_run_on_the_same_ehr_still_claims`, `Two_named_ehr_systems_keep_separate_runs_and_the_same_endpoint_stays_protected` | Pass |
| M1 | Two EHRs start together under one LaunchId. Neither batch waits on the other. | `Two_EHRs_keep_their_own_scenarios_and_open_one_launch`, `A_launch_starts_one_batch_per_EHR_without_waiting` | Pass |
| M2 | Each EHR keeps its own scenarios, copies, and cap. | `Two_EHRs_keep_their_own_scenarios_and_open_one_launch` | Pass |
| M3 | A checked EHR that is not ready blocks the launch and creates nothing. An unchecked one does not. | `A_not_ready_EHR_blocks_the_whole_launch`, `An_unchecked_EHR_that_is_not_ready_does_not_block_the_launch` | Pass |
| M4 | Cancel one EHR leaves the other running. Cancel launch cancels every open batch. | `Cancelling_one_EHR_leaves_the_other_running`, `Cancelling_the_launch_cancels_every_open_EHR` | Pass |
| M5 | The launch page sums pending, in progress, completed, failed, and cancelled, and groups rows by EHR. | `Two_EHRs_keep_their_own_scenarios_and_open_one_launch`, `Cancelling_the_launch_cancels_every_open_EHR` | Pass |
| M6 | A different base URL does not hold the same resource path. The same base URL does. | `A_different_ehr_does_not_block_expunge_of_the_same_path`, `Two_named_ehr_systems_keep_separate_runs_and_the_same_endpoint_stays_protected` | Pass |
| M7 | If a later batch fails to create, batches already created for that launch are cancelled. | `A_second_batch_that_fails_to_create_cancels_the_first`, `A_failed_insert_cancels_batches_already_created`, `A_batch_saved_before_dispatch_fails_is_cancelled_with_the_launch` | Pass |

## Single EHR

Preconditions for S1–S10: the automation engine is ready, one EHR is configured, and at least one saved scenario has a patient source. Both period ends may be empty. One end, or an end before the start, is rejected.

- S1. Save no cap, or clear it. Start three runs. Expected: two are in progress, the third is pending, and the third starts when one of the first two finishes.
- S2. On the launch form, set max concurrent runs above the saved cap. Expected: the launch is rejected and no batch is stored. On the settings page, save 0 and save 9. Expected: both are rejected. Save 4. Expected: the page says new batches use a cap of 4.
- S3. Start a batch at cap 2, then save cap 4, then start another batch. Expected: the first batch still runs at 2. The second uses 4.
- S4. Select two scenarios and set copies to 2. Expected: the batch items are scenario A, A, B, B, and each item gets its own run.
- S5. Select one ad-hoc scenario and one scheduled scenario in the same launch. Expected: both are in the one batch. A regenerate scenario is listed under Scheduled.
- S6. Put two metrics scenarios in one batch. Expected: the second stays pending until the first metrics run finishes. Start a metrics run, then a batch whose next item is metrics while another metrics run is already active outside the batch. Expected: that item fails and the other items still run. A non-metrics run is accepted while a metrics run is active.
- S7. Make one start throw, and in another batch let one run fail. Expected: the other items still start.
- S8. While a batch has a running item and queued items, cancel it from the tracking page. Expected: queued items are cancelled and never start, the running run is cancelled, and items that already finished stay finished.
- S9. Stop the Link.UI process while a batch has one running item and one that never started, then start Link.UI again. Expected: the running item is cancelled with "Interrupted by process restart" and is not resumed. The item that never started is started. An item whose run already finished stays finished.
- S10. Run two reports at the same time on one EHR. Expected: they get different facility ids. When the first succeeds and cleans up, the second run's created resources are still on that EHR. Two EHR names that point at the same base URL share this hold.

## Multiple EHR systems

Preconditions for M1–M7: two EHR systems with different base URLs, plus the single-EHR preconditions. The launch form is `/Automation/ReportBatches/Launch`. One launch writes one batch per selected EHR and ties them with a LaunchId. The combined page is `/Automation/ReportBatches/Launches/{launchId}`.

- M1. Check two EHRs and start the launch. Expected: two batches, one LaunchId, and both start without waiting for each other.
- M2. Give each EHR its own scenario, its own copies, and its own cap at or below the saved cap. Expected: the batches keep those lists and caps. There is no launch-wide cap.
- M3. Check an EHR whose metadata probe is not ready, and check a ready one. Expected: nothing is created, and the error names the EHR that is not ready. Uncheck the EHR that is not ready and start again. Expected: only the ready EHR starts.
- M4. On the combined page, cancel one EHR. Expected: that batch is cancelled and the other stays open. Cancel the launch. Expected: every still-open batch is cancelled.
- M5. Open the combined page while both batches are running. Expected: the header sums pending, in progress, completed, failed, and cancelled across the launch, and the body is one group per EHR.
- M6. Run a report on each EHR at the same time, against different base URLs, including a resource path that exists on both. Expected: success cleanup on the first EHR does not expunge that path on the second. The same base URL under two names does hold the path.
- M7. Make the second batch fail after the first has been stored. Expected: the first batch is cancelled and nothing from that launch is left running.

## Live pass

I ran Link.UI from this worktree (`dotnet run`, Development) on `http://localhost:5280` against compose project `leglink-1761-qa`. That project does not use the scaffold containers. The scaffold SQL Server, Redis, and collector stayed up the whole time.

Two HAPI servers: `local` at `http://localhost:6157/fhir` and `sandbox` at `http://localhost:6257/fhir`. A third EHR, `down`, points at `http://127.0.0.1:6199/fhir` and nothing is listening there. `Patient/qa-seed` is on both HAPI servers. The saved cap for this pass is 4. The validation container is capped at 2 GB for this project. Uncapped, that JVM held about 7 GB and the host had under 1 GB free before any report started.

Evidence is under `dev-docs/leglink-1761/`. A captured status response is the record when I did not keep a screenshot.

| Case | Result | What I saw |
| --- | --- | --- |
| L1 | Pass | Settings page saved a cap of 4. The launch form then defaulted Max concurrent runs to 4. A later batch lowered its own cap to 2, which a batch is allowed to do. |
| L2 | Pass | Three copies on `local` with max concurrent 2. After one facility create failed, the other two were in progress. The queue itself is `The_queued_run_starts_when_a_slot_frees`. |
| L3 | Pass | The open launch page polled its status URL twice in five seconds. |
| L4 | Pass | Cancel while one item was running and one had no run: the running run was cancelled, and the queued item was cancelled without a run id. |
| L5 | Pass | History lists the batch, grouped under `local`. |
| L6 | Stopped | `local` and `sandbox` were both in progress under one launch. I cancelled before either finished because free RAM fell to 0.7 GB. |
| L7 | Pass | Checking `down` disabled Start launch and created nothing. |
| L8 | Pass | Cancel one EHR left the other in progress. Cancel launch then cancelled that one too. |
| L9 | Pass on re-run | The first restart copied the scenario-recovery cancel text. After the fix, the same steps recorded "Interrupted by process restart." and the item that had never started did start. |
| M6 | Stopped | Launch `bb061283-f873-44e0-a604-cc4db316743d` had `local` and `sandbox` in progress together. I cancelled at 0.98 GB free. Success cleanup did not run. `Patient/qa-seed` stayed HTTP 200 on both servers. |

### L1. Settings cap

I saved 4 on `/Automation/ReportBatches`. The page said new batches use a cap of 4. The launch form then showed Max concurrent runs of 4 and the help line that it defaults to the saved cap of 4. Screenshot: `dev-docs/leglink-1761/l1-settings-cap-4.jpg`.

### L2. Cap queues the third copy

Launch `28ebded6-214a-4879-9954-95809060cf24`, batch `56592f5d-6c8c-4d77-b1ad-589f871dfd67`, EHR `local`, Adhoc Report Test, copies 3, max concurrent 2. The screenshot is the read after one copy failed: At once 2, Pending 0, In progress 2, Failed 1. Item 0 was in progress (`194fe6cf-06e9-477c-97c7-5a3d6df2f2f1`). Item 1 failed (`7713c64e-8cf7-4575-8f75-a5997a23daa3`) with "Failed to create facility '7713c64e-8cf7-4575-8f75-a5997a23daa3'. HTTP 500". Item 2 was in progress (`8bb0ed0c-6bac-4189-bb12-2adec904b626`). Screenshot: `dev-docs/leglink-1761/l2-cap-queues-third-run.jpg`. The same counts are in `dev-docs/leglink-1761/l2-status.json`.

The tenant container log at 2026-11-12T23:18:36Z recorded SqlException 1205 for that facility create: the transaction was the deadlock victim. The same two-at-once create did not deadlock on L6: both facilities reached In Progress. I am not treating the single 1205 as a confirmed product defect. A third copy staying queued until a slot frees is the unit test `The_queued_run_starts_when_a_slot_frees`.

### L3. Live tracking interval

While launch `28ebded6-214a-4879-9954-95809060cf24` was open, the page requested `/Automation/ReportBatches/Launches/28ebded6-214a-4879-9954-95809060cf24/status` twice in five seconds. That matches the 2 second poll. A direct GET of that URL returned 200.

### L4. Cancel queued and running

Launch `ba55bbd0-a3a8-463f-8d2a-919b6d1061ed`, `local`, Adhoc Report Test, copies 2, cap 1. Before cancel, item 0 was in progress (`79bdc7f6-7664-4950-b297-dc419f51aaf7`) and item 1 was pending with no run. Screenshot: `dev-docs/leglink-1761/l4-queued-while-one-runs.jpg`.

After cancel (`dev-docs/leglink-1761/l4-cancel-queued.json`): the launch was closed, both items cancelled. Item 0 error was "Cancelled by user." Item 1 had no run id and error "Cancelled before start."

Cancel of the earlier launch `28ebded6-214a-4879-9954-95809060cf24` left the failed item failed and cancelled the two that were still running, both with "Cancelled by user."

### L5. History

`/Automation/ReportBatches/History` showed Recent batches, grouped by EHR. The `local` group included the batch from L2: Cancelled, 3 reports, At once 2, batch `56592f5d-6c8c-4d77-b1ad-589f871dfd67`, launch `28ebded6-214a-4879-9954-95809060cf24`. Screenshot: `dev-docs/leglink-1761/l5-history.jpg`.

### L6. Two EHRs at once

Launch `0dc644a8-6b6f-4b2b-b257-05709b186e3d`. One Adhoc Report Test on `local` (batch `55003a92-0e0e-4adf-9f00-3548d037b10a`, cap 1, run `4dd9969e-adb4-440e-ae53-1fdde124b191`) and one on `sandbox` (batch `4484e7e6-81de-4a11-8eb8-981fe2565364`, cap 1, run `d5459f0e-25e5-4340-a59b-95ddebc4c193`). Both were in progress together. Status: `dev-docs/leglink-1761/l6-both-running.json`.

Free RAM was 2.4 GB when I started that launch and 0.7 GB once both runs were in progress. I cancelled the launch before either report could succeed. Success cleanup therefore did not run. This is the M6 gap below.

### L7. An EHR that is not ready blocks the launch

Checking `down` together with `local` disabled Start launch. The page said `down` is not ready and that the connection to `127.0.0.1:6199` was refused. I did not submit the form, so no batch was created. Screenshot: `dev-docs/leglink-1761/l7-not-ready-blocks-launch.jpg`. The server-side rejection is the unit test `A_not_ready_EHR_blocks_the_whole_launch`.

### L8. Cancel one EHR, then the launch

On launch `0dc644a8-6b6f-4b2b-b257-05709b186e3d` I cancelled the `sandbox` batch only. `sandbox` became Cancelled with "Cancelled by user." `local` stayed In Progress and the launch stayed open. Status: `dev-docs/leglink-1761/l8-cancel-one.json`.

I then cancelled the launch. `local` became Cancelled with "Cancelled by user." and the launch was closed. Status: `dev-docs/leglink-1761/l8-cancel-launch.json`.

### L9. Process restart

Launch `c3985b88-3444-4ff5-b9a8-af25bf424be5`, `local`, Adhoc Report Test, copies 2, cap 1. Before the stop (`dev-docs/leglink-1761/l9-before.json`): item 0 in progress (`467998bd-7d69-48ed-8f25-ed49ed705ef3`), item 1 pending with no run id.

I stopped the only process listening on port 5280, confirmed the port was free, and started Link.UI once. After startup (`dev-docs/leglink-1761/l9-after.json`):

- Item 0 was Cancelled. It was not resumed. The error was "Cancelled by user (no active execution in this process)."
- Item 1 was In Progress on a new run, `94e32f2e-791e-461b-ba34-5f5693fe7229`.

The batch item did not show "Interrupted by process restart." The log line was scenario startup recovery cancelling run `467998bd-7d69-48ed-8f25-ed49ed705ef3`. Batch recovery then copied that run's error. The unit test calls batch recovery directly and still expects "Interrupted by process restart."

I cancelled this launch after that read so the new run would not keep going. That user cancel is not the restart result. The file `l9-after.json` is the status from before that cancel.

Startup recovery cancels the run before batch recovery reads it, so the batch was copying "Cancelled by user (no active execution in this process)." The batch now keeps "Interrupted by process restart." when that run is already cancelled. A run that already succeeded or failed is unchanged. The new test is `Restart_keeps_interrupted_when_startup_recovery_already_cancelled_the_run`. It failed on the old code and passed after the change.

Re-run, launch `e496cba9-112b-4aef-a02f-0362e1356beb`, same shape: copies 2, cap 1, `local` only. Before the stop (`dev-docs/leglink-1761/l9-rerun-before.json`): item 0 in progress (`3ed4fbd5-5b16-4b05-8ef3-63a733e23c8f`), item 1 pending with no run id. I stopped the only listener on port 5280, confirmed the port was free, and started Link.UI once. After startup (`dev-docs/leglink-1761/l9-rerun-after.json`):

- Item 0 was Cancelled and was not resumed. The error was "Interrupted by process restart."
- Item 1 was In Progress on a new run, `d83e88b9-6b42-4e6b-bda9-3a98e50900e7`.

The log still shows startup recovery cancelling run `3ed4fbd5-5b16-4b05-8ef3-63a733e23c8f` first. The batch item no longer repeats that text.

### M6. Cleanup across two EHR servers

Stopped. Two Adhoc Report Test runs, one on `local` and one on `sandbox`, did not both finish. Success cleanup did not run, so this pass does not show that cleanup on one EHR leaves the other EHR's data in place. The unit tests `A_different_ehr_does_not_block_expunge_of_the_same_path` and `Two_named_ehr_systems_keep_separate_runs_and_the_same_endpoint_stays_protected` still cover that rule in memory.

The live attempt is launch `bb061283-f873-44e0-a604-cc4db316743d`. `local` batch run `0c670fe4-c6ba-4ee2-acbd-01013246c76b` and `sandbox` batch run `11c898dc-7f18-48e1-9e6e-7d2f51b5ccce` were both in progress. Free RAM was 1.64 GB at the start and 0.98 GB on the next read. I cancelled the launch. Both items are Cancelled with "Cancelled by user." Status: `dev-docs/leglink-1761/m6-final.json`. The before patient ids are `dev-docs/leglink-1761/m6-patients-before.json`.

`Patient/qa-seed` was HTTP 200 on both servers before the launch and after the cancel. Before: 13 patients on `local` and 4 on `sandbox` (`dev-docs/leglink-1761/m6-patients-before.json`). After the cancel: 14 on `local` (added `Patient-56734ad9-001`) and 5 on `sandbox` (added `Patient-4d3b2c34-001`), in `dev-docs/leglink-1761/m6-patients-after-cancel.json`. Cancel does not expunge those creates. The same ids, including `qa-seed`, were still on the server they started on.

Two earlier tries on this same stack did not reach cleanup either. One died at facility create with HTTP 502 because `l1761-mock-dmrp` was not running. The next, launch `f540750a-db28-4aa5-bd64-4dc2b83b1252`, had both reports in progress until validation threw `Java heap space` inside its 2 GB cap and both runs aborted after validation was unavailable for 120 seconds. I restarted that container at the same 2 GB cap before this attempt. It was healthy, and this attempt was cancelled for host RAM before validation was the blocker.

A closed batch whose items all failed was showing Completed with the success badge. It now shows Failed. A mix of completed and failed items shows Partial with the warning badge. The tests are `A_closed_batch_badge_follows_its_items` and `A_launch_badge_follows_each_ehr_batch`. This cancelled launch did not exercise that badge.
