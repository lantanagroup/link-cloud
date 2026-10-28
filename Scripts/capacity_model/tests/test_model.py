"""Tests for the metadata volume model. Run from the repository root:

    python -m unittest discover Scripts/capacity_model/tests
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import model  # noqa: E402


def baseline(**overrides):
    a = model.load_assumptions(model.DEFAULT_ASSUMPTIONS)
    a.update(overrides)
    return a


def store(**fields):
    s = {"id": "test", "service": "Test", "kind": "sql", "store": "t", "grain": "patient_report",
         "multiplier": 1, "multiplier_status": "schema", "multiplier_source": "test", "bytes_per_row": 100,
         "anchor": "report"}
    s.update(fields)
    return s


class ArrivalTests(unittest.TestCase):
    def test_baseline_arrival_rates(self):
        arr = model.derive_arrivals(baseline())
        self.assertAlmostEqual(arr.cadences["daily"].reports_per_month, 27_000)
        self.assertAlmostEqual(arr.cadences["monthly"].reports_per_month, 1_800)
        self.assertAlmostEqual(arr.reports_per_month, 28_800)
        self.assertAlmostEqual(arr.cadences["daily"].entries_per_month, 13_500_000)
        self.assertAlmostEqual(arr.cadences["monthly"].entries_per_month, 9_000_000)
        self.assertAlmostEqual(arr.entries_per_month, 22_500_000)

    def test_aligned_boundary_batch(self):
        arr = model.derive_arrivals(baseline())
        self.assertAlmostEqual(arr.boundary_entries, 9_450_000)
        self.assertAlmostEqual(arr.boundary_reports, 2_700)

    def test_overlap_changes_only_distinct_facilities(self):
        low = model.derive_arrivals(baseline(share_both=0.35))
        high = model.derive_arrivals(baseline(share_both=0.45))
        self.assertAlmostEqual(low.distinct_facilities, 2_000)
        self.assertAlmostEqual(high.distinct_facilities, 1_800)
        self.assertAlmostEqual(low.entries_per_month, high.entries_per_month)
        self.assertAlmostEqual(high.census_patients_per_month, 9_000_000)


class HeldVolumeTests(unittest.TestCase):
    def test_report_anchor_at_one_month_without_lag_or_cleanup(self):
        # daily: 450k/day x (0.5 + 30) days; monthly: 300k/day x (15 + 30) days
        a = baseline(completion_lag_days=0, cleanup_interval_days=0)
        arr = model.derive_arrivals(a)
        self.assertAlmostEqual(model.held_rows(store(), arr, a, 1), 13_725_000 + 13_500_000)

    def test_report_anchor_includes_lag_and_cleanup(self):
        a = baseline(completion_lag_days=1, cleanup_interval_days=1)
        arr = model.derive_arrivals(a)
        # daily 450k x 32 days; monthly 300k x 46.5 days
        self.assertAlmostEqual(model.held_rows(store(), arr, a, 1), 14_400_000 + 13_950_000)

    def test_slope_and_offset_reproduce_held_at_any_window(self):
        a = baseline()
        arr = model.derive_arrivals(a)
        s = store(multiplier=7)
        r, c = model.slope_offset(s, arr, a)
        for w in (1, 3, 36, 84):
            self.assertAlmostEqual(model.held_rows(s, arr, a, w), r * (w + c), places=3)

    def test_naive_scaling_misstates_held_volume(self):
        # Multiplying the one-month figure by W also multiplies the offset, so it overstates;
        # rows-per-month x W drops the offset, so it understates.
        a = baseline()
        arr = model.derive_arrivals(a)
        s = store()
        held_3 = model.held_rows(s, arr, a, 3)
        r, c = model.slope_offset(s, arr, a)
        self.assertLess(held_3, 3 * model.held_rows(s, arr, a, 1))
        self.assertGreater(held_3, 3 * r)
        self.assertAlmostEqual(3 * model.held_rows(s, arr, a, 1) - held_3, 2 * r * c, places=3)

    def test_peak_is_at_least_steady(self):
        a = baseline()
        arr = model.derive_arrivals(a)
        for anchor in ("report", "record_age"):
            s = store(anchor=anchor)
            self.assertGreaterEqual(model.held_rows(s, arr, a, 1, peak=True), model.held_rows(s, arr, a, 1))

    def test_record_age_ignores_report_period(self):
        a = baseline(cleanup_interval_days=0)
        arr = model.derive_arrivals(a)
        s = store(grain="census_patient", anchor="record_age")
        self.assertAlmostEqual(model.held_rows(s, arr, a, 1), arr.census_patients_per_month)

    def test_own_retention_does_not_change_with_window(self):
        a = baseline()
        arr = model.derive_arrivals(a)
        s = store(anchor="own_retention", residency_days_param="kafka_retention_days",
                  copies_param="kafka_replication_factor")
        self.assertAlmostEqual(model.held_rows(s, arr, a, 1), model.held_rows(s, arr, a, 84))
        self.assertAlmostEqual(model.held_rows(s, arr, a, 1), 22_500_000 / 30 * 7)
        self.assertIsNone(model.slope_offset(s, arr, a))
        self.assertAlmostEqual(model.bytes_held(s, 10, a), 10 * 100 * 3)

    def test_hour_residency_parameter(self):
        a = baseline(dispatch_delay_hours=12)
        s = store(anchor="fixed_residency", residency_days_param="dispatch_delay_hours", residency_param_unit="hours")
        self.assertAlmostEqual(model.residency_param_days(s, a), 0.5)

    def test_unbounded_reads_window_as_horizon(self):
        a = baseline()
        arr = model.derive_arrivals(a)
        s = store(grain="report", anchor="unbounded", multiplier_param="retention_participants")
        self.assertAlmostEqual(model.held_rows(s, arr, a, 12), 28_800 * 4 * 12)


class ThroughputTests(unittest.TestCase):
    def test_peak_rate_processes_boundary_batch_in_window(self):
        a = baseline()
        arr = model.derive_arrivals(a)
        steady, peak = model.write_rates(store(), arr, a, 24)
        self.assertAlmostEqual(steady, 22_500_000 / (30 * 86_400))
        self.assertAlmostEqual(peak, 9_450_000 / 86_400)

    def test_unbatched_grain_peaks_at_steady_rate(self):
        a = baseline()
        arr = model.derive_arrivals(a)
        steady, peak = model.write_rates(store(grain="census_patient", anchor="record_age"), arr, a, 2)
        self.assertAlmostEqual(steady, peak)


class InputTests(unittest.TestCase):
    def test_shipped_inputs_validate_and_render(self):
        a = model.load_assumptions(model.DEFAULT_ASSUMPTIONS)
        stores, excluded = model.load_stores(model.DEFAULT_STORES)
        model.validate(a, stores)
        text = model.markdown_report(a, stores, excluded, a["reference_window_months"])
        self.assertIn("## Arrival rates", text)
        self.assertIn("9.45 M", text)
        rows = model.csv_report(a, stores, 1).strip().splitlines()
        self.assertEqual(len(rows), len(stores) + 1)

    def test_impossible_overlap_is_rejected(self):
        with self.assertRaises(ValueError):
            model.validate(baseline(share_both=0.30), [])
        with self.assertRaises(ValueError):
            model.validate(baseline(share_both=0.50), [])

    def test_set_override_parses_json(self):
        a = model.load_assumptions(model.DEFAULT_ASSUMPTIONS, ["share_both=0.4", "projection_windows_months=[1,2]"])
        self.assertEqual(a["share_both"], 0.4)
        self.assertEqual(a["projection_windows_months"], [1, 2])
        with self.assertRaises(ValueError):
            model.load_assumptions(model.DEFAULT_ASSUMPTIONS, ["no_such_key=1"])

    def test_exclude_tag(self):
        all_stores, _ = model.load_stores(model.DEFAULT_STORES)
        trimmed, _ = model.load_stores(model.DEFAULT_STORES, ["shadow"])
        self.assertLess(len(trimmed), len(all_stores))
        self.assertFalse(any("shadow" in s.get("tags", []) for s in trimmed))


if __name__ == "__main__":
    unittest.main()
