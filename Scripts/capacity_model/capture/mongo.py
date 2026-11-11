"""MongoDB and Cosmos DB (Mongo API) collector.

Uses no aggregation pipeline at all, so nothing here depends on $collStats, $indexStats or the other stages
Cosmos DB for MongoDB (RU) restricts (AGENTS.md): counts come from count commands, sizes from the `collStats`
command where the server supports it, and run facts from a projected find.
"""

from __future__ import annotations

from .common import normalize_guid


def is_cosmos(connection_string: str) -> bool:
    return ".cosmos.azure.com" in (connection_string or "").lower()


def resolve_collection(db, name: str) -> str | None:
    """The actual collection name, matched case-insensitively."""
    wanted = name.lower()
    return next((c for c in db.list_collection_names() if c.lower() == wanted), None)


def collection_size(db, name: str, unavailable, source: str) -> dict:
    coll = db[name]
    result = {"rows": int(coll.estimated_document_count()), "data_bytes": None, "index_bytes": None}
    try:
        stats = db.command("collStats", name)
        result["data_bytes"] = int(stats["size"]) if "size" in stats else None
        result["index_bytes"] = int(stats["totalIndexSize"]) if "totalIndexSize" in stats else None
        if result["data_bytes"] is None or result["index_bytes"] is None:
            unavailable.add(source, f"{name} sizes", "collStats returned no size fields")
    except Exception as e:  # noqa: BLE001 - Cosmos or permissions; record and carry on
        unavailable.add(source, f"{name} sizes", f"collStats failed: {e}")
    return result


def report_facts(db, name: str, report_key: str, report_ids: list[str]) -> dict:
    """Documents per run report, and the reportable share across their evaluated report entries.

    `report_key` is a dotted path whose parent is an array of report entries (`reports.reportTrackingId`); each
    entry's `reportable` flag sits beside the key.
    """
    wanted = {normalize_guid(r) for r in report_ids}
    variants = sorted(wanted | {r.upper() for r in wanted})
    parent, _, leaf = report_key.rpartition(".")
    projection = {report_key: 1, f"{parent}.reportable": 1} if parent else {report_key: 1}
    by_report: dict[str, int] = {}
    evaluated = reportable = 0
    for doc in db[name].find({report_key: {"$in": variants}}, projection):
        entries = doc.get(parent, []) if parent else [doc]
        for entry in entries if isinstance(entries, list) else [entries]:
            try:
                rid = normalize_guid(entry.get(leaf))
            except (ValueError, TypeError, AttributeError):
                continue
            if rid not in wanted:
                continue
            by_report[rid] = by_report.get(rid, 0) + 1
            if entry.get("reportable") is not None:
                evaluated += 1
                reportable += 1 if entry.get("reportable") else 0
    return {"by_report": by_report, "evaluated": evaluated, "reportable": reportable,
            "share": reportable / evaluated if evaluated else None}


def context(client, db, connection_string: str, unavailable, source: str) -> dict:
    info = {"cosmos": is_cosmos(connection_string)}
    try:
        info["version"] = client.server_info().get("version")
    except Exception as e:  # noqa: BLE001
        unavailable.add(source, "server version", e)
    if info["cosmos"]:
        try:
            got = db.command({"customAction": "GetDatabase"})
            info["throughput"] = {k: got.get(k) for k in ("provisionedThroughput", "autoScaleSettings") if k in got}
        except Exception as e:  # noqa: BLE001
            unavailable.add(source, "Cosmos provisioned throughput", e)
    return info
