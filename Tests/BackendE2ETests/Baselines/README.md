# Baselines

This directory stores committed baseline files used by `ValidationBaselineManager` in `BackendE2ETests`.

Expected baseline files:

- `SmokeTest.baseline.json`
- `MegaPatientTest.baseline.json`
- `MultiPatientTest.baseline.json`

## Behavior

- If a baseline file exists, tests compare current run output against it.
- If a baseline file does **not** exist, tests create it automatically.
- If `E2E_BASELINE_REGENERATE=true`, tests overwrite/regenerate the baseline.

## Optional path override

You can override the baseline directory with:

- `E2E_BASELINE_DIR`

When set, baseline files are read/written from that directory instead of this folder.
