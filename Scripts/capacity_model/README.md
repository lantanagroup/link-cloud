# Metadata volume model (LEGPROG-516)

Projects how much non-clinical operational data and metadata Link holds, and how fast it's
written, at the LEGPROG-516 target scale. LEGLINK-1364 built it. LEGLINK-1369 and the
service-specific enablers (LEGLINK-1365, 1366, 1370–1374) replace its placeholder multipliers
with measured ones.

The method, the assumptions behind each input and the divergences from ADR 08 and LEGLINK-1242
are documented in the model document, "Metadata Volume Model: Assumptions and Method". This file
covers how to run and update the model.

Run from the repository root. Python 3.11+ and the standard library only.

```powershell
python Scripts/capacity_model/model.py                                    # Markdown report to stdout
python Scripts/capacity_model/model.py --out sizing.md
python Scripts/capacity_model/model.py --format csv --out sizing.csv     # one row per store
python Scripts/capacity_model/model.py --window-months 3                 # per-store table at 3 months
python Scripts/capacity_model/model.py --set share_both=0.45 --set completion_lag_days=3
python Scripts/capacity_model/model.py --exclude-tag shadow --exclude-tag rubric
python -m unittest discover Scripts/capacity_model/tests
```

Nothing here connects to an environment or needs credentials.

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

## Recording a measurement (LEGLINK-1369)

1. Divide the measured row count by the number of grain units in the run. For example, rows
   added to `ReportResource` divided by the patient-report entries in the run.
2. Set `multiplier`, `multiplier_status: "measured"` and `multiplier_source`, the last naming the
   environment, date, ticket and run size. Set `bytes_per_row` from the measured data and index
   sizes.
3. Rerun the model and commit the inputs together with the regenerated tables in the model
   document.

## What the tests pin

`tests/test_model.py` checks:
- the baseline arrival rates (28.8 k reports and 22.5 M patient-report entries a month)
- the aligned boundary batch (9.45 M)
- the held-volume formulas for each anchor
- the slope-and-offset scaling rule
- throughput
- input validation

Rerun the tests after any change to `model.py` or to the assumptions they read.
