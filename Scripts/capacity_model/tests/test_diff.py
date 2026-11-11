"""Tests for diff_snapshots.py, using snapshots built in code. Run from the repository root:

    python -m unittest discover Scripts/capacity_model/tests
"""

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

import diff_snapshots as diff  # noqa: E402

D1 = "11111111-1111-1111-1111-111111111111"   # daily report
M1 = "22222222-2222-2222-2222-222222222222"   # monthly report
OLD = "33333333-3333-3333-3333-333333333333"  # a report from before the run


def store(sid, kind="sql", grain="patient_report", anchor="report", **extra):
    s = {"id": sid, "service": extra.pop("service", "Report"), "kind": kind, "store": sid, "grain": grain,
         "multiplier": 1, "multiplier_status": "placeholder", "multiplier_source": "test", "bytes_per_row": 100,
         "anchor": anchor, "location": extra.pop("location", {"service": "report", "table": "dbo.T"})}
    s.update(extra)
    if "multiplier_param" in s:  # stores.json carries one or the other
        del s["multiplier"]
    return s


STORES = [
    store("report.ReportEntry"),
    store("report.ReportResource", multiplier=300),
    store("report.RetentionCleanupResult", grain="report", anchor="unbounded",
          multiplier_param="retention_participants"),
    store("da.PendingReferenceId", anchor="fixed_residency", service="DataAcquisition"),
    store("census.PatientEncounter", grain="census_patient", anchor="record_age", service="Census"),
    store("audit.pipeline", anchor="record_age", service="Audit"),
    store("audit.retention", grain="report", anchor="record_age", service="Audit",
          location={"measured_with": "audit.pipeline"}),
    store("validation.legacy_shadow_result", service="Validation", tags=["shadow"]),
    store("da.SftpAcquisitionLog", grain="facility_day", anchor="record_age", service="DataAcquisition"),
    store("kafka.DataAcquisitionRequested", kind="kafka", anchor="own_retention", service="Kafka",
          location={"topics": ["DataAcquisitionRequested", "DataAcquisitionRequested-Retry"]}),
    store("loki.all_services", kind="loki", anchor="own_retention", service="Loki",
          location={"selector": '{app="link-cloud"}', "group_by": "component"}),
]


def size(rows, data=None, index=None, by_report=None):
    s = {"rows": rows, "data_bytes": data if data is not None else rows * 80,
         "index_bytes": index if index is not None else rows * 20}
    if by_report is not None:
        s["by_report"] = by_report
    return s


def before():
    return {"schema": 1, "taken_at": "2026-11-11T17:00:00Z", "environment": "test", "profile": "full",
            "stores": {"report.ReportEntry": size(100), "report.ReportResource": size(1000),
                       "report.RetentionCleanupResult": size(0), "da.PendingReferenceId": size(0),
                       "census.PatientEncounter": size(50), "audit.pipeline": size(10),
                       "validation.legacy_shadow_result": size(0), "da.SftpAcquisitionLog": size(0)},
            "kafka": {"DataAcquisitionRequested": {"high_watermark": 500, "partitions": 3, "replication": 1},
                      "DataAcquisitionRequested-Retry": {"high_watermark": 10, "partitions": 3, "replication": 1},
                      "GenerateReportRequested": {"high_watermark": 1, "partitions": 3, "replication": 1}},
            "contention": {"sql": {"localhost,1433": {"services": ["report"], "waits": {
                "LCK_M_X": {"tasks": 1, "wait_ms": 100, "signal_ms": 0},
                "SLEEP_TASK": {"tasks": 5, "wait_ms": 9000, "signal_ms": 0}}}}},
            "unavailable": [{"source": "cloud", "item": "Cosmos RU and 429 metrics", "reason": "none configured"}]}


def after():
    run = {"since": "2026-11-11T17:00:00Z",
           "reports": [{"id": D1, "cadence": "daily", "completion_lag_days": 0.5},
                       {"id": M1, "cadence": "monthly", "completion_lag_days": 1.5},
                       {"id": "44444444-4444-4444-4444-444444444444", "cadence": "other",
                        "completion_lag_days": 1046.0}],  # ad hoc report over a historical period
           "by_cadence": {"daily": {"reports": 1, "entries": 20}, "monthly": {"reports": 1, "entries": 30},
                          "other": {"reports": 1, "entries": 0}},
           "census": {"encounters": 30, "patients": 25}, "facilities": 2,
           "reportable": {"evaluated": 50, "reportable": 20, "share": 0.4}, "completion_lag_days": [0.5, 1.5]}
    return {"schema": 1, "taken_at": "2026-11-11T19:00:00Z", "environment": "test", "profile": "full",
            "code_version": "0.9.0", "run": run,
            "stores": {"report.ReportEntry": size(150, by_report={D1: 20, M1: 30}),
                       "report.ReportResource": size(16_000, by_report={D1: 4000, M1: 9000, OLD: 5}),
                       "report.RetentionCleanupResult": size(12, by_report={}),
                       "da.PendingReferenceId": size(0),
                       "census.PatientEncounter": size(80), "audit.pipeline": size(110),
                       "validation.legacy_shadow_result": size(50), "da.SftpAcquisitionLog": size(0)},
            "kafka": {"DataAcquisitionRequested": {"high_watermark": 580, "partitions": 3, "replication": 1,
                                                   "retention_ms": 604800000, "sampled": 30, "avg_message_bytes": 900},
                      "DataAcquisitionRequested-Retry": {"high_watermark": 30, "partitions": 3, "replication": 1,
                                                         "retention_ms": 604800000, "sampled": 10,
                                                         "avg_message_bytes": 1300},
                      "GenerateReportRequested": {"high_watermark": 4, "partitions": 3, "replication": 1}},
            "loki": {"loki.all_services": {"window_s": 7200, "bytes_by_component": {"Report": 500_000,
                                                                                   "DataAcquisition": 1_500_000}}},
            "contention": {"sql": {"localhost,1433": {"services": ["report"], "waits": {
                "LCK_M_X": {"tasks": 4, "wait_ms": 900, "signal_ms": 5},
                "SLEEP_TASK": {"tasks": 50, "wait_ms": 90000, "signal_ms": 0},
                "PAGEIOLATCH_SH": {"tasks": 2, "wait_ms": 300, "signal_ms": 1}},
                "deadlocks": {"count": 1, "events": []}}}, "cosmos": {}},
            "unavailable": [{"source": "cloud", "item": "Cosmos RU and 429 metrics", "reason": "none configured"}]}


def by_id(results):
    return {r["id"]: r for r in results}


class PairTests(unittest.TestCase):
    def setUp(self):
        self.pair = diff.diff_pair(STORES, before(), after())
        self.r = by_id(self.pair["stores"])

    def test_multiplier_and_per_cadence(self):
        rr = self.r["report.ReportResource"]
        self.assertEqual(rr["delta_rows"], 15_000)
        self.assertEqual(rr["units"], 50)
        self.assertEqual(rr["multiplier"], 300)
        self.assertEqual(rr["per_cadence"]["daily"]["multiplier"], 200)
        self.assertEqual(rr["per_cadence"]["monthly"]["multiplier"], 300)
        self.assertAlmostEqual(rr["coverage"], 13_000 / 15_000)
        self.assertEqual(rr["bytes_per_row"], 100)

    def test_grain_units_by_kind(self):
        self.assertEqual(self.r["census.PatientEncounter"]["multiplier"], 30 / 25)
        self.assertEqual(self.r["audit.pipeline"]["multiplier"], 100 / 50)
        self.assertEqual(self.r["report.RetentionCleanupResult"]["multiplier"], 12 / 3)

    def test_reasons_instead_of_division(self):
        self.assertIn("measured with audit.pipeline", self.r["audit.retention"]["reason"])
        self.assertIn("work queue", self.r["da.PendingReferenceId"]["reason"])
        self.assertIsNone(self.r["da.PendingReferenceId"].get("multiplier"))
        self.assertEqual(self.r["da.SftpAcquisitionLog"]["multiplier"], 0)  # 2 facilities x 1/12 day, no rows

        a = after()
        a["run"]["by_cadence"] = {}
        no_entries = by_id(diff.diff_pair(STORES, before(), a)["stores"])
        self.assertIn("no patient-report entries", no_entries["report.ReportEntry"]["reason"])

        missing = after()
        del missing["stores"]["report.ReportEntry"]
        self.assertEqual(by_id(diff.diff_pair(STORES, before(), missing)["stores"])["report.ReportEntry"]["reason"],
                         "missing from after")

    def test_negative_delta_is_flagged(self):
        a = after()
        a["stores"]["audit.pipeline"] = size(5)
        r = by_id(diff.diff_pair(STORES, before(), a)["stores"])["audit.pipeline"]
        self.assertIn("rows fell", r["note"])
        self.assertNotIn("audit.pipeline", diff.proposal(diff.diff_pair(STORES, before(), a))["stores"])

    def test_kafka_and_loki(self):
        k = self.r["kafka.DataAcquisitionRequested"]
        self.assertEqual(k["delta_rows"], 100)
        self.assertEqual(k["multiplier"], 2)
        self.assertAlmostEqual(k["bytes_per_row"], (900 * 30 + 1300 * 10) / 40)
        self.assertEqual(k["retention_ms"], [604800000])
        self.assertEqual(self.pair["unmodeled_topics"], {"GenerateReportRequested": 3})
        lk = self.r["loki.all_services"]
        self.assertEqual(lk["multiplier"], 2_000_000 / 50)
        self.assertEqual(lk["by_component"]["DataAcquisition"], 1_500_000)

    def test_run_summary_and_enablement(self):
        run = self.pair["run"]
        self.assertEqual(run["reports"], 3)
        self.assertEqual(run["entries"], 50)
        self.assertEqual(run["reportable_share"], 0.4)
        self.assertEqual(run["completion_lag_days_median"], 1.0)  # the ad hoc report's 1,046 days is ignored
        self.assertEqual(run["terminal_reports"], 2)
        self.assertEqual(run["enabled"], {"shadow": True, "rubric": False})

    def test_bytes_per_row_needs_enough_rows(self):
        self.assertIsNone(self.r["report.ReportEntry"]["bytes_per_row"])  # 150 rows: page-granular noise
        self.assertEqual(self.r["report.ReportResource"]["bytes_per_row"], 100)
        self.assertNotIn("bytes_per_row", diff.proposal(self.pair)["stores"]["report.ReportEntry"])

    def test_zero_delta_is_reported_not_proposed(self):
        self.assertEqual(self.r["da.SftpAcquisitionLog"]["note"], "no rows written in the run; not proposed")
        self.assertNotIn("da.SftpAcquisitionLog", diff.proposal(self.pair)["stores"])

    def test_wait_deltas_drop_benign_waits(self):
        waits = self.pair["contention"]["sql"]["localhost,1433"]["top_waits"]
        self.assertEqual([w["wait_type"] for w in waits], ["LCK_M_X", "PAGEIOLATCH_SH"])
        self.assertEqual(waits[0], {"wait_type": "LCK_M_X", "tasks": 3, "wait_ms": 800, "signal_ms": 5})
        self.assertEqual(self.pair["contention"]["sql"]["localhost,1433"]["deadlocks"]["count"], 1)

    def test_proposal(self):
        p = diff.proposal(self.pair)
        self.assertEqual(p["stores"]["report.ReportResource"],
                         {"multiplier": 300.0, "delta_rows": 15000, "units": 50, "grain": "patient_report",
                          "bytes_per_row": 100, "by_cadence": {"daily": 200.0, "monthly": 300.0}})
        self.assertNotIn("da.PendingReferenceId", p["stores"])
        self.assertNotIn("audit.retention", p["stores"])
        self.assertEqual(p["assumptions"], {"completion_lag_days": 1.0})
        self.assertNotIn("bytes_per_row", p["stores"]["loki.all_services"])

    def test_summary_banner_and_sections(self):
        text = diff.summary(self.pair, None, tooling_check=True)
        self.assertIn("Tooling check, not a measurement", text)
        self.assertIn("## Store multipliers", text)
        self.assertIn("Scheduled reports terminal", text)  # ad hoc reports don't count toward the lag
        self.assertIn("**300**", text)
        self.assertIn("GenerateReportRequested", text)
        self.assertIn("## Unavailable", text)
        self.assertEqual(text.count("Cosmos RU and 429 metrics"), 1)  # de-duplicated across snapshots
        self.assertNotIn("Tooling check", diff.summary(self.pair, None, tooling_check=False))
        self.assertIn("= retention_participants → **4**", text)  # a parameter-driven model multiplier


def sample(t, pending, entries, high, lag, blocked):
    return {"taken_at": t, "profile": "light",
            "stores": {"report.ReportEntry": {"rows": entries}, "report.ReportResource": {"rows": entries * 300},
                       "da.PendingReferenceId": {"rows": pending}},
            "kafka": {"DataAcquisitionRequested": {"high_watermark": high},
                      "DataAcquisitionRequested-Retry": {"high_watermark": 0}},
            "consumer_lag": {"dataacq": {"DataAcquisitionRequested": lag}},
            "contention": {"sql": {"s": {"blocking": {"blocked": blocked, "max_wait_ms": blocked * 100,
                                                      "by_wait_type": {"LCK_M_U": blocked} if blocked else {},
                                                      "by_database": {}},
                                         "waits": {"LCK_M_U": {"tasks": blocked, "wait_ms": blocked * 100,
                                                               "signal_ms": 0}}}}}}


SERIES = [sample("2026-11-11T17:00:00Z", 0, 100, 0, 0, 0),
          sample("2026-11-11T17:00:10Z", 40, 150, 20, 15, 2),
          sample("2026-11-11T17:00:20Z", 60, 160, 25, 4, 0),
          sample("2026-11-11T17:00:30Z", 0, 200, 30, 0, 1)]


class SeriesTests(unittest.TestCase):
    def setUp(self):
        self.s = diff.diff_series(STORES, SERIES, diff.required_rates(diff.DEFAULT_STORES, diff.DEFAULT_ASSUMPTIONS))

    def test_write_rates(self):
        entry = self.s["stores"]["report.ReportEntry"]
        self.assertEqual(entry["peak"], 5.0)        # 50 rows in 10 s
        self.assertAlmostEqual(entry["mean"], 100 / 30)
        self.assertEqual(self.s["services"]["Report"]["peak"], 5.0 * 301)
        self.assertEqual(self.s["topics"]["kafka.DataAcquisitionRequested"]["peak"], 2.0)

    def test_transient_depth_lag_and_blocking(self):
        self.assertEqual(self.s["transient_depth"]["da.PendingReferenceId"],
                         {"first": 0, "max": 60, "last": 0, "drained": True})
        self.assertEqual(self.s["consumer_lag"]["dataacq"]["DataAcquisitionRequested"], {"max": 15, "last": 0})
        b = self.s["blocking"]["s"]
        self.assertEqual((b["samples"], b["samples_blocked"], b["max_blocked"], b["max_wait_ms"]), (4, 2, 2, 200))
        self.assertEqual(b["by_wait_type"], {"LCK_M_U": 3})
        self.assertEqual(self.s["waits"]["s"][0]["wait_ms"], 100)

    def test_required_rates_come_from_the_model(self):
        req = self.s["required"]
        self.assertEqual(req["windows_hours"], [2, 6, 24])
        ids = req["stores"]["da.DataAcquisitionLogResourceId"]
        self.assertGreater(ids[0], ids[2])  # a 2 h window needs more than 24 h
        self.assertEqual(req["stores"]["report.ReportResource"], [0, 0, 0])  # not written since LNK-5134
        text = diff.summary(None, self.s, tooling_check=False)
        self.assertIn("## Transient queues", text)
        self.assertIn("Required, 24 h", text)


class ApplyTests(unittest.TestCase):
    def test_apply_writes_measured_values(self):
        pair = diff.diff_pair(STORES, before(), after())
        with tempfile.TemporaryDirectory() as d:
            stores_path, assumptions_path = Path(d) / "stores.json", Path(d) / "assumptions.json"
            stores_path.write_text(json.dumps({"stores": STORES}), encoding="utf-8")
            shutil.copy(diff.DEFAULT_ASSUMPTIONS, assumptions_path)
            changed = diff.apply(diff.proposal(pair), "local-compose 2026-11-11 LEGLINK-1369 tooling check",
                                 stores_path, assumptions_path)
            written = {s["id"]: s for s in json.loads(stores_path.read_text(encoding="utf-8"))["stores"]}
            assumptions = json.loads(assumptions_path.read_text(encoding="utf-8"))
        self.assertIn("report.ReportResource", changed)
        rr = written["report.ReportResource"]
        self.assertEqual((rr["multiplier"], rr["multiplier_status"], rr["bytes_per_row"]), (300.0, "measured", 100))
        self.assertIn("LEGLINK-1369", rr["multiplier_source"])
        self.assertIn("reportable share 0.40", rr["multiplier_source"])
        self.assertEqual(rr["measured_by_cadence"], {"daily": 200.0, "monthly": 300.0})
        cleanup = written["report.RetentionCleanupResult"]
        self.assertNotIn("multiplier_param", cleanup)
        self.assertEqual(cleanup["multiplier"], 4.0)
        self.assertEqual(written["da.PendingReferenceId"]["multiplier_status"], "placeholder")
        self.assertEqual(assumptions["completion_lag_days"]["value"], 1.0)
        self.assertIn("measured:", assumptions["completion_lag_days"]["source"])

    def test_apply_refuses_without_source_or_paths(self):
        with tempfile.TemporaryDirectory() as d:
            proposal = Path(d) / "p.json"
            proposal.write_text("{}", encoding="utf-8")
            self.assertEqual(diff.main(["--apply", str(proposal), "--stores", "s", "--assumptions", "a"]), 2)
            self.assertEqual(diff.main(["--apply", str(proposal), "--source", "  "]), 2)
            self.assertEqual(diff.main(["--apply", str(proposal), "--source", "x"]), 2)

    def test_cli_writes_outputs(self):
        with tempfile.TemporaryDirectory() as d:
            d = Path(d)
            stores_path = d / "stores.json"
            stores_path.write_text(json.dumps({"stores": STORES}), encoding="utf-8")
            for name, snap in (("before.json", before()), ("after.json", after())):
                (d / name).write_text(json.dumps(snap), encoding="utf-8")
            (d / "run.jsonl").write_text("\n".join(json.dumps(s) for s in SERIES), encoding="utf-8")
            code = diff.main(["--before", str(d / "before.json"), "--after", str(d / "after.json"),
                              "--series", str(d / "run.jsonl"), "--out-dir", str(d / "out"),
                              "--stores", str(stores_path), "--assumptions", str(diff.DEFAULT_ASSUMPTIONS),
                              "--tooling-check"])
            self.assertEqual(code, 0)
            self.assertTrue((d / "out" / "summary.md").exists())
            self.assertIn("report.ReportResource",
                          json.loads((d / "out" / "proposed-update.json").read_text(encoding="utf-8"))["stores"])
            self.assertIn("series", json.loads((d / "out" / "comparison.json").read_text(encoding="utf-8")))


if __name__ == "__main__":
    unittest.main()
