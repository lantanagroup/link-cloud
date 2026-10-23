# Patient Flow Simulator

Generates a month of synthetic patient flow for three facilities (admissions, clinical data,
discharges and the six census lists Link reads) as hourly FHIR transaction bundles, and uploads
each hour's bundle to a FHIR server once that hour has elapsed. The same cohorts come back every
month with new names and ids.

```powershell
dotnet run --project DotNet/PatientFlowSimulator -- plan --month 2026-11              # roster + expected counts
dotnet run --project DotNet/PatientFlowSimulator -- generate --month 2026-11 --data ./data
dotnet run --project DotNet/PatientFlowSimulator -- report --month 2026-11 --data ./data   # Markdown + Mermaid, one chart per patient
dotnet run --project DotNet/PatientFlowSimulator -- report --month 2026-11 --data ./data --format html   # same, as a page that renders the charts
dotnet run --project DotNet/PatientFlowSimulator -- import --fhir http://localhost:6157/fhir --data ./data
dotnet run --project DotNet/PatientFlowSimulator -- run --fhir http://localhost:6157/fhir --interval 60
```

`scenarios/default.json` is the whole configuration: facilities, cohorts, counts, measures and the
clinical templates. Changing the patient mix or adding a measure is a change to that file, not to code.

Design, Link facility setup and deployment notes: [dev-docs/patient-flow-simulator.md](../../dev-docs/patient-flow-simulator.md).
