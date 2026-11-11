# Metadata volume model (LEGPROG-516)

Projects how much non-clinical operational data and metadata Link holds, and how fast it's
written, at the LEGPROG-516 target scale. LEGLINK-1364 built it. LEGLINK-1369 and the
service-specific enablers (LEGLINK-1365, 1366, 1370–1374) replace its placeholder multipliers
with measured ones.

The method, the assumptions behind each input and the divergences from ADR 08 and LEGLINK-1242
are documented in the model document, "Metadata Volume Model: Assumptions and Method". This file
covers how to run and update the model.

`snapshot.py` and `diff_snapshots.py` measure the multipliers from a real pipeline run; see
[Capture](#capture). The model itself runs from the repository root on Python 3.11+ with the standard
library only.

```powershell
python Scripts/capacity_model/model.py                                    # Markdown report to stdout
python Scripts/capacity_model/model.py --out sizing.md
python Scripts/capacity_model/model.py --format csv --out sizing.csv     # one row per store
python Scripts/capacity_model/model.py --window-months 3                 # per-store table at 3 months
python Scripts/capacity_model/model.py --set share_both=0.45 --set completion_lag_days=3
python Scripts/capacity_model/model.py --exclude-tag shadow --exclude-tag rubric
python -m unittest discover Scripts/capacity_model/tests
```

The model itself connects to nothing and needs no credentials.

## Quick start: measuring a run

The steps are the same in every environment:
1. A full snapshot before the run.
2. A light series during it.
3. A full snapshot after it, once every report is terminal.
4. A diff.

[Capture](#capture) has the details.

**Setup, once:**

```powershell
python -m venv $env:USERPROFILE/.venvs/link-capacity
& $env:USERPROFILE/.venvs/link-capacity/Scripts/python -m pip install -r Scripts/capacity_model/requirements-capture.txt
$py = "$env:USERPROFILE/.venvs/link-capacity/Scripts/python"
```

**Local Compose stack.** The default targets file reads `LINK_DB_PASS` and the Kafka SASL user from
the repository's `.env`, so no flags are needed.

```powershell
& $py Scripts/capacity_model/snapshot.py --label before --out before.json
& $py Scripts/capacity_model/snapshot.py --interval 15 --previous before.json --out run.jsonl   # second terminal; Ctrl+C to stop
./Scripts/run-adhoc-reporting-smoke-test.ps1                                                    # or any other run
& $py Scripts/capacity_model/snapshot.py --label after --previous before.json --out after.json
python Scripts/capacity_model/diff_snapshots.py --before before.json --after after.json --series run.jsonl --out-dir diff --tooling-check
```

A local run proves the tooling. It doesn't produce numbers for the model.

**Deployed environment** (LCG, CDC, …):
1. **Write a targets file for the environment, outside this repository.** The repository is public,
   so its hostnames don't belong here. Copy `targets/docker-compose.json` and set the SQL server or
   per-service connection strings, the Cosmos Mongo connection string and `cosmos.resource_id`, the
   Kafka brokers and security, the Loki URL, and `replicas` (`kubectl` with namespace and context, or
   `none`). Make every credential a `${VAR}`, supplied from your shell or a private `--env-file`.
2. **Run from somewhere that can reach every endpoint,** using `kubectl port-forward` for in-cluster
   services. Use a read-only SQL login with VIEW DATABASE STATE, plus VIEW SERVER STATE where the
   server allows it. For Cosmos RU and 429s, `az login` with Reader on the Cosmos account.
3. **Check connectivity** with a full snapshot and a recent `--since`, so every source is exercised:
   `snapshot.py --targets <file> --env-file <file> --since <five minutes ago, ISO> --out check.json`.
   Then clear any `unavailable` items you didn't expect.
4. **Run the same four commands as above,** adding `--targets <file> --env-file <file> --code-version
   <release>` to each `snapshot.py`. Use a 30–60 s `--interval` for long runs. Drop `--tooling-check`.
   - In a shared environment, pass `--report-ids <file>` to the after snapshot, so only your run's
     reports count.
5. **Read `diff/summary.md`.** For a representative run only, record it with `diff_snapshots.py --apply`
   (see [Recording a measurement](#recording-a-measurement-leglink-1369)).

## Inputs

**`assumptions.json`** holds the global parameters. Each one is `{ "value": ..., "source": "..." }`.
To change an assumption, edit the value and update its source. `--set NAME=VALUE` overrides a
value for one run, which is useful for sensitivity checks. `VALUE` is parsed as JSON.

**`stores.json`** has one entry per in-scope store, plus an `excluded` list giving the reason each
other store is left out.

| Field | Meaning |
|---|---|
| `id`, `service`, `kind`, `store` | Identity. `kind` is `sql`, `mongo`, `kafka` or `loki`. |
| `grain` | What one arrival unit is: `patient_report` (one patient in one report), `report`, `census_patient` (one unique patient per facility per month), `facility_day`. |
| `multiplier` / `multiplier_param` | Rows (documents, messages; bytes for Loki) per grain unit, either literal or taken from an assumption. |
| `multiplier_status` | `measured`, `prior` (from earlier analysis), `placeholder`, `schema` (fixed by the data model), or `design` (fixed by an unbuilt design). |
| `multiplier_source` | Where the value came from. For a measured value, give the environment, date and ticket. |
| `bytes_per_row` | Average bytes per row, including indexes. These start as schema-based estimates. |
| `anchor` | What starts a row's retention clock: `report` (report completion + window), `record_age` (the record's own date + window), `own_retention` (the system's own setting, such as Kafka `retention.ms`), `fixed_residency` (a work queue that clears itself), or `unbounded` (never removed). |
| `residency_days` / `residency_days_param` | Residency for `own_retention` and `fixed_residency` stores. Set `residency_param_unit` to `hours` when the parameter is in hours. |
| `copies_param` | A replication multiplier on bytes (Kafka). |
| `adr_action`, `analysis_action`, `pi`, `divergences`, `tags`, `notes` | Documentation only. `tags` drive `--exclude-tag`. |
| `location` | Where capture finds the store; see the table below. `model.py` ignores it. |
| `measured_by_cadence` | Written by `diff_snapshots.py --apply`: the daily and monthly multipliers, kept for the record. `model.py` ignores it. |

| `location` kind | Fields |
|---|---|
| sql | `service` (a key in the targets file's `sql.databases`), `table` (`schema.table`) or `tables` (summed), and optionally `attribution`: `{"key": column}`, or `{"key": "p.Column", "from": "<table> t JOIN ... p ON ..."}` when the report ID sits on a parent row |
| mongo | `service`, `collection` (matched case-insensitively), `report_key` (dotted path to the report ID inside an array of report entries) |
| kafka | `topics`: summed |
| loki | `selector` (LogQL stream selector) and `group_by` (label) |
| any | `{"measured_with": "<store id>"}` when two model rows share one physical table, as the two `AuditLogs` rows do |

Store `location`s are trusted input: `capture.common.location_errors` restricts table, key and join
text to plain identifiers, and the model's tests fail on any store without a usable location.

## Capture

Measuring a run takes three steps:
1. A **full snapshot** before the run.
2. A **light series** during it, optional but needed for write rates, queue depth, blocking and lag.
3. A **full snapshot** after it.

`diff_snapshots.py` then turns them into multipliers, rates and contention, and proposes a
`stores.json` update. It changes the model only with `--apply`.

### Setup

The capture scripts need client libraries. `model.py` and `diff_snapshots.py` don't.

```powershell
python -m venv $env:USERPROFILE/.venvs/link-capacity      # outside the repository; .gitignore doesn't cover venvs
& $env:USERPROFILE/.venvs/link-capacity/Scripts/python -m pip install -r Scripts/capacity_model/requirements-capture.txt
```

pyodbc also needs a SQL Server ODBC driver (17 or 18). Use the venv's `python` for `snapshot.py`.

### Targets

A targets file names one environment's connections. `targets/docker-compose.json` is the local stack.
For another environment, copy it, or override any value on the command line. The flags mirror
`Scripts/clean.py`: `--db-connection-string`, `--<service>-db-name`, `--<service>-connection-string`,
`--mongo-connection-string`, `--kafka-broker`, `--kafka-username`, and so on.

Credentials are never written into a targets file. Reference them as `${VAR}` instead. Values come
from `.env` in the current directory (or `--env-file`) and the environment, with the environment
taking precedence, exactly as `clean.py` resolves them. A reference that can't be resolved stops the
run, naming the variable but never its value.

The optional sections:

| Section | Fields | Gives |
|---|---|---|
| `cosmos` | `resource_id`: the Cosmos DB account's Azure resource ID | RU and HTTP 429 totals for the run, from `az monitor metrics` (needs `az login` with reader access) |
| `replicas` | `provider`: `docker`, `kubectl` (with `namespace`, `context`) or `none` | Running replicas, plus HPA min/max on Kubernetes |
| `loki` | `url`, optionally `org_id`, `bearer_token`, `selector` | Log bytes per service over the run |

### A run

Run these from the repository root:

```powershell
$py = "$env:USERPROFILE/.venvs/link-capacity/Scripts/python"
& $py Scripts/capacity_model/snapshot.py --label before --out before.json
# start the run, then sample every 15 s until it finishes (Ctrl+C also stops the series)
& $py Scripts/capacity_model/snapshot.py --interval 15 --until 2026-11-11T20:00:00Z --out run.jsonl
& $py Scripts/capacity_model/snapshot.py --label after --previous before.json --out after.json
python Scripts/capacity_model/diff_snapshots.py --before before.json --after after.json --series run.jsonl --out-dir capture-out
```

The after snapshot finds the run's reports through `--previous`: they are the `ReportSchedule` rows
created since the before snapshot was taken. `--report-ids FILE` names them explicitly instead, one
per line.

Add these flags as needed:
- `--environment` and `--code-version` record where the run happened and what was deployed.
- `--tooling-check` labels the summary when the run only proves the scripts.

`diff_snapshots.py` writes three files to `--out-dir`:
- **`summary.md`:**
  - run composition, including the reportable share, completion lag, and whether shadow and rubric validation were active
  - each store's multiplier, overall and per cadence, next to the model's current value
  - bytes per row
  - Loki bytes by component
  - Kafka topics with activity that no store models
  - SQL waits and deadlocks, and Cosmos RU and 429s
  - from the series: write rates per service and store, compared with the rates the model requires for the aligned month-end batch, transient-queue depth, Kafka produce rates, consumer-lag peaks, and blocking
- **`comparison.json`:** the same data, machine-readable.
- **`proposed-update.json`:** the multipliers, bytes per row and completion lag the run supports.

### Profiles and the snapshot format

| | Full | Light |
|---|---|---|
| Stores | rows, data and index bytes; rows per run report where the store has an attribution | rows |
| Run | reports, entries by cadence, census patients, facilities, reportable share, completion lag | – |
| Kafka | high watermark, messages held, partitions, replication, `retention.ms`, sampled bytes per message, consumer lag | high watermark, messages held, consumer lag |
| SQL contention | wait stats, a blocking sample, deadlocks since the run started | wait stats, a blocking sample |
| Other | Loki bytes, Cosmos RU and 429s, server editions and tiers, Mongo version, replicas | – |

Every snapshot carries `schema`, `taken_at`, `label`, `environment`, `code_version`, `profile`, `since`
and `unavailable`. `unavailable` lists everything that couldn't be read, with the reason, such as a
missing permission, a table that doesn't exist yet, or no `az` login. A snapshot never fails for one
source.

### What each source can't tell you

- **SQL Server.** Sizes come from `sys.dm_db_partition_stats` and need VIEW DATABASE STATE. Wait
  stats, blocking and deadlocks need VIEW SERVER STATE (VIEW DATABASE STATE on Azure SQL Database,
  where waits are per database).
  - Deadlocks come from the `system_health` session. On Azure SQL Database they need the
    telemetry target in `master`, so expect them to be unavailable there unless the login can read it.
  - Row counts are the engine's own and can trail a busy table by a moment.
- **MongoDB / Cosmos DB.** Counts come from count commands and sizes from the `collStats` command. No
  aggregation pipeline is used, so nothing depends on stages Cosmos restricts. Whether `collStats`
  returns sizes on Cosmos is still to be confirmed (LEGLINK-1369 subtask B). Where it doesn't, sizes
  are recorded as unavailable.
- **Kafka.** Messages produced during a run come from high watermarks, which retention can't reduce.
  Bytes per message come from reading up to 50 of the newest messages per partition with an
  assign-only consumer, which joins no group and commits nothing. `SubmitPayload` and
  `PayloadSubmitted` carry per-patient and per-report messages that offsets can't separate.
- **Work queues** (`fixed_residency`: `PendingReferenceIds`, `patientDispatches`, the QueryDispatch
  Quartz rows, `EventRetries`). Rows drain as the queue works, so a before/after pair can't show
  rows written. The series reports their maximum depth and whether they drained.
- **Loki.** Bytes come from `bytes_over_time` grouped by `component`, over the window from the before
  snapshot to the after snapshot. Logs from anything else running in that window are included.

### PHI and secrets

Snapshots hold counts, sizes, IDs and timings. They contain no SQL text, no message bodies and no
log lines. The exception is deadlock reports: their input buffers can contain literal values, such
as patient identifiers.
- Snapshots keep only the databases and objects involved in each deadlock.
- `--include-deadlock-xml` writes the raw reports to a separate `<out>.deadlocks.json`. Handle that
  file as sensitive.

Connection strings are masked in log output.

## Recording a measurement (LEGLINK-1369)

`diff_snapshots.py --apply` writes the update:

```powershell
python Scripts/capacity_model/diff_snapshots.py --apply capture-out/proposed-update.json `
    --source "CDC DEV, 2026-12-02, LEGLINK-1369, 2 reports / 1,200 patient-report entries" `
    --stores Scripts/capacity_model/stores.json --assumptions Scripts/capacity_model/assumptions.json
```

It refuses to run without `--source` and without explicit `--stores` and `--assumptions` paths.

For each store the run supports, it:
- sets `multiplier` and replaces any `multiplier_param`
- sets `multiplier_status: measured`
- sets a `multiplier_source` that combines your `--source` with the run's size, its reportable
  share, and the rows and units behind the value
- sets `bytes_per_row`
- records `measured_by_cadence`

It also writes the median completion lag of the run's scheduled reports to `assumptions.json`. An
ad hoc report can cover any period, so its lag is ignored.

These are left alone, and the summary gives the reason for each:
- **Stores the run can't support:** work queues, rows shared with another store, stores whose row
  count fell, and stores with no new rows. No new rows usually means the process that writes them
  didn't run (retention, rubric validation, census), not a true zero.
- **Bytes per row for any table under 1,000 rows,** where whole 8 KB pages swamp the average.

Rerun the model, and commit the inputs together with the regenerated tables.

## What the tests pin

`tests/test_model.py` checks:
- the baseline arrival rates (28.8 k reports and 22.5 M patient-report entries a month)
- the aligned boundary batch (9.45 M)
- the held-volume formulas for each anchor
- the slope-and-offset scaling rule
- throughput
- input validation
- that every store has a usable `location`, with no two stores measuring the same table or topic

`tests/test_capture.py` runs each collector against fakes and recorded responses, covering:
- SQL sizes, attribution, run facts, waits, blocking and deadlocks
- Mongo without aggregation
- Kafka watermarks, sampling and lag
- the Loki and `az` parsers
- the targets resolution
- a full and a light `snapshot.take`

`tests/test_diff.py` covers:
- multipliers per grain and cadence
- the reasons given instead of dividing
- the Kafka and Loki arithmetic
- wait deltas
- series rates, queue depth, blocking and lag
- the comparison with required rates
- the proposal and `--apply`

None of the tests need a live system or the capture libraries. Rerun them after any change to the
scripts or to the inputs they read.
