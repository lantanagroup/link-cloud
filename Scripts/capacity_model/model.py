"""Metadata volume model for LEGPROG-516 (LEGLINK-1364).

Reads the global assumptions and the per-store table, and writes arrival rates, held volume at a
retention window, projections across windows, peak write throughput and the D1 comparison. See
README.md for the method and for how LEGLINK-1369 feeds measured multipliers back in.

Run from the repository root:

    python Scripts/capacity_model/model.py
    python Scripts/capacity_model/model.py --format csv --out sizing.csv
    python Scripts/capacity_model/model.py --set share_both=0.45 --exclude-tag shadow
"""

from __future__ import annotations

import argparse
import csv
import io
import json
import sys
from dataclasses import dataclass
from pathlib import Path

HERE = Path(__file__).resolve().parent
DEFAULT_ASSUMPTIONS = HERE / "assumptions.json"
DEFAULT_STORES = HERE / "stores.json"

GRAINS = ("patient_report", "report", "census_patient", "facility_day")
ANCHORS = ("report", "record_age", "own_retention", "fixed_residency", "unbounded")
CADENCES = ("daily", "monthly")
SECONDS_PER_DAY = 86_400


# --------------------------------------------------------------------------------------------
# Inputs
# --------------------------------------------------------------------------------------------

def load_assumptions(path: Path, overrides: list[str] | None = None) -> dict:
    """Returns {name: value}, applying `name=value` overrides (values parsed as JSON)."""
    raw = json.loads(path.read_text(encoding="utf-8"))
    values = {k: v["value"] for k, v in raw.items() if not k.startswith("_")}
    for item in overrides or []:
        name, sep, text = item.partition("=")
        if not sep or name not in values:
            raise ValueError(f"--set expects an existing assumption as name=value, got {item!r}")
        values[name] = json.loads(text)
    return values


def load_stores(path: Path, exclude_tags: list[str] | None = None) -> tuple[list[dict], list[dict]]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    excluded_tags = set(exclude_tags or [])
    stores = [s for s in raw["stores"] if not excluded_tags.intersection(s.get("tags", []))]
    return stores, raw.get("excluded", [])


def validate(a: dict, stores: list[dict]) -> None:
    low = max(0.0, a["share_monthly"] + a["share_daily"] - 1)
    high = min(a["share_monthly"], a["share_daily"])
    if not low - 1e-9 <= a["share_both"] <= high + 1e-9:
        raise ValueError(f"share_both must be between {low:.2f} and {high:.2f}, got {a['share_both']}")
    seen = set()
    for s in stores:
        if s["id"] in seen:
            raise ValueError(f"duplicate store id {s['id']}")
        seen.add(s["id"])
        if s["grain"] not in GRAINS:
            raise ValueError(f"{s['id']}: unknown grain {s['grain']}")
        if s["anchor"] not in ANCHORS:
            raise ValueError(f"{s['id']}: unknown anchor {s['anchor']}")
        if s["anchor"] in ("own_retention", "fixed_residency") and not (
                "residency_days" in s or "residency_days_param" in s):
            raise ValueError(f"{s['id']}: anchor {s['anchor']} needs residency_days or residency_days_param")
        if "multiplier" not in s and "multiplier_param" not in s:
            raise ValueError(f"{s['id']}: needs multiplier or multiplier_param")


# --------------------------------------------------------------------------------------------
# Derived arrival rates
# --------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Cadence:
    name: str
    facilities: float
    period_days: float
    patients_per_report: float
    reports_per_month: float

    @property
    def entries_per_month(self) -> float:
        return self.reports_per_month * self.patients_per_report

    @property
    def boundary_reports(self) -> float:
        """Reports closing at one aligned period boundary: one per enrolled facility."""
        return self.facilities

    @property
    def boundary_entries(self) -> float:
        return self.facilities * self.patients_per_report


@dataclass(frozen=True)
class Arrivals:
    days_per_month: float
    distinct_facilities: float
    both_facilities: float
    cadences: dict[str, Cadence]
    census_patients_per_month: float

    @property
    def reports_per_month(self) -> float:
        return sum(c.reports_per_month for c in self.cadences.values())

    @property
    def entries_per_month(self) -> float:
        return sum(c.entries_per_month for c in self.cadences.values())

    @property
    def boundary_entries(self) -> float:
        """Aligned worst case: every facility's monthly and daily batch at the month boundary."""
        return sum(c.boundary_entries for c in self.cadences.values())

    @property
    def boundary_reports(self) -> float:
        return sum(c.boundary_reports for c in self.cadences.values())


def derive_arrivals(a: dict) -> Arrivals:
    days = a["days_per_month"]
    monthly = a["facilities"] * a["share_monthly"]
    daily = a["facilities"] * a["share_daily"]
    both = a["facilities"] * a["share_both"]
    distinct = monthly + daily - both
    cadences = {
        "daily": Cadence("daily", daily, 1, a["patients_per_daily_report"], daily * days),
        "monthly": Cadence("monthly", monthly, days, a["patients_per_monthly_report"], monthly),
    }
    return Arrivals(days, distinct, both, cadences, distinct * a["census_patients_per_facility_month"])


# --------------------------------------------------------------------------------------------
# Per-store volume
# --------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Component:
    """One arrival stream into a store. Report-keyed grains get one per cadence."""
    cadence: str | None
    rows_per_month: float
    period_days: float
    boundary_rows: float | None  # rows written at an aligned period boundary; None = never batched


def multiplier(store: dict, a: dict) -> float:
    if "multiplier_param" in store:
        return float(a[store["multiplier_param"]])
    return float(store["multiplier"])


def copies(store: dict, a: dict) -> float:
    return float(a[store["copies_param"]]) if "copies_param" in store else 1.0


def residency_param_days(store: dict, a: dict) -> float:
    if "residency_days" in store:
        return float(store["residency_days"])
    value = float(a[store["residency_days_param"]])
    return value / 24 if store.get("residency_param_unit") == "hours" else value


def components(store: dict, arr: Arrivals, a: dict) -> list[Component]:
    m = multiplier(store, a)
    grain = store["grain"]
    if grain == "patient_report":
        return [Component(c.name, c.entries_per_month * m, c.period_days, c.boundary_entries * m)
                for c in arr.cadences.values()]
    if grain == "report":
        return [Component(c.name, c.reports_per_month * m, c.period_days, c.boundary_reports * m)
                for c in arr.cadences.values()]
    if grain == "census_patient":
        return [Component(None, arr.census_patients_per_month * m, 0, None)]
    return [Component(None, arr.distinct_facilities * arr.days_per_month * m, 0, None)]  # facility_day


def residency_days(store: dict, comp: Component, a: dict, window_months: float, peak: bool) -> float:
    """Expected (steady) or maximum (peak) time a row stays in the store.

    For `unbounded` stores the window is read as a horizon: time since the store started filling.
    """
    window = window_months * a["days_per_month"]
    cleanup = a["cleanup_interval_days"]
    anchor = store["anchor"]
    if anchor == "report":
        before_period_end = comp.period_days if peak else comp.period_days / 2
        return before_period_end + a["completion_lag_days"] + window + (cleanup if peak else cleanup / 2)
    if anchor == "record_age":
        return window + (cleanup if peak else cleanup / 2)
    if anchor in ("own_retention", "fixed_residency"):
        return residency_param_days(store, a)
    return window  # unbounded


def held_rows(store: dict, arr: Arrivals, a: dict, window_months: float, peak: bool = False) -> float:
    return sum(c.rows_per_month / a["days_per_month"] * residency_days(store, c, a, window_months, peak)
               for c in components(store, arr, a))


def rows_per_month(store: dict, arr: Arrivals, a: dict) -> float:
    return sum(c.rows_per_month for c in components(store, arr, a))


def scales_with_window(store: dict) -> bool:
    return store["anchor"] in ("report", "record_age")


def slope_offset(store: dict, arr: Arrivals, a: dict) -> tuple[float, float] | None:
    """Held(W) = R * (W + c): R rows per month of retention, c offset in months. None if W doesn't apply."""
    if not scales_with_window(store):
        return None
    r = rows_per_month(store, arr, a)
    if r == 0:
        return 0.0, 0.0
    return r, held_rows(store, arr, a, 0) / r


def write_rates(store: dict, arr: Arrivals, a: dict, window_hours: float) -> tuple[float, float]:
    """(steady rows/s, peak rows/s) where peak processes each batched stream's boundary rows in `window_hours`."""
    steady = rows_per_month(store, arr, a) / (a["days_per_month"] * SECONDS_PER_DAY)
    peak = 0.0
    for c in components(store, arr, a):
        if c.boundary_rows is None:
            peak += c.rows_per_month / (a["days_per_month"] * SECONDS_PER_DAY)
        else:
            peak += c.boundary_rows / (window_hours * 3600)
    return steady, peak


def bytes_held(store: dict, rows: float, a: dict) -> float:
    return rows * float(store["bytes_per_row"]) * copies(store, a)


# --------------------------------------------------------------------------------------------
# Formatting
# --------------------------------------------------------------------------------------------

def _sig(n: float) -> str:
    """Three significant figures, without switching to exponent notation below 1,000."""
    return f"{n:,.0f}" if abs(n) >= 100 else f"{n:.3g}"


def fmt_count(n: float) -> str:
    for size, suffix in ((1e12, "T"), (1e9, "B"), (1e6, "M"), (1e3, "k")):
        if abs(n) >= size:
            return f"{_sig(n / size)} {suffix}"
    return _sig(n)


def fmt_bytes(n: float) -> str:
    for size, suffix in ((1e15, "PB"), (1e12, "TB"), (1e9, "GB"), (1e6, "MB"), (1e3, "KB")):
        if abs(n) >= size:
            return f"{_sig(n / size)} {suffix}"
    return f"{n:,.0f} B"


def fmt_rate(n: float) -> str:
    return fmt_count(n) + "/s"


def window_label(w: float, a: dict) -> str:
    text = f"{w:g} mo"
    return text + " †" if w > a["window_cap_months"] else text


def md_table(headers: list[str], rows: list[list[str]], align: str | None = None) -> str:
    align = align or "l" + "r" * (len(headers) - 1)
    marks = {"l": "---", "r": "---:", "c": ":---:"}
    out = ["| " + " | ".join(headers) + " |", "| " + " | ".join(marks[c] for c in align) + " |"]
    out += ["| " + " | ".join(r) + " |" for r in rows]
    return "\n".join(out)


def store_label(store: dict) -> str:
    return f"{store['service']}: {store['store']}"


def services_in_order(stores: list[dict]) -> list[str]:
    order: list[str] = []
    for s in stores:
        if s["service"] not in order:
            order.append(s["service"])
    return order


# --------------------------------------------------------------------------------------------
# Report sections
# --------------------------------------------------------------------------------------------

def section_inputs(a: dict, arr: Arrivals) -> str:
    rows = [[k, json.dumps(v)] for k, v in a.items()]
    derived = [
        ["distinct enrolled facilities", fmt_count(arr.distinct_facilities)],
        ["facilities on both cadences", fmt_count(arr.both_facilities)],
        ["implied daily reports per unique patient per month",
         f"{a['patients_per_daily_report'] * a['days_per_month'] / a['patients_per_monthly_report']:.1f}"],
    ]
    return ("## Inputs\n\n" + md_table(["Parameter", "Value"], rows, "ll") + "\n\n"
            + md_table(["Derived", "Value"], derived, "lr"))


def section_arrivals(a: dict, arr: Arrivals) -> str:
    d, m = arr.cadences["daily"], arr.cadences["monthly"]
    days = a["days_per_month"]
    rows = [
        ["Enrolled facilities", fmt_count(d.facilities), fmt_count(m.facilities),
         f"{fmt_count(arr.distinct_facilities)} distinct"],
        ["Patients per report", fmt_count(d.patients_per_report), fmt_count(m.patients_per_report), ""],
        ["Reports / month", fmt_count(d.reports_per_month), fmt_count(m.reports_per_month),
         fmt_count(arr.reports_per_month)],
        ["Reports / day (average)", fmt_count(d.reports_per_month / days), fmt_count(m.reports_per_month / days),
         fmt_count(arr.reports_per_month / days)],
        ["Patient-report entries / month", fmt_count(d.entries_per_month), fmt_count(m.entries_per_month),
         fmt_count(arr.entries_per_month)],
        ["Patient-report entries / day (average)", fmt_count(d.entries_per_month / days),
         fmt_count(m.entries_per_month / days), fmt_count(arr.entries_per_month / days)],
        ["Acquisitions / month (one per patient-report)", fmt_count(d.entries_per_month),
         fmt_count(m.entries_per_month), fmt_count(arr.entries_per_month)],
        ["Census patients / month", "", "", fmt_count(arr.census_patients_per_month)],
        ["Aligned boundary batch (entries)", fmt_count(d.boundary_entries), fmt_count(m.boundary_entries),
         fmt_count(arr.boundary_entries)],
        ["Aligned boundary batch (reports)", fmt_count(d.boundary_reports), fmt_count(m.boundary_reports),
         fmt_count(arr.boundary_reports)],
    ]
    for hours in a["peak_processing_windows_hours"]:
        rows.append([f"Boundary entries per minute in a {hours:g} h window", "", "",
                     fmt_count(arr.boundary_entries / (hours * 60))])
    return "## Arrival rates\n\n" + md_table(["Quantity", "Daily", "Monthly", "Total"], rows)


def section_stores(a: dict, arr: Arrivals, stores: list[dict], window: float) -> str:
    rows = []
    for s in stores:
        so = slope_offset(s, arr, a)
        steady = held_rows(s, arr, a, window)
        rows.append([
            store_label(s), s["grain"], s["anchor"],
            f"{multiplier(s, a):g} ({s['multiplier_status']})",
            fmt_count(rows_per_month(s, arr, a)),
            fmt_count(steady), fmt_count(held_rows(s, arr, a, window, peak=True)),
            fmt_bytes(bytes_held(s, steady, a)),
            fmt_count(so[0]) if so else "n/a", f"{so[1]:.2f}" if so else "n/a",
        ])
    headers = ["Store", "Grain", "Anchor", "Multiplier", "Rows / month", "Held (steady)", "Held (peak)",
               "Bytes (steady)", "R (rows / retention-month)", "c (months)"]
    return (f"## Per-store volume at a {window_label(window, a)} window\n\n"
            "Steady = uniform arrivals; peak = maximum held just before the oldest cohort becomes eligible. "
            "`unbounded` stores read the window as time since go-live. Bytes include replication where set.\n\n"
            + md_table(headers, rows, "lll" + "r" * 7))


def section_projections(a: dict, arr: Arrivals, stores: list[dict]) -> str:
    windows = a["projection_windows_months"]
    rows = []
    grand = [0.0] * len(windows)
    for service in services_in_order(stores):
        members = [s for s in stores if s["service"] == service]
        cells = []
        for i, w in enumerate(windows):
            rows_held = sum(held_rows(s, arr, a, w) for s in members)
            b = sum(bytes_held(s, held_rows(s, arr, a, w), a) for s in members)
            grand[i] += b
            cells.append(f"{fmt_count(rows_held)} / {fmt_bytes(b)}")
        anchors = {s["anchor"] for s in members}
        suffix = {frozenset({"own_retention"}): " (own retention)",
                  frozenset({"fixed_residency"}): " (transient)"}.get(frozenset(anchors), "")
        rows.append([service + suffix] + cells)
    rows.append(["**Total bytes**"] + [f"**{fmt_bytes(b)}**" for b in grand])
    return ("## Projections by service (steady state, rows / bytes)\n\n"
            "† above the 36-month cap that ADR 08 and LEGLINK-1242 enforce. Kafka and Loki hold their own "
            "retention, so their figures don't change with the window.\n\n"
            + md_table(["Service"] + [window_label(w, a) for w in windows], rows))


def section_throughput(a: dict, arr: Arrivals, stores: list[dict]) -> str:
    hours = a["peak_processing_windows_hours"]
    rows = []
    for service in services_in_order(stores):
        members = [s for s in stores if s["service"] == service and s["kind"] != "loki"]
        if not members:
            continue
        steady = sum(write_rates(s, arr, a, hours[0])[0] for s in members)
        peaks = [sum(write_rates(s, arr, a, h)[1] for s in members) for h in hours]
        rows.append([service, fmt_rate(steady)] + [fmt_rate(p) for p in peaks])
    top = sorted((s for s in stores if s["kind"] != "loki"),
                 key=lambda s: write_rates(s, arr, a, hours[-1])[1], reverse=True)[:10]
    top_rows = [[store_label(s), fmt_rate(write_rates(s, arr, a, hours[0])[0])]
                + [fmt_rate(write_rates(s, arr, a, h)[1]) for h in hours] for s in top]
    headers = ["Service", "Steady"] + [f"Peak, {h:g} h window" for h in hours]
    return ("## Write throughput (rows or messages per second)\n\n"
            "Peak = the aligned boundary batch written within the window; streams that aren't batched "
            "(census, per-facility) contribute their steady rate.\n\n"
            + md_table(headers, rows) + "\n\n**Ten busiest stores at peak**\n\n"
            + md_table(["Store"] + headers[1:], top_rows))


def section_d1(a: dict, arr: Arrivals, stores: list[dict], window: float) -> str:
    members = [s for s in stores if "D1" in s.get("divergences", [])]
    horizons = [12, 36, 84]
    rows = []
    for s in members:
        bounded = bytes_held(s, held_rows(s, arr, a, window), a)
        unbounded = [bytes_held(s, rows_per_month(s, arr, a) * h, a) for h in horizons]
        rows.append([store_label(s), fmt_bytes(bounded)] + [fmt_bytes(u) for u in unbounded])
    total_b = sum(bytes_held(s, held_rows(s, arr, a, window), a) for s in members)
    totals = [sum(bytes_held(s, rows_per_month(s, arr, a) * h, a) for s in members) for h in horizons]
    rows.append(["**Total**", f"**{fmt_bytes(total_b)}**"] + [f"**{fmt_bytes(t)}**" for t in totals])
    headers = ["Store", f"Purged at {window_label(window, a)} (analysis)"] + [
        f"Kept, after {h} mo (LEGLINK-1242 Scrub)" for h in horizons]
    return ("## D1: purge on the shared schedule vs. keep on scrub\n\n"
            + md_table(headers, rows))


def section_excluded(excluded: list[dict]) -> str:
    return "## Excluded stores\n\n" + md_table(["Store", "Reason"], [[e["store"], e["reason"]] for e in excluded], "ll")


def markdown_report(a: dict, stores: list[dict], excluded: list[dict], window: float) -> str:
    arr = derive_arrivals(a)
    placeholders = sum(1 for s in stores if s["multiplier_status"] in ("placeholder", "prior"))
    header = (f"# Metadata volume model output\n\nGenerated by `Scripts/capacity_model/model.py`. "
              f"{placeholders} of {len(stores)} store multipliers are placeholders or priors, so these "
              "figures show the shape of growth, not measured sizes.")
    parts = [header, section_inputs(a, arr), section_arrivals(a, arr), section_stores(a, arr, stores, window),
             section_projections(a, arr, stores), section_throughput(a, arr, stores),
             section_d1(a, arr, stores, window), section_excluded(excluded)]
    return "\n\n".join(parts) + "\n"


def csv_report(a: dict, stores: list[dict], window: float) -> str:
    arr = derive_arrivals(a)
    buf = io.StringIO()
    writer = csv.writer(buf, lineterminator="\n")
    windows = a["projection_windows_months"]
    hours = a["peak_processing_windows_hours"]
    writer.writerow(["id", "service", "kind", "store", "grain", "anchor", "multiplier", "multiplier_status",
                     "multiplier_source", "bytes_per_row", "rows_per_month",
                     f"held_steady_{window:g}mo", f"held_peak_{window:g}mo", f"bytes_steady_{window:g}mo",
                     "slope_rows_per_retention_month", "offset_months"]
                    + [f"held_steady_{w:g}mo" for w in windows] + [f"bytes_steady_{w:g}mo" for w in windows]
                    + ["steady_rows_per_s"] + [f"peak_rows_per_s_{h:g}h" for h in hours]
                    + ["adr_action", "analysis_action", "pi", "divergences"])
    for s in stores:
        so = slope_offset(s, arr, a)
        steady = held_rows(s, arr, a, window)
        held = [held_rows(s, arr, a, w) for w in windows]
        writer.writerow([s["id"], s["service"], s["kind"], s["store"], s["grain"], s["anchor"], multiplier(s, a),
                         s["multiplier_status"], s["multiplier_source"], s["bytes_per_row"],
                         round(rows_per_month(s, arr, a)), round(steady),
                         round(held_rows(s, arr, a, window, peak=True)), round(bytes_held(s, steady, a)),
                         round(so[0]) if so else "", round(so[1], 4) if so else ""]
                        + [round(h) for h in held] + [round(bytes_held(s, h, a)) for h in held]
                        + [round(write_rates(s, arr, a, hours[0])[0], 3)]
                        + [round(write_rates(s, arr, a, h)[1], 3) for h in hours]
                        + [s.get("adr_action", ""), s.get("analysis_action", ""), s.get("pi", ""),
                           ";".join(s.get("divergences", []))])
    return buf.getvalue()


# --------------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------------

def parse_args(argv: list[str] | None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--assumptions", type=Path, default=DEFAULT_ASSUMPTIONS)
    p.add_argument("--stores", type=Path, default=DEFAULT_STORES)
    p.add_argument("--window-months", type=float, help="retention window for the per-store table "
                   "(default: reference_window_months)")
    p.add_argument("--set", action="append", default=[], metavar="NAME=VALUE",
                   help="override an assumption; VALUE is JSON (repeatable)")
    p.add_argument("--exclude-tag", action="append", default=[], metavar="TAG",
                   help="leave out stores carrying TAG, e.g. shadow or rubric (repeatable)")
    p.add_argument("--format", choices=("md", "csv"), default="md")
    p.add_argument("--out", type=Path, help="write here instead of stdout")
    return p.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    a = load_assumptions(args.assumptions, args.set)
    stores, excluded = load_stores(args.stores, args.exclude_tag)
    validate(a, stores)
    window = args.window_months if args.window_months is not None else a["reference_window_months"]
    text = markdown_report(a, stores, excluded, window) if args.format == "md" else csv_report(a, stores, window)
    if args.out:
        args.out.write_text(text, encoding="utf-8", newline="\n")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
