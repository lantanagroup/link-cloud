"""Turn snapshots into model inputs: multipliers, write rates and contention, plus a proposed stores.json update.

Pair mode (--before/--after) gives each store's rows per grain unit, overall and per cadence, its bytes per row,
the run's composition and the SQL and Cosmos contention between the two snapshots. Series mode (--series) adds
achieved write rates per store and service, transient-queue depth, blocking, consumer lag, and a comparison with
the rates the model requires for the aligned month-end batch. Nothing is written to the model unless --apply is
given, with --source and explicit --stores and --assumptions paths.

    python Scripts/capacity_model/diff_snapshots.py --before before.json --after after.json --series run.jsonl --out-dir out
    python Scripts/capacity_model/diff_snapshots.py --apply out/proposed-update.json --source "..." \
        --stores Scripts/capacity_model/stores.json --assumptions Scripts/capacity_model/assumptions.json
"""

from __future__ import annotations

import argparse
import statistics
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import model  # noqa: E402
from capture.common import iso, parse_iso, read_json, read_series, utc_now, write_json  # noqa: E402

DEFAULT_STORES = HERE / "stores.json"
DEFAULT_ASSUMPTIONS = HERE / "assumptions.json"
GRAIN_LABEL = {"patient_report": "patient-report entries", "report": "reports",
               "census_patient": "census patients", "facility_day": "facility-days"}
# Waits that accumulate whether or not anything is contended; excluded from the top-waits tables.
BENIGN_WAITS = ("SLEEP_", "BROKER_", "XE_", "SQLTRACE_", "LAZYWRITER_SLEEP", "CHECKPOINT_QUEUE", "LOGMGR_QUEUE",
                "REQUEST_FOR_DEADLOCK_SEARCH", "DIRTY_PAGE_POLL", "HADR_FILESTREAM_IOMGR_IOCOMPLETION",
                "SOS_WORK_DISPATCHER", "QDS_", "WAIT_XTP_", "SP_SERVER_DIAGNOSTICS_SLEEP", "FT_IFTS",
                "CLR_AUTO_EVENT", "CLR_MANUAL_EVENT", "ONDEMAND_TASK_QUEUE", "DISPATCHER_QUEUE_SEMAPHORE",
                "WAITFOR", "PWAIT_EXTENSIBILITY_CLEANUP_TASK", "PREEMPTIVE_XE_", "PARALLEL_REDO_",
                "PVS_PREALLOCATE", "UCS_SESSION_REGISTRATION", "RESOURCE_QUEUE")
TOP_WAITS = 10
# Below this many rows, page-granular sizes make the table-wide average meaningless (one row in an 8 KB page).
MIN_ROWS_FOR_BYTES = 1000
SCHEDULED_CADENCES = ("daily", "monthly")


def sig(x: float | None, digits: int = 4) -> float | None:
    return None if x is None else float(f"{x:.{digits}g}")


# --------------------------------------------------------------------------------------------
# Run composition and grain units
# --------------------------------------------------------------------------------------------

def grain_units(run: dict, grain: str, elapsed_days: float) -> dict[str, float | None]:
    """{'all': units, 'daily': ..., 'monthly': ...} for one grain. Cadence splits only where they apply."""
    by_cadence = run.get("by_cadence", {})
    if grain == "patient_report":
        units = {c: v["entries"] for c, v in by_cadence.items()}
        return {"all": sum(units.values()), **units}
    if grain == "report":
        units = {c: v["reports"] for c, v in by_cadence.items()}
        return {"all": sum(units.values()), **units}
    if grain == "census_patient":
        return {"all": (run.get("census") or {}).get("patients")}
    if grain == "facility_day":
        f = run.get("facilities")
        return {"all": f * elapsed_days if f is not None else None}
    return {"all": None}


def run_summary(run: dict) -> dict:
    # Only scheduled reports say anything about the model's completion lag; an ad hoc report can cover any period.
    lags = [r["completion_lag_days"] for r in run.get("reports", [])
            if r.get("cadence") in SCHEDULED_CADENCES and r.get("completion_lag_days") is not None]
    reportable = run.get("reportable") or {}
    return {
        "reports": len(run.get("reports", [])),
        "by_cadence": run.get("by_cadence", {}),
        "entries": sum(v["entries"] for v in run.get("by_cadence", {}).values()),
        "census": run.get("census"),
        "facilities": run.get("facilities"),
        "reportable_share": reportable.get("share"),
        "reportable_evaluated": reportable.get("evaluated"),
        "terminal_reports": len(lags),
        "completion_lag_days_median": statistics.median(lags) if lags else None,
        "completion_lag_days_max": max(lags) if lags else None,
    }


# --------------------------------------------------------------------------------------------
# Pair mode
# --------------------------------------------------------------------------------------------

def store_bytes_per_row(size: dict) -> float | None:
    if not size or (size.get("rows") or 0) < MIN_ROWS_FOR_BYTES or size.get("data_bytes") is None \
            or size.get("index_bytes") is None:
        return None
    return (size["data_bytes"] + size["index_bytes"]) / size["rows"]


def per_cadence(after_size: dict, before_size: dict, run: dict, units: dict) -> dict:
    """Rows per unit by cadence, from per-report attribution. Reports created in the run have no prior rows."""
    by_report = (after_size or {}).get("by_report")
    if by_report is None:
        return {}
    before = (before_size or {}).get("by_report", {})
    rows: dict[str, int] = {}
    for r in run.get("reports", []):
        rows[r["cadence"]] = rows.get(r["cadence"], 0) + by_report.get(r["id"], 0) - before.get(r["id"], 0)
    return {c: {"rows": n, "units": units.get(c), "multiplier": n / units[c] if units.get(c) else None}
            for c, n in sorted(rows.items())}


def run_driver_units(entries: float, run_share: float | None, run_resources: float | None) -> dict[str, float | None]:
    """How many of each driver the run contained. reportable x resources assumes reportable entries acquire the
    run's average resources per entry; capture doesn't split resources by reportability."""
    reportable = entries * run_share if run_share is not None else None
    return {"entry": entries,
            "reportable": reportable,
            "resources": entries * run_resources if run_resources is not None else None,
            "reportable_resources": reportable * run_resources
            if reportable is not None and run_resources is not None else None}


def normalize_drivers(store: dict, result: dict, units: dict[str, float | None], by_phase: dict | None,
                      a: dict) -> None:
    """Works out each driver term's `per` from the run, and what they give at the decided drivers.

    One term: rows / that driver's units in the run. Entry + reportable (the two acquisition passes): the
    Initial and Supplemental rows when capture split them by phase, otherwise the model's own ratio between the
    terms, scaled to the run.
    """
    terms = store["drivers"]
    missing = [t["driver"] for t in terms if not units.get(t["driver"])]
    if missing:
        result["note"] = f"can't normalize: the run has no {', '.join(missing)} figure"
        return
    delta = result["delta_rows"]
    names = [t["driver"] for t in terms]
    if len(terms) == 1:
        measured = [{"driver": names[0], "per": delta / units[names[0]]}]
        method = f"rows / run {names[0].replace('_', ' x ')}"
    elif sorted(names) == ["entry", "reportable"] and by_phase and by_phase.get("Initial") is not None:
        measured = [{"driver": "entry", "per": by_phase.get("Initial", 0) / units["entry"]},
                    {"driver": "reportable", "per": by_phase.get("Supplemental", 0) / units["reportable"]}]
        method = "QueryPhase split"
    else:
        expected = sum(t["per"] * units[t["driver"]] for t in terms)
        k = delta / expected if expected else 0.0
        measured = [{"driver": t["driver"], "per": t["per"] * k} for t in terms]
        method = "model's term ratio, scaled to the run"
    result["drivers_measured"] = measured
    result["normalization"] = method
    result["at_decided"] = sum(t["per"] * model.driver_value(t["driver"], a) for t in measured)


def diff_store(store: dict, before: dict, after: dict, run: dict, elapsed_days: float,
               a: dict | None = None, run_resources: float | None = None) -> dict:
    sid = store["id"]
    loc = store.get("location", {})
    a = a or model.load_assumptions(DEFAULT_ASSUMPTIONS)
    result = {"id": sid, "kind": store["kind"], "grain": store["grain"], "anchor": store["anchor"],
              "model_multiplier": model.multiplier(store, a) if "drivers" in store
              else store.get("multiplier", store.get("multiplier_param")),
              "model_bytes_per_row": store.get("bytes_per_row")}
    if "measured_with" in loc:
        return {**result, "reason": f"measured with {loc['measured_with']}"}
    units = grain_units(run, store["grain"], elapsed_days)
    if store["kind"] in ("sql", "mongo"):
        b_size, a_size = before.get("stores", {}).get(sid), after.get("stores", {}).get(sid)
        if a_size is None or b_size is None:
            return {**result, "reason": "missing from " + ("both snapshots" if a_size is None and b_size is None
                                                            else "before" if b_size is None else "after")}
        delta = a_size["rows"] - b_size["rows"]
        result.update(delta_rows=delta, units=units["all"], bytes_per_row=store_bytes_per_row(a_size))
        if store["anchor"] == "fixed_residency":
            return {**result, "reason": "work queue: rows drain, so counts don't give rows written (see series depth)"}
        if store["grain"] in ("patient_report", "report"):
            result["per_cadence"] = per_cadence(a_size, b_size, run, units)
            attributed = sum(v["rows"] for v in result["per_cadence"].values())
            result["coverage"] = attributed / delta if result["per_cadence"] and delta > 0 else None
    elif store["kind"] == "kafka":
        topics = loc["topics"]
        bt, at = before.get("kafka", {}), after.get("kafka", {})
        missing = [t for t in topics if t not in bt or t not in at]
        if missing:
            return {**result, "reason": f"topics missing: {', '.join(missing)}"}
        delta = sum(at[t]["high_watermark"] - bt[t]["high_watermark"] for t in topics)
        sampled = [(at[t].get("avg_message_bytes"), at[t].get("sampled", 0)) for t in topics]
        weight = sum(n for v, n in sampled if v is not None)
        result.update(delta_rows=delta, units=units["all"],
                      bytes_per_row=sum(v * n for v, n in sampled if v is not None) / weight if weight else None,
                      retention_ms=sorted({at[t].get("retention_ms") for t in topics} - {None}),
                      replication=sorted({at[t].get("replication") for t in topics} - {None}))
    elif store["kind"] == "loki":
        window = after.get("loki", {}).get(sid)
        if window is None:
            return {**result, "reason": "no Loki window in the after snapshot (needs --since or --previous)"}
        delta = sum(window["bytes_by_component"].values())
        result.update(delta_rows=delta, units=units["all"], bytes_per_row=None,
                      by_component=window["bytes_by_component"], window_s=window["window_s"])
    else:
        return {**result, "reason": f"unknown kind {store['kind']}"}
    if not units["all"]:
        return {**result, "reason": f"no {GRAIN_LABEL[store['grain']]} in the run"}
    result["multiplier"] = result["delta_rows"] / units["all"]
    if result["delta_rows"] < 0:
        result["note"] = "rows fell during the run (cleanup or retention ran); multiplier is not meaningful"
    elif result["delta_rows"] == 0:
        # Usually the process that writes the store didn't run (retention, rubric, census), not a true zero.
        result["note"] = "no rows written in the run; not proposed"
    elif "drivers" in store:
        share = (run.get("reportable") or {}).get("share")
        by_phase = (after.get("stores", {}).get(sid) or {}).get("by_phase")
        normalize_drivers(store, result, run_driver_units(units["all"], share, run_resources), by_phase, a)
    return result


def unmodeled_topics(stores: list[dict], before: dict, after: dict) -> dict[str, int]:
    modeled = {t for s in stores if s["kind"] == "kafka" for t in s.get("location", {}).get("topics", [])}
    bt, at = before.get("kafka", {}), after.get("kafka", {})
    return {t: at[t]["high_watermark"] - bt[t]["high_watermark"] for t in sorted(at)
            if t in bt and t not in modeled and at[t]["high_watermark"] > bt[t]["high_watermark"]}


def wait_deltas(before: dict, after: dict) -> list[dict]:
    rows = []
    for wait_type, a in after.items():
        if wait_type.startswith(BENIGN_WAITS) or wait_type in BENIGN_WAITS:
            continue
        b = before.get(wait_type, {"tasks": 0, "wait_ms": 0, "signal_ms": 0})
        d = {k: a[k] - b[k] for k in ("tasks", "wait_ms", "signal_ms")}
        if d["wait_ms"] > 0:
            rows.append({"wait_type": wait_type, **d})
    return sorted(rows, key=lambda r: r["wait_ms"], reverse=True)[:TOP_WAITS]


def contention(before: dict, after: dict) -> dict:
    out = {}
    b_sql = before.get("contention", {}).get("sql", {})
    for scope, a in after.get("contention", {}).get("sql", {}).items():
        b = b_sql.get(scope, {})
        out[scope] = {"services": a.get("services", []),
                      "top_waits": wait_deltas(b.get("waits", {}), a.get("waits", {})),
                      "deadlocks": a.get("deadlocks")}
    return {"sql": out, "cosmos": after.get("contention", {}).get("cosmos") or {}}


RESOURCES_STORE = "da.DataAcquisitionLogResourceId"  # one ID row per acquired resource


def diff_pair(stores: list[dict], before: dict, after: dict, a: dict | None = None) -> dict:
    a = a or model.load_assumptions(DEFAULT_ASSUMPTIONS)
    t0, t1 = parse_iso(before["taken_at"]), parse_iso(after["taken_at"])
    elapsed_days = (t1 - t0).total_seconds() / 86_400
    run = after.get("run") or {}
    # The run's own resources per entry come from the resource-ID store, measured first; every resource driver
    # term is normalized by it.
    resource_store = next((s for s in stores if s["id"] == RESOURCES_STORE), None)
    run_resources = None
    if resource_store is not None:
        r = diff_store({k: v for k, v in resource_store.items() if k != "drivers"} | {"multiplier": 1},
                       before, after, run, elapsed_days, a)
        run_resources = r.get("multiplier") if not r.get("note") else None
    results = [diff_store(s, before, after, run, elapsed_days, a, run_resources) for s in stores]
    by_id = {r["id"]: r for r in results}
    enabled = {tag: any(by_id[s["id"]].get("delta_rows", 0) > 0 for s in stores if tag in s.get("tags", []))
               for tag in ("shadow", "rubric")}
    resource_ids = by_id.get("da.DataAcquisitionLogResourceId", {})
    return {"before": before["taken_at"], "after": after["taken_at"], "environment": after.get("environment"),
            "code_version": after.get("code_version"), "elapsed_days": elapsed_days,
            "run": {**run_summary(run), "enabled": enabled,
                    "resources_acquired_per_patient_report": resource_ids.get("multiplier"),
                    "decided_share_reportable": a["share_reportable"],
                    "decided_resources_per_patient": a["resources_per_patient"]},
            "stores": results, "unmodeled_topics": unmodeled_topics(stores, before, after),
            "contention": contention(before, after),
            "context": after.get("context", {}),
            "unavailable": before.get("unavailable", []) + after.get("unavailable", [])}


# --------------------------------------------------------------------------------------------
# Series mode (subtask C)
# --------------------------------------------------------------------------------------------

def _rates(points: list[tuple[float, float]]) -> dict:
    """Peak interval rate and mean rate (per second) over (seconds, value) points."""
    if len(points) < 2:
        return {"peak": None, "mean": None, "samples": len(points)}
    interval = [(v2 - v1) / (t2 - t1) for (t1, v1), (t2, v2) in zip(points, points[1:]) if t2 > t1]
    span = points[-1][0] - points[0][0]
    return {"peak": max(interval, default=None), "mean": (points[-1][1] - points[0][1]) / span if span else None,
            "samples": len(points)}


def diff_series(stores: list[dict], series: list[dict], required: dict | None = None) -> dict:
    series = sorted(series, key=lambda s: s["taken_at"])
    t0 = parse_iso(series[0]["taken_at"])
    secs = [(parse_iso(s["taken_at"]) - t0).total_seconds() for s in series]
    by_store, by_service, depth = {}, {}, {}
    for st in stores:
        if st["kind"] not in ("sql", "mongo") or "measured_with" in st.get("location", {}):
            continue
        pts = [(t, s["stores"][st["id"]]["rows"]) for t, s in zip(secs, series) if st["id"] in s.get("stores", {})]
        if not pts:
            continue
        by_store[st["id"]] = {"service": st["service"], **_rates(pts)}
        if st["anchor"] == "fixed_residency":
            values = [v for _, v in pts]
            depth[st["id"]] = {"first": values[0], "max": max(values), "last": values[-1],
                               "drained": values[-1] <= values[0]}
    for service in sorted({v["service"] for v in by_store.values()}):
        members = [st["id"] for st in stores if st["service"] == service and st["id"] in by_store]
        pts = [(t, sum(s["stores"][m]["rows"] for m in members if m in s["stores"]))
               for t, s in zip(secs, series)]
        by_service[service] = _rates(pts)
    topics = {}
    for st in stores:
        if st["kind"] != "kafka":
            continue
        pts = [(t, sum(s["kafka"][x]["high_watermark"] for x in st["location"]["topics"]))
               for t, s in zip(secs, series) if all(x in s.get("kafka", {}) for x in st["location"]["topics"])]
        if pts:
            topics[st["id"]] = _rates(pts)
    lag: dict[str, dict[str, dict]] = {}
    for s in series:
        for group, per_topic in (s.get("consumer_lag") or {}).items():
            for topic, value in per_topic.items():
                entry = lag.setdefault(group, {}).setdefault(topic, {"max": 0, "last": 0})
                entry["max"] = max(entry["max"], value)
                entry["last"] = value
    blocking: dict[str, dict] = {}
    for s in series:
        for scope, c in (s.get("contention", {}).get("sql") or {}).items():
            b = c.get("blocking")
            if b is None:
                continue
            entry = blocking.setdefault(scope, {"samples": 0, "samples_blocked": 0, "max_blocked": 0,
                                                "max_wait_ms": 0, "by_wait_type": {}})
            entry["samples"] += 1
            entry["samples_blocked"] += 1 if b["blocked"] else 0
            entry["max_blocked"] = max(entry["max_blocked"], b["blocked"])
            entry["max_wait_ms"] = max(entry["max_wait_ms"], b["max_wait_ms"])
            for k, n in b["by_wait_type"].items():
                entry["by_wait_type"][k] = entry["by_wait_type"].get(k, 0) + n
    first_c, last_c = series[0].get("contention", {}).get("sql", {}), series[-1].get("contention", {}).get("sql", {})
    waits = {scope: wait_deltas(first_c.get(scope, {}).get("waits", {}), c.get("waits", {}))
             for scope, c in last_c.items()}
    return {"first": series[0]["taken_at"], "last": series[-1]["taken_at"], "samples": len(series),
            "stores": by_store, "services": by_service, "topics": topics, "transient_depth": depth,
            "consumer_lag": lag, "blocking": blocking, "waits": waits,
            "required": required or {}}


def required_rates(stores_path: Path, assumptions_path: Path) -> dict:
    """Peak rows/s the model requires per store for each processing window (from the model's own multipliers)."""
    a = model.load_assumptions(assumptions_path)
    stores, _ = model.load_stores(stores_path)
    arr = model.derive_arrivals(a)
    hours = a["peak_processing_windows_hours"]
    return {"windows_hours": hours,
            "stores": {s["id"]: [model.write_rates(s, arr, a, h)[1] for h in hours]
                       for s in stores if s["kind"] in ("sql", "mongo")}}


# --------------------------------------------------------------------------------------------
# Proposal and apply
# --------------------------------------------------------------------------------------------

def proposal(pair: dict) -> dict:
    stores = {}
    for r in pair["stores"]:
        if r.get("multiplier") is None or r.get("note"):
            continue
        entry = {"multiplier": sig(r["multiplier"]), "delta_rows": r["delta_rows"], "units": r["units"],
                 "grain": r["grain"]}
        if r.get("bytes_per_row") is not None:
            entry["bytes_per_row"] = round(r["bytes_per_row"])
        cad = {c: sig(v["multiplier"]) for c, v in (r.get("per_cadence") or {}).items()
               if c in ("daily", "monthly") and v["multiplier"] is not None}
        if cad:
            entry["by_cadence"] = cad
        if r.get("drivers_measured"):
            # A driver store is recorded as its per-driver terms, never as the run's blended multiplier.
            entry["drivers"] = [{"driver": t["driver"], "per": sig(t["per"], 6)} for t in r["drivers_measured"]]
            entry["normalization"] = r["normalization"]
            entry["at_decided"] = sig(r["at_decided"])
        stores[r["id"]] = entry
    run = pair["run"]
    assumptions = {}
    if run.get("completion_lag_days_median") is not None:
        assumptions["completion_lag_days"] = sig(run["completion_lag_days_median"], 3)
    return {"generated_at": iso(utc_now()), "environment": pair["environment"], "before": pair["before"],
            "after": pair["after"], "run": run, "stores": stores, "assumptions": assumptions}


def apply(proposal_doc: dict, source: str, stores_path: Path, assumptions_path: Path) -> list[str]:
    """Writes measured multipliers and bytes per row into stores.json, and assumptions into assumptions.json."""
    run = proposal_doc["run"]
    run_text = (f"{run['reports']} reports, {run['entries']} patient-report entries"
                + (f", reportable share {run['reportable_share']:.2f}" if run.get("reportable_share") is not None
                   else "")
                + (f", {run['resources_acquired_per_patient_report']:.0f} resources per entry"
                   if run.get("resources_acquired_per_patient_report") is not None else ""))
    doc = read_json(stores_path)
    changed = []
    for s in doc["stores"]:
        p = proposal_doc["stores"].get(s["id"])
        if p is None:
            continue
        s.pop("multiplier_param", None)
        rows_text = f"{p['delta_rows']} rows over {p['units']:g} {GRAIN_LABEL[p['grain']]}"
        if "drivers" in p:
            s.pop("multiplier", None)
            s["drivers"] = p["drivers"]
            rows_text += f"; normalized to drivers by {p['normalization']}"
        elif "drivers" in s:
            continue  # a driver store with no normalized terms is left alone rather than blended
        else:
            s["multiplier"] = p["multiplier"]
        s["multiplier_status"] = "measured"
        s["multiplier_source"] = f"measured: {source} ({run_text}; {rows_text})"
        if "bytes_per_row" in p:
            s["bytes_per_row"] = p["bytes_per_row"]
        if "by_cadence" in p:
            s["measured_by_cadence"] = p["by_cadence"]
        changed.append(s["id"])
    write_json(stores_path, doc)
    if proposal_doc.get("assumptions"):
        a = read_json(assumptions_path)
        for name, value in proposal_doc["assumptions"].items():
            a[name] = {"value": value, "source": f"measured: {source} ({run_text})"}
            changed.append(f"assumption {name}")
        write_json(assumptions_path, a)
    return changed


# --------------------------------------------------------------------------------------------
# Markdown summary
# --------------------------------------------------------------------------------------------

def _n(x, digits: int = 3) -> str:
    if x is None:
        return "–"
    if isinstance(x, str):  # a multiplier_param name, e.g. retention_participants
        return f"= {x}"
    if isinstance(x, float) and not x.is_integer():
        return f"{x:,.{digits}g}" if abs(x) < 1000 else f"{x:,.0f}"
    return f"{int(x):,}"


def _table(headers: list[str], rows: list[list], align: str | None = None) -> str:
    return model.md_table(headers, [[str(c) for c in r] for r in rows], align)


def summary(pair: dict | None, series_result: dict | None, tooling_check: bool) -> str:
    parts = ["# Capacity capture summary"]
    if tooling_check:
        parts.append("> **Tooling check, not a measurement.** These figures prove the capture scripts work. They come "
                     "from an unrepresentative run and must not be applied to the model.")
    if pair:
        run = pair["run"]
        parts.append(f"Environment **{pair['environment']}**, {pair['before']} → {pair['after']} "
                     f"({pair['elapsed_days'] * 24:.2f} h)" + (f", code {pair['code_version']}"
                                                               if pair.get("code_version") else "") + ".")
        cad = run.get("by_cadence", {})
        parts.append("## Run composition\n\n" + _table(["Item", "Value"], [
            ["Reports", run["reports"]],
            ["By cadence (reports / entries)", ", ".join(f"{c}: {v['reports']} / {v['entries']}"
                                                         for c, v in sorted(cad.items())) or "–"],
            ["Patient-report entries", run["entries"]],
            ["Census encounters / patients", f"{_n((run.get('census') or {}).get('encounters'))} / "
                                             f"{_n((run.get('census') or {}).get('patients'))}"],
            ["Facilities", _n(run.get("facilities"))],
            ["Reportable share (run / decided)", f"{_n(run.get('reportable_share'))} / "
                                                 f"{_n(run.get('decided_share_reportable'))}"],
            ["Scheduled reports terminal / completion lag median, max (days)",
             f"{run['terminal_reports']} / {_n(run.get('completion_lag_days_median'))}, "
             f"{_n(run.get('completion_lag_days_max'))}"],
            ["Resources acquired per patient-report (run / decided)",
             f"{_n(run.get('resources_acquired_per_patient_report'))} / "
             f"{_n(run.get('decided_resources_per_patient'))}"],
            ["Shadow / rubric validation active", f"{run['enabled']['shadow']} / {run['enabled']['rubric']}"],
        ], "ll"))
        rows, skipped = [], []
        for r in pair["stores"]:
            if r.get("multiplier") is None:
                skipped.append([r["id"], r.get("reason", "")])
                continue
            cad = ", ".join(f"{c} {_n(v['multiplier'])}" for c, v in (r.get("per_cadence") or {}).items()
                            if v["multiplier"] is not None)
            decided = (f"**{_n(r['at_decided'])}** ({r['normalization']})" if r.get("at_decided") is not None
                       else "–")
            rows.append([r["id"], r["grain"], _n(r["delta_rows"]), _n(r["units"]),
                         f"{_n(r['model_multiplier'])} → **{_n(r['multiplier'])}**", decided, cad or "–",
                         f"{_n(r['model_bytes_per_row'])} → {_n(r.get('bytes_per_row'))}",
                         _n(r.get("coverage")), r.get("note", "")])
        parts.append("## Store multipliers\n\nModel value (at the decided drivers) → measured in this run, per "
                     "grain unit. For stores with driver terms, *At decided drivers* re-expresses the measurement "
                     "at the decided reportable share and resources per patient, removing the run's own; that is "
                     "the figure `--apply` records, as per-driver terms. Coverage is the share of the store's new "
                     "rows attributed to the run's reports; below 1 means other activity wrote to the store too. "
                     "Bytes per row is the table-wide average (data + indexes) in the after snapshot, given only "
                     f"for tables of at least {MIN_ROWS_FOR_BYTES:,} rows; smaller tables are dominated by whole "
                     "8 KB pages. Completion lag counts scheduled (daily and monthly) reports only.\n\n"
                     + _table(["Store", "Grain", "Δ rows", "Units", "Multiplier", "At decided drivers",
                               "By cadence", "Bytes/row", "Coverage", "Note"], rows, "llrrllllrl"))
        if skipped:
            parts.append("**Not derived**\n\n" + _table(["Store", "Reason"], skipped, "ll"))
        loki = [r for r in pair["stores"] if r.get("by_component")]
        for r in loki:
            parts.append(f"## Loki bytes by component ({r['window_s']} s window)\n\n" + _table(
                ["Component", "Bytes", "Bytes / patient-report"],
                [[c, _n(b), _n(b / r["units"]) if r.get("units") else "–"]
                 for c, b in sorted(r["by_component"].items(), key=lambda kv: -kv[1])]))
        if pair["unmodeled_topics"]:
            parts.append("**Kafka topics with activity that no store models**\n\n" + _table(
                ["Topic", "Messages produced"], [[t, _n(n)] for t, n in pair["unmodeled_topics"].items()]))
        c = pair["contention"]
        for scope, entry in c["sql"].items():
            dl = entry.get("deadlocks")
            parts.append(f"## SQL contention: {scope}\n\nServices: {', '.join(entry['services'])}. Deadlocks since "
                         f"the run started: {dl['count'] if dl else 'not captured'}.\n\n"
                         + (_table(["Wait type", "Tasks", "Wait ms", "Signal ms"],
                                   [[w["wait_type"], _n(w["tasks"]), _n(w["wait_ms"]), _n(w["signal_ms"])]
                                    for w in entry["top_waits"]]) if entry["top_waits"] else "No non-benign waits."))
        if c["cosmos"]:
            parts.append("## Cosmos DB\n\n" + _table(["Metric", "Value"], [[k, _n(v)] for k, v in c["cosmos"].items()],
                                                     "lr"))
    if series_result:
        s = series_result
        parts.append(f"## Write throughput ({s['samples']} samples, {s['first']} → {s['last']})\n\nAchieved in this "
                     "environment. Peak is the highest rate between consecutive samples.\n\n" + _table(
                         ["Service", "Peak rows/s", "Mean rows/s"],
                         [[k, _n(v["peak"]), _n(v["mean"])] for k, v in s["services"].items()]))
        req = s.get("required", {})
        hours = req.get("windows_hours", [])
        busiest = sorted(s["stores"].items(), key=lambda kv: -(kv[1]["peak"] or 0))[:15]
        parts.append("**Busiest stores**: achieved, and what the model requires for the aligned month-end batch "
                     "(using the model's current multipliers)\n\n" + _table(
                         ["Store", "Peak rows/s", "Mean rows/s"] + [f"Required, {h:g} h" for h in hours],
                         [[k, _n(v["peak"]), _n(v["mean"])] + [_n(x) for x in req.get("stores", {}).get(k, [])]
                          for k, v in busiest]))
        if s["transient_depth"]:
            parts.append("## Transient queues\n\n" + _table(
                ["Store", "First", "Max", "Last", "Drained"],
                [[k, _n(v["first"]), _n(v["max"]), _n(v["last"]), v["drained"]]
                 for k, v in s["transient_depth"].items()]))
        if s["topics"]:
            parts.append("## Kafka produce rates\n\n" + _table(
                ["Store", "Peak msg/s", "Mean msg/s"], [[k, _n(v["peak"]), _n(v["mean"])] for k, v in s["topics"].items()]))
        lag_rows = [[g, t, _n(v["max"]), _n(v["last"])] for g, per in s["consumer_lag"].items()
                    for t, v in per.items() if v["max"] > 0]
        if lag_rows:
            parts.append("## Consumer lag peaks\n\n" + _table(["Group", "Topic", "Max lag", "Last lag"],
                                                             sorted(lag_rows, key=lambda r: r[0])))
        for scope, b in s["blocking"].items():
            parts.append(f"## Blocking: {scope}\n\n{b['samples_blocked']} of {b['samples']} samples had blocked "
                         f"requests (max {b['max_blocked']} at once, longest wait {b['max_wait_ms']:,} ms)."
                         + (" Wait types: " + ", ".join(f"{k} ({n})" for k, n in b["by_wait_type"].items())
                            if b["by_wait_type"] else ""))
    unavailable = (pair or {}).get("unavailable", [])
    if unavailable:
        seen, rows = set(), []
        for u in unavailable:
            key = (u["source"], u["item"], u["reason"])
            if key not in seen:
                seen.add(key)
                rows.append(list(key))
        parts.append("## Unavailable\n\n" + _table(["Source", "Item", "Reason"], rows, "lll"))
    return "\n\n".join(parts) + "\n"


# --------------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------------

def parse_args(argv):
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--before", type=Path)
    p.add_argument("--after", type=Path)
    p.add_argument("--series", type=Path, help="JSON-lines series from snapshot.py --interval")
    p.add_argument("--out-dir", type=Path, help="where summary.md, comparison.json and proposed-update.json go")
    p.add_argument("--tooling-check", action="store_true", help="label the summary as a tooling check")
    p.add_argument("--apply", type=Path, metavar="PROPOSAL", help="write a proposed-update.json into the model")
    p.add_argument("--source", help="for --apply: environment, date, ticket and run size")
    p.add_argument("--stores", type=Path, help="stores.json (required with --apply; default for diffs)")
    p.add_argument("--assumptions", type=Path, help="assumptions.json (required with --apply; default for diffs)")
    return p.parse_args(argv)


def main(argv=None) -> int:
    args = parse_args(argv)
    if args.apply:
        if not args.source or not args.source.strip():
            print("error: --apply needs --source naming the environment, date, ticket and run size", file=sys.stderr)
            return 2
        if not args.stores or not args.assumptions:
            print("error: --apply needs explicit --stores and --assumptions paths", file=sys.stderr)
            return 2
        changed = apply(read_json(args.apply), args.source.strip(), args.stores, args.assumptions)
        print(f"updated {len(changed)} entries: {', '.join(changed)}", file=sys.stderr)
        return 0
    if not args.out_dir or not ((args.before and args.after) or args.series):
        print("error: give --out-dir, and --before with --after, or --series", file=sys.stderr)
        return 2
    stores_path = args.stores or DEFAULT_STORES
    assumptions_path = args.assumptions or DEFAULT_ASSUMPTIONS
    stores = read_json(stores_path)["stores"]
    args.out_dir.mkdir(parents=True, exist_ok=True)
    pair = diff_pair(stores, read_json(args.before), read_json(args.after), model.load_assumptions(assumptions_path)) \
        if args.before and args.after else None
    series_result = diff_series(stores, read_series(args.series), required_rates(stores_path, assumptions_path)) \
        if args.series else None
    (args.out_dir / "summary.md").write_text(summary(pair, series_result, args.tooling_check), encoding="utf-8",
                                             newline="\n")
    write_json(args.out_dir / "comparison.json", {"pair": pair, "series": series_result})
    if pair:
        write_json(args.out_dir / "proposed-update.json", proposal(pair))
    print(f"wrote {args.out_dir / 'summary.md'}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
