# Patient Flow Simulator

`DotNet/PatientFlowSimulator` is a spike: a small console tool that plays a month of synthetic patient
flow for three facilities into a FHIR server, one hour at a time, so a Link environment has a steady
stream of admissions, clinical data, discharges and census changes to report on. The same cohorts
come back every month with new patient names and ids.

Everything that decides who is admitted, when, with what data, lives in one JSON file
(`scenarios/default.json`). Changing the patient mix, moving a measure to another facility or
adding a measure is a change to that file, not to code.

## What it produces

For a month `yyyy-MM` the generator writes `data/yyyy-MM/yyyy-MM-ddTHHZ.json`: one FHIR
`transaction` Bundle per UTC hour that has something to upload, plus `manifest.json`. Every entry is
a `PUT` with a client-assigned id, so uploading a file twice is harmless.

The file for hour `H` holds what happened in `[H, H+1h)`:

- At admission: `Patient`, `Encounter` (`in-progress`), `Condition`s, `MedicationRequest`s.
- During the stay: `Observation`s and completed `MedicationAdministration`s on the template's schedule.
- At discharge: the same `Encounter` again, now `finished` with `period.end` and a discharge disposition.
- The six census `List`s of each facility whose membership changed during the hour, as they stand at
  the end of the hour.

The first hour of a facility's month also carries its `Organization` and `Location`s and a baseline
of every patient carried over from earlier months, so a month can be imported on its own.

### Census lists

Link reads a facility's census as **six** FHIR Lists (`DotNet/Census/.../PatientListsValidator.cs`
rejects anything else): Admit and Discharge, each split into less than 24 hours, 24 to 48 hours and
more than 48 hours ago. The simulator keeps all six current. A patient is on exactly one Admit list
from admission to discharge, then on one Discharge list for 72 hours, then on none. Their ids follow
the Automation convention, e.g. `census-sim-mercy-general-admit-lt24`.

Census admits whoever appears on an Admit list, discharges whoever appears on a Discharge list, and
also discharges anyone it has admitted who is on no list at all. Keeping admitted patients on an
Admit list the whole time is what prevents spurious discharges.

### Determinism and identities

Every random choice is seeded from `scenario seed | facility | cohort | month | index`, so a month
regenerates bit-for-bit and a later month recomputes the earlier months it needs. Patient ids are
`<facility prefix><10 digits>`, names come from fixed pools and change every month.

### Cross-month patients

A cohort's `crossMonthCount` patients are admitted in the last `crossMonth.admitWithinHoursOfMonthEnd`
hours of the month and discharged in the first `dischargeWithinHoursOfMonthStart` hours of the next
one. Long stays (`confineToMonth: false`, used for long-term care) cross naturally. When month `M+1`
is generated, every stay from earlier months that is still in, or on a discharge list, is a
*carry-over*: its discharge and clinical events land in `M+1`'s files and its baseline is re-sent in
`M+1`'s first hour.

A carried-over encounter overlaps both months' measurement periods, so a monthly report contains
more than the cohort's new patients. The manifest reports both numbers (`expectedQualifying` and
`expectedQualifyingWithCarryOvers`).

## Default scenario

| Facility | Measures | Cohort | Template | Count | Cross-month | Qualifies |
|---|---|---|---|---|---|---|
| Mercy General Hospital (`sim-mercy-general`) | ACH Monthly, ACH Daily | acute-inpatients | acute-inpatient | 20 | 3 | ACH Monthly, ACH Daily |
| | | clinic-visits | outpatient-visit | 20 | 0 | none |
| Riverside Medical Center (`sim-riverside-medical`) | Glycemic Control Hypoglycemic | insulin-inpatients | acute-inpatient-insulin | 20 | 3 | Hypoglycemic |
| | | inpatients-without-insulin | acute-inpatient | 7 | 1 | none |
| | | clinic-visits | outpatient-visit | 13 | 0 | none |
| Lakeside Long-Term Care (`sim-lakeside-ltc`) | LTC Monthly | residents | ltc-resident | 20 | natural | LTC Monthly |

100 new patients a month; 20 qualifying per measure. All three facilities keep `America/Chicago`
time; month boundaries are local midnight there.

The measures are split across facilities so that "20 per measure" holds literally. Both hospitals
could report all three ACH measures instead: add the measure to the facility's `measures` and to
the cohorts' `qualifiesFor`, and read the expected counts from the manifest.

### How the templates reach each initial population

Taken from the CQL in `DotNet/Automation/measures/*.json` (decode the base64 `text/cql` content).

- **ACH Monthly / ACH Daily**: an `Encounter` with status in `in-progress, finished, triaged, onleave,
  entered-in-error` whose period overlaps the measurement period, and that is inpatient, emergency or
  observation by class (`IMP`, `ACUTE`, `NONAC`, `SS`, `EMER`, `OBSENC`), by type (Encounter Inpatient /
  ED Visit / Observation Services value sets) or by a location whose type is in "Inpatient, Emergency,
  and Observation Locations" (HSLOC). `acute-inpatient` uses class `IMP`, SNOMED `32485007` and a
  Medical Ward location (HSLOC `1060-3`).
- **Glycemic Control Hypoglycemic**: an inpatient encounter (class in the NHSN inpatient class codes or
  type Encounter Inpatient, or a hospital location) overlapping the period, with a
  `MedicationAdministration` (status `completed`) or `MedicationRequest` whose code is in the
  "Diabetes Medications" value set, timed inside the hospitalization and the period.
  `acute-inpatient-insulin` administers RxNorm `311036` (Humulin R) every 8 hours and `860975`
  (metformin ER) daily. Both codes are inside the first 1000 entries of the bundle's expansion, which
  is truncated (`total` 1033, 1000 `contains`); codes past that cut are not matched by MeasureEval.
- **LTC Monthly** (`NHSNLongTermCareMonthlyInitialPopulation` 2.0.1-cibuild): every `Encounter` whose
  period overlaps the measurement period, with no class, type, status or location condition. That is
  why Lakeside only gets residents: any other encounter there would qualify too. `ltc-resident` uses
  class `NONAC` and a Long Term Care Facility General Nursing Unit (HSLOC `1258-3`) for realism.
- **Not qualifying**: `outpatient-visit` is class `AMB`, SNOMED `185349003`, no location, one to three
  hours long. It still goes through the census lists like everyone else.

## Running it

```powershell
cd DotNet/PatientFlowSimulator
dotnet run -- plan --month 2026-11                                   # roster and expected counts, writes nothing
dotnet run -- generate --month 2026-11 --data ./data                 # write the month (add --force to replace it)
dotnet run -- import --fhir http://localhost:6157/fhir --data ./data # one scheduled run
dotnet run -- run --fhir http://localhost:6157/fhir --interval 60    # keep running (docker-compose style)
```

`import` is the hourly job. It makes sure the current month (per facility time zone) has been
generated, then uploads every file whose hour has fully elapsed and that has no `.done` marker yet,
oldest first. The file for `14:00Z` is therefore uploaded by the first run after `15:00Z`. A failed
upload stops the run; nothing after it is marked done, and the next run retries from there. After an
outage the backlog is uploaded in order. `--now <iso>` pretends it is another time, `--dry-run` lists
what would be uploaded.

Every option has an environment variable (`SIM_SCENARIO`, `SIM_DATA_DIR`, `SIM_FHIR_BASE_URL`,
`SIM_FHIR_BEARER_TOKEN`, `SIM_FHIR_BASIC_AUTH`, `SIM_INTERVAL_MINUTES`); `--help` lists them.

### Local stack

```powershell
docker compose --profile simulator up -d patient-flow-simulator
```

The service runs `run` against `http://fhir-server:8080/fhir` with its data on the `simulator_data`
volume. It is behind a compose profile so the default stack is unchanged.

### Kubernetes

`deploy/cronjob.yaml` is a sample hourly `CronJob` with a persistent volume for the data directory and
a Secret for the FHIR credentials: the "lambda" shape. The real Link manifests live in `link-cac`;
the sample is a starting point, not a deployment.

### Data directory

The data directory is state: the generated files and the `.done` markers are how a run knows what
earlier runs uploaded. Keep it on a volume. The default `./data` is git-ignored.

## Setting up the facilities in Link

The simulator writes only to the FHIR server. Each facility has to exist in Link with the FHIR
server as its EHR. `Scripts/setup-patient-flow-facilities.ps1` does all of it through the Admin BFF,
reading the facilities and measures from the scenario file so the two cannot drift apart:

```powershell
./Scripts/setup-patient-flow-facilities.ps1 -LoadMeasures                       # local stack
./Scripts/setup-patient-flow-facilities.ps1 -AdminBffUrl https://<bff> -AccessToken $token `
    -FhirServerBaseUrl https://<fhir>/fhir -LoadMeasures                        # a deployed environment
./Scripts/setup-patient-flow-facilities.ps1 -WhatIf                             # show what would change
./Scripts/setup-patient-flow-facilities.ps1 -Remove                             # tear it all down
```

Existing items are left alone (`-UpdateExisting` changes them to match). It handles the three
Tenant/DMRP modes the same way `FacilitySetupHelper` does. The FHIR base URL is the one the Link
services use, so inside docker-compose it is `http://fhir-server:8080/fhir`, not `localhost:6157`.

The calls it makes, for reference (payloads as `Automation.Link/Helpers/FacilitySetupHelper.cs` and
`Scripts/seed.py` send them):

1. **Measure definitions** in MeasureEval: `PUT /api/measureeval/measure-definition` with each bundle
   from `DotNet/Automation/measures/`.
2. **Facility** (Tenant): `POST /api/Facility`
   ```json
   {
     "FacilityId": "sim-mercy-general",
     "FacilityName": "Mercy General Hospital",
     "TimeZone": "America/Chicago",
     "ScheduledReports": {
       "Monthly": ["NHSNAcuteCareHospitalMonthlyInitialPopulation"],
       "Daily": ["NHSNAcuteCareHospitalDailyInitialPopulation"],
       "Weekly": []
     }
   }
   ```
   Riverside: `Monthly: ["NHSNGlycemicControlHypoglycemicInitialPopulation"]`.
   Lakeside: `Monthly: ["NHSNLongTermCareMonthlyInitialPopulation"]`.
   With DMRP enabled the schedule is derived from reporting plans instead; `FacilitySetupHelper`
   documents both paths.
3. **Census** schedule: `POST /api/census/config`
   `{ "FacilityId": "sim-mercy-general", "ScheduledTrigger": "0 0/15 * * * ?", "Enabled": true }`
   Any cadence works; the lists change at most once an hour.
4. **FHIR query configuration** (DataAcquisition): `POST /api/data-acquisition/fhirQueryConfiguration`
   `{ "FacilityId": "...", "FhirServerBaseUrl": "<fhir base>", "Authentication": null, "QueryPlanIds": [...], "MaxConcurrentRequests": 8, "TimeZone": "UTC" }`
5. **FHIR list configuration**: `POST /api/data-acquisition/fhirQueryList`
   ```json
   {
     "FacilityId": "sim-mercy-general",
     "FhirBaseServerUrl": "<fhir base>",
     "EHRPatientLists": [
       { "Status": "Admit",     "TimeFrame": "LessThan24Hours",    "FhirId": "census-sim-mercy-general-admit-lt24" },
       { "Status": "Admit",     "TimeFrame": "Between24To48Hours", "FhirId": "census-sim-mercy-general-admit-24to48" },
       { "Status": "Admit",     "TimeFrame": "MoreThan48Hours",    "FhirId": "census-sim-mercy-general-admit-gt48" },
       { "Status": "Discharge", "TimeFrame": "LessThan24Hours",    "FhirId": "census-sim-mercy-general-discharge-lt24" },
       { "Status": "Discharge", "TimeFrame": "Between24To48Hours", "FhirId": "census-sim-mercy-general-discharge-24to48" },
       { "Status": "Discharge", "TimeFrame": "MoreThan48Hours",    "FhirId": "census-sim-mercy-general-discharge-gt48" }
     ]
   }
   ```
   The six ids are in each month's `manifest.json` under `censusListIds`.
6. **Query plans**: `POST /api/data-acquisition/{facilityId}/QueryPlan` for the `Discharge`, `Daily`
   and `Monthly` types (`Scripts/seed_data/query_plan.json` is a working Epic-style plan; the
   resource types the templates produce are Encounter, Condition, Observation, MedicationRequest,
   MedicationAdministration and Location).
7. **QueryDispatch** discharge delay, e.g. `PT0S` or `PT2H` (`EnsureQueryDispatchConfigAsync`).

## Checking the result

`data/yyyy-MM/manifest.json` lists every patient with admit and discharge times (UTC and local),
their cohort, whether they cross the month boundary, and per facility the expected qualifying count
per measure, with and without carry-overs. Compare it with the report Link produces for the month;
the ids are the FHIR `Patient` ids.

## Changing the simulation

- Counts, cross-month counts and which facility reports what: edit `facilities[]` in the scenario.
- A new kind of patient: add a template under `templates` and a cohort that uses it. A template is an
  encounter class, optional type and HSLOC location, a length-of-stay range, an age range, a pool of
  conditions, scheduled observations and scheduled medications.
- A new measure: a template that satisfies its initial population plus a cohort with it in
  `qualifiesFor`, and the measure in the facility's `measures`.
- Regenerating: `generate --force` replaces a month's files and markers. Do it only for months that
  have not been uploaded yet, and change the scenario at a month boundary: a different scenario
  gives the month different patients, and files already uploaded cannot be taken back.

## Open points

- The QA FHIR server base URL and credentials are not in this repository. `--fhir` plus `--bearer`
  or `--basic` cover a static token or basic auth; OAuth client credentials would be a small addition
  to `Import/FhirImportClient.cs`.
- Nothing is ever deleted from the FHIR server. Every simulator resource carries an identifier in the
  `urn:link:patient-flow-simulator:*` namespace, which a cleanup could key on.
- The measure bundle for LTC carries no value sets and the Hypoglycemic bundle's Diabetes Medications
  expansion is truncated at 1000 codes; both are properties of the bundles, not of the simulator.
- Verified locally against HAPI 8.10 with referential integrity enabled (the compose server has it
  off). Not yet run against the QA environment.
