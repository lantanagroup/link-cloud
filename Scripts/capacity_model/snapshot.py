"""Snapshot every in-scope store, with the grain counts and run facts, for the LEGPROG-516 volume model.

A full snapshot records sizes, per-report attribution, run facts, contention counters, Kafka message sizes, Loki
bytes and environment context. A light snapshot records only what changes fast (row counts, Kafka watermarks
and lag, blocking, wait counters) and is meant to be repeated with --interval. diff_snapshots.py turns a pair
or a series into multipliers, write rates and contention deltas.

Run from the repository root, for example around a run on the local stack:

    python Scripts/capacity_model/snapshot.py --label before --out before.json
    python Scripts/capacity_model/snapshot.py --profile light --interval 15 --until 2026-11-11T18:00:00Z --out run.jsonl
    python Scripts/capacity_model/snapshot.py --label after --previous before.json --out after.json
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from capture import SCHEMA_VERSION  # noqa: E402
from capture import cloud, kafka, loki, mongo, sql  # noqa: E402
from capture import targets as targets_mod  # noqa: E402
from capture.common import (Unavailable, iso, normalize_guid, parse_iso, read_json, stores_of_kind,  # noqa: E402
                            utc_now, write_json)

DEFAULT_STORES = HERE / "stores.json"
SAMPLE_PER_PARTITION = 50


class Session:
    """Connections for one environment, opened lazily and reused across a series."""

    def __init__(self, targets: dict, unavailable: Unavailable):
        self.targets = targets
        self.unavailable = unavailable
        self._sql: dict[str, object] = {}
        self._unreachable: set[str] = set()
        self._mongo = None
        self._kafka = None

    def sql(self, service: str):
        if service not in self._sql:
            server = targets_mod.sql_server_key(self.targets, service)
            if server in self._unreachable:  # don't wait out the same timeout once per database
                self._sql[service] = None
                self.unavailable.add("sql", service, f"server {server} unreachable (an earlier connection failed)")
                return None
            try:
                self._sql[service] = targets_mod.sql_connect(self.targets, service)
                if self._sql[service] is None:
                    self.unavailable.add("sql", service, "no connection configured, or the connection failed")
            except Exception as e:  # noqa: BLE001
                self._sql[service] = None
                self.unavailable.add("sql", service, e)
            if self._sql[service] is None and server != "unknown" \
                    and not targets_mod.sql_server_reachable(self.targets, service):
                self._unreachable.add(server)
        return self._sql[service]

    def mongo(self):
        if self._mongo is None:
            cfg = self.targets.get("mongo", {})
            from pymongo import MongoClient  # noqa: PLC0415 - optional dependency
            self._mongo = MongoClient(cfg["connection_string"], serverSelectionTimeoutMS=10000)
        return self._mongo

    def kafka(self):
        if self._kafka is None:
            self._kafka = kafka.clients(self.targets["kafka"])
        return self._kafka

    def close(self) -> None:
        for conn in self._sql.values():
            if conn is not None:
                conn.close()
        if self._mongo is not None:
            self._mongo.close()
        if self._kafka is not None:
            self._kafka[1].close()


# --------------------------------------------------------------------------------------------
# Sections
# --------------------------------------------------------------------------------------------

def capture_run(session: Session, since, report_ids: list[str] | None) -> dict | None:
    """Run reports and grain counts. Needs --since or --report-ids; None otherwise."""
    if since is None and not report_ids:
        return None
    u = session.unavailable
    run: dict = {"since": iso(since), "reports": []}
    conn = session.sql("report")
    if conn is None:
        return run
    try:
        run["reports"] = sql.run_reports(conn, since, report_ids)
        ids = [r["id"] for r in run["reports"]]
        entries = sql.entries_by_report(conn, ids)
        run["entries_by_report"] = entries
        by_cadence: dict[str, dict] = {}
        for r in run["reports"]:
            c = by_cadence.setdefault(r["cadence"], {"reports": 0, "entries": 0})
            c["reports"] += 1
            c["entries"] += entries.get(r["id"], 0)
        run["by_cadence"] = by_cadence
        lags = [r["completion_lag_days"] for r in run["reports"] if r["completion_lag_days"] is not None]
        run["completion_lag_days"] = lags
    except Exception as e:  # noqa: BLE001
        u.add("sql", "run reports", e)
    if since is not None:
        census = session.sql("census")
        if census is not None:
            try:
                run["census"] = sql.census_patients(census, since)
            except Exception as e:  # noqa: BLE001
                u.add("sql", "census patients", e)
    tenant = session.sql("tenant")
    if tenant is not None:
        try:
            run["facilities"] = sql.facility_count(tenant)
        except Exception as e:  # noqa: BLE001
            u.add("sql", "facility count", e)
    return run


def capture_sql(session: Session, stores: list[dict], full: bool, report_ids: list[str]) -> dict:
    out: dict = {}
    by_service: dict[str, list[dict]] = {}
    for s in stores_of_kind(stores, "sql"):
        by_service.setdefault(s["location"]["service"], []).append(s)
    for service, members in by_service.items():
        conn = session.sql(service)
        if conn is None:
            continue
        try:
            sizes = sql.table_sizes(conn)
        except Exception as e:  # noqa: BLE001
            session.unavailable.add("sql", f"{service} table sizes", e)
            continue
        for s in members:
            size = sql.store_size(sizes, s["location"])
            if size is None:
                session.unavailable.add("sql", s["id"], "table not found")
                continue
            if full and report_ids and "attribution" in s["location"]:
                try:
                    size["by_report"] = sql.attribution_counts(conn, s["location"], report_ids)
                except Exception as e:  # noqa: BLE001
                    session.unavailable.add("sql", f"{s['id']} attribution", e)
            out[s["id"]] = size
    return out


def capture_sql_servers(session: Session, services: list[str], full: bool, since, keep_xml: bool) -> tuple[dict, dict]:
    """(contention, context) for each distinct server, or each database on Azure SQL Database."""
    contention, context = {}, {}
    for service in services:
        conn = session.sql(service)
        if conn is None:
            continue
        try:
            info = sql.server_info(conn)
        except Exception as e:  # noqa: BLE001
            session.unavailable.add("sql", f"{service} server info", e)
            continue
        context[service] = info
        edition = info["engine_edition"]
        # Wait statistics are per server, except on Azure SQL Database, where they are per database.
        scope = service if edition == sql.AZURE_SQL_DATABASE else targets_mod.sql_server_key(session.targets, service)
        if scope in contention:
            contention[scope]["services"].append(service)
            continue
        entry: dict = {"engine_edition": edition, "services": [service]}
        for name, fn in (("waits", lambda: sql.wait_stats(conn, edition)), ("blocking", lambda: sql.blocking(conn))):
            try:
                entry[name] = fn()
            except Exception as e:  # noqa: BLE001
                session.unavailable.add("sql", f"{scope} {name}", e)
        if full and since is not None:
            try:
                entry["deadlocks"] = sql.deadlocks(conn, since, edition, keep_xml)
            except Exception as e:  # noqa: BLE001
                session.unavailable.add("sql", f"{scope} deadlocks", e)
        contention[scope] = entry
    return contention, context


def capture_mongo(session: Session, stores: list[dict], full: bool, report_ids: list[str]) -> tuple[dict, dict, dict]:
    """(store sizes, run facts, context)."""
    members = stores_of_kind(stores, "mongo")
    if not members:
        return {}, {}, {}
    u = session.unavailable
    cfg = session.targets.get("mongo", {})
    try:
        client = session.mongo()
    except Exception as e:  # noqa: BLE001
        u.add("mongo", "connection", e)
        return {}, {}, {}
    out, facts, ctx = {}, {}, {}
    for s in members:
        loc = s["location"]
        db = client[cfg.get("databases", {}).get(loc["service"], loc["service"])]
        try:
            name = mongo.resolve_collection(db, loc["collection"])
            if name is None:
                u.add("mongo", s["id"], f"collection {loc['collection']} not found")
                continue
            out[s["id"]] = mongo.collection_size(db, name, u, "mongo") if full else \
                {"rows": int(db[name].estimated_document_count())}
            if full and report_ids and loc.get("report_key"):
                f = mongo.report_facts(db, name, loc["report_key"], report_ids)
                out[s["id"]]["by_report"] = f.pop("by_report")
                facts[s["id"]] = f
            if full and not ctx:
                ctx = mongo.context(client, db, cfg.get("connection_string", ""), u, "mongo")
        except Exception as e:  # noqa: BLE001
            u.add("mongo", s["id"], e)
    return out, facts, ctx


def capture_kafka(session: Session, stores: list[dict], full: bool) -> tuple[dict, dict, dict]:
    """(topics, consumer lag, context)."""
    if not stores_of_kind(stores, "kafka") or not session.targets.get("kafka", {}).get("bootstrap_servers"):
        return {}, {}, {}
    u = session.unavailable
    try:
        admin, consumer = session.kafka()
        meta = kafka.topic_metadata(admin)
    except Exception as e:  # noqa: BLE001
        u.add("kafka", "connection", e)
        return {}, {}, {}
    modeled = {t for s in stores_of_kind(stores, "kafka") for t in s["location"]["topics"]}
    topics, highs = {}, {}
    for name, m in sorted(meta.items()):
        try:
            marks = kafka.watermarks(consumer, name, m["partitions"])
        except Exception as e:  # noqa: BLE001
            u.add("kafka", f"{name} watermarks", e)
            continue
        highs.update({(name, p): h for p, (_, h) in marks.items()})
        entry = {"partitions": len(m["partitions"]), "replication": m["replication"],
                 **kafka.summarize_watermarks(marks)}
        if full and name in modeled:
            try:
                entry.update(kafka.sample_sizes(consumer, name, marks, SAMPLE_PER_PARTITION))
            except Exception as e:  # noqa: BLE001
                u.add("kafka", f"{name} message sizes", e)
        topics[name] = entry
    if full:
        try:
            for name, ms in kafka.retention_ms(admin, sorted(topics)).items():
                topics[name]["retention_ms"] = ms
        except Exception as e:  # noqa: BLE001
            u.add("kafka", "retention.ms", e)
    try:
        lag = kafka.consumer_lag(admin, highs)
    except Exception as e:  # noqa: BLE001
        u.add("kafka", "consumer lag", e)
        lag = {}
    for s in stores_of_kind(stores, "kafka"):
        missing = [t for t in s["location"]["topics"] if t not in topics]
        if missing:
            u.add("kafka", s["id"], f"topics not found: {', '.join(missing)}")
    context = {"topics": len(topics), "partitions": sum(t["partitions"] for t in topics.values())} if full else {}
    return topics, lag, context


def capture_loki(session: Session, stores: list[dict], since, until) -> dict:
    members = stores_of_kind(stores, "loki")
    cfg = session.targets.get("loki", {})
    if not members or not cfg.get("url") or since is None:
        return {}
    out = {}
    for s in members:
        loc = s["location"]
        try:
            out[s["id"]] = loki.bytes_by_group(cfg, cfg.get("selector", loc["selector"]), loc["group_by"], since, until)
        except Exception as e:  # noqa: BLE001
            session.unavailable.add("loki", s["id"], e)
    return out


def capture_cloud(session: Session, since, until) -> tuple[dict, dict | None]:
    """(Cosmos metrics, replicas)."""
    cosmos_metrics: dict = {}
    rid = session.targets.get("cosmos", {}).get("resource_id")
    if rid and since is not None:
        try:
            cosmos_metrics = cloud.cosmos_metrics(rid, since, until)
        except Exception as e:  # noqa: BLE001
            session.unavailable.add("cloud", "Cosmos RU and 429 metrics", e)
    elif since is not None:
        session.unavailable.add("cloud", "Cosmos RU and 429 metrics", "no cosmos.resource_id configured")
    replicas = None
    try:
        replicas = cloud.replicas(session.targets.get("replicas", {}))
    except Exception as e:  # noqa: BLE001
        session.unavailable.add("cloud", "replicas", e)
    return cosmos_metrics, replicas


# --------------------------------------------------------------------------------------------
# Snapshot
# --------------------------------------------------------------------------------------------

def take(session: Session, stores: list[dict], profile: str, label: str | None, since=None,
         report_ids: list[str] | None = None, keep_deadlock_xml: bool = False, code_version: str | None = None) -> dict:
    full = profile == "full"
    session.unavailable.items = []
    taken = utc_now()
    snap: dict = {"schema": SCHEMA_VERSION, "taken_at": iso(taken), "label": label,
                  "environment": session.targets.get("environment"), "code_version": code_version,
                  "profile": profile, "since": iso(since)}
    run = capture_run(session, since, report_ids) if full else None
    ids = [r["id"] for r in run["reports"]] if run else []
    snap["stores"] = capture_sql(session, stores, full, ids)
    sql_services = sorted({s["location"]["service"] for s in stores_of_kind(stores, "sql")})
    contention, sql_context = capture_sql_servers(session, sql_services, full, since, keep_deadlock_xml)
    m_sizes, m_facts, m_context = capture_mongo(session, stores, full, ids)
    snap["stores"].update(m_sizes)
    if run is not None:
        reportable = next(iter(m_facts.values()), None)
        run["reportable"] = reportable
        snap["run"] = run
    snap["kafka"], snap["consumer_lag"], k_context = capture_kafka(session, stores, full)
    if full:
        snap["loki"] = capture_loki(session, stores, since, taken)
        cosmos_metrics, replicas = capture_cloud(session, since, taken)
        snap["contention"] = {"sql": contention, "cosmos": cosmos_metrics}
        snap["context"] = {"sql": sql_context, "mongo": m_context, "kafka": k_context, "replicas": replicas}
    else:
        snap["contention"] = {"sql": contention}
    snap["unavailable"] = list(session.unavailable.items)
    return snap


def split_deadlock_xml(snap: dict) -> list[str]:
    """Moves raw deadlock XML out of the snapshot, so it only ever lands in its own file."""
    raw = []
    for entry in snap.get("contention", {}).get("sql", {}).values():
        raw += entry.get("deadlocks", {}).pop("xml", [])
    return raw


# --------------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------------

def parse_args(argv):
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    targets_mod.add_arguments(p)
    p.add_argument("--stores", type=Path, default=DEFAULT_STORES)
    p.add_argument("--profile", choices=("full", "light"), help="default: full, or light with --interval")
    p.add_argument("--label", help="e.g. before, after")
    p.add_argument("--code-version", help="recorded in the snapshot, e.g. the deployed release")
    when = p.add_mutually_exclusive_group()
    when.add_argument("--since", help="ISO time the run started; selects run reports and bounds windows")
    when.add_argument("--previous", type=Path, help="a snapshot whose taken_at is used as --since")
    p.add_argument("--report-ids", type=Path, help="file of run report IDs, one per line (instead of --since)")
    p.add_argument("--interval", type=float, help="seconds between snapshots; writes a JSON-lines series")
    stop = p.add_mutually_exclusive_group()
    stop.add_argument("--count", type=int, help="number of snapshots in the series")
    stop.add_argument("--until", help="ISO time to stop the series")
    p.add_argument("--include-deadlock-xml", action="store_true",
                   help="also write raw deadlock XML to <out>.deadlocks.json (it can hold literal parameter values)")
    p.add_argument("--out", type=Path, required=True)
    return p.parse_args(argv)


def main(argv=None) -> int:
    args = parse_args(argv)
    try:
        targets = targets_mod.resolve(args)
    except ValueError as e:
        print(f"error: {e}", file=sys.stderr)
        return 2
    stores = read_json(args.stores)["stores"]
    since = parse_iso(args.since) if args.since else parse_iso(read_json(args.previous)["taken_at"]) \
        if args.previous else None
    report_ids = [normalize_guid(line) for line in args.report_ids.read_text(encoding="utf-8").split()] \
        if args.report_ids else None
    session = Session(targets, Unavailable())
    try:
        if args.interval:
            return series(session, stores, args, since)
        snap = take(session, stores, args.profile or "full", args.label, since, report_ids,
                    args.include_deadlock_xml, args.code_version)
        raw = split_deadlock_xml(snap)
        write_json(args.out, snap)
        if args.include_deadlock_xml:
            write_json(args.out.with_suffix(".deadlocks.json"), raw)
        report(snap, args.out)
        return 0
    finally:
        session.close()


def series(session: Session, stores: list[dict], args, since) -> int:
    until = parse_iso(args.until) if args.until else None
    remaining = args.count
    taken = 0
    print(f"writing series to {args.out} every {args.interval:g}s; Ctrl+C to stop", file=sys.stderr)
    try:
        with open(args.out, "a", encoding="utf-8", newline="\n") as f:
            while True:
                started = time.monotonic()
                snap = take(session, stores, args.profile or "light", args.label, since,
                            code_version=args.code_version)
                f.write(json.dumps(snap, ensure_ascii=False) + "\n")
                f.flush()
                taken += 1
                if remaining is not None and taken >= remaining:
                    break
                if until is not None and utc_now() >= until:
                    break
                time.sleep(max(0.0, args.interval - (time.monotonic() - started)))
    except KeyboardInterrupt:
        pass
    print(f"{taken} snapshots written", file=sys.stderr)
    return 0


def report(snap: dict, out: Path) -> None:
    run = snap.get("run") or {}
    print(f"{snap['profile']} snapshot written to {out}: {len(snap['stores'])} stores, "
          f"{len(snap.get('kafka', {}))} topics, {len(run.get('reports', []))} run reports, "
          f"{len(snap['unavailable'])} unavailable items", file=sys.stderr)
    for item in snap["unavailable"]:
        print(f"  unavailable: {item['source']} / {item['item']}: {item['reason']}", file=sys.stderr)


if __name__ == "__main__":
    sys.exit(main())
