"""SQL Server and Azure SQL collector: table sizes, per-report attribution, run facts, contention and context.

Everything here takes a DB-API connection (pyodbc in practice) and only reads. Report IDs are validated as GUIDs
before they appear in a query, and table, key and join text come from stores.json, which `location_errors`
restricts to plain identifiers.
"""

from __future__ import annotations

import xml.etree.ElementTree as ET
from datetime import datetime, timezone

from .common import cadence_of, chunks, iso, normalize_guid

AZURE_SQL_DATABASE = 5  # SERVERPROPERTY('EngineEdition')
CHUNK = 500

TABLE_SIZES = """
SELECT s.name AS schema_name, t.name AS table_name,
       SUM(CASE WHEN ps.index_id IN (0, 1) THEN ps.row_count ELSE 0 END) AS row_count,
       SUM(CASE WHEN ps.index_id IN (0, 1) THEN ps.used_page_count ELSE 0 END) * 8192 AS data_bytes,
       SUM(CASE WHEN ps.index_id > 1 THEN ps.used_page_count ELSE 0 END) * 8192 AS index_bytes
FROM sys.dm_db_partition_stats ps
JOIN sys.tables t ON t.object_id = ps.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
GROUP BY s.name, t.name
"""

SERVER_INFO = """
SELECT CAST(SERVERPROPERTY('EngineEdition') AS int),
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
       CAST(SERVERPROPERTY('Edition') AS nvarchar(128)),
       CAST(DATABASEPROPERTYEX(DB_NAME(), 'Edition') AS nvarchar(128)),
       CAST(DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective') AS nvarchar(128)),
       DB_NAME()
"""

WAITS_SERVER = ("SELECT wait_type, waiting_tasks_count, wait_time_ms, signal_wait_time_ms "
                "FROM sys.dm_os_wait_stats WHERE wait_time_ms > 0")
WAITS_DATABASE = ("SELECT wait_type, waiting_tasks_count, wait_time_ms, signal_wait_time_ms "
                  "FROM sys.dm_db_wait_stats WHERE wait_time_ms > 0")

BLOCKING = """
SELECT r.wait_type, r.wait_time, DB_NAME(r.database_id)
FROM sys.dm_exec_requests r
WHERE r.blocking_session_id <> 0
"""

DEADLOCKS_SYSTEM_HEALTH = """
SELECT CAST(event_data AS nvarchar(max)), timestamp_utc
FROM sys.fn_xe_file_target_read_file('system_health*.xel', NULL, NULL, NULL)
WHERE object_name = 'xml_deadlock_report' AND timestamp_utc >= ?
"""

DEADLOCKS_AZURE = """
SELECT CAST(event_data AS nvarchar(max)), timestamp_utc
FROM sys.fn_xe_telemetry_blob_target_read_file('dl', NULL, NULL, NULL)
WHERE object_name = 'database_xml_deadlock_report' AND timestamp_utc >= ?
"""


def _rows(conn, sql: str, params: tuple = ()) -> list:
    cur = conn.cursor()
    try:
        cur.execute(sql, params) if params else cur.execute(sql)
        return cur.fetchall()
    finally:
        cur.close()


def _naive_utc(dt: datetime) -> datetime:
    """datetime2 columns hold UTC without an offset."""
    return dt if dt.tzinfo is None else dt.astimezone(timezone.utc).replace(tzinfo=None)


def _in_list(ids: list[str]) -> str:
    return ", ".join(f"'{normalize_guid(i)}'" for i in ids)


# --------------------------------------------------------------------------------------------
# Sizes and attribution
# --------------------------------------------------------------------------------------------

def table_sizes(conn) -> dict[str, dict]:
    """{'schema.table' (lower-case): {rows, data_bytes, index_bytes}} for every user table in the database."""
    return {f"{r[0]}.{r[1]}".lower(): {"rows": int(r[2]), "data_bytes": int(r[3]), "index_bytes": int(r[4])}
            for r in _rows(conn, TABLE_SIZES)}


def store_size(sizes: dict[str, dict], location: dict) -> dict | None:
    """Rows and bytes for one store, summing a multi-table location. None if any table is missing."""
    tables = location.get("tables") or [location["table"]]
    found = [sizes.get(t.lower()) for t in tables]
    if any(f is None for f in found):
        return None
    return {k: sum(f[k] for f in found) for k in ("rows", "data_bytes", "index_bytes")}


def attribution_counts(conn, location: dict, report_ids: list[str]) -> dict[str, int]:
    """Rows per report for the given reports, keyed by lower-case report ID."""
    attr = location["attribution"]
    key = attr["key"]
    source = attr.get("from") or f"{location['table']} t"
    counts: dict[str, int] = {}
    for part in chunks(sorted(set(report_ids)), CHUNK):
        sql = (f"SELECT CAST({key} AS nvarchar(64)), COUNT_BIG(*) FROM {source} "
               f"WHERE {key} IN ({_in_list(part)}) GROUP BY {key}")
        for rid, n in _rows(conn, sql):
            counts[normalize_guid(rid)] = counts.get(normalize_guid(rid), 0) + int(n)
    return counts


def phase_counts(conn, location: dict, report_ids: list[str]) -> dict[str, int]:
    """The run reports' rows split by acquisition phase (`Initial` / `Supplemental`), for stores whose
    attribution names a `phase` column. It separates a two-pass store's per-entry and per-reportable terms."""
    attr = location["attribution"]
    key, phase = attr["key"], attr["phase"]
    source = attr.get("from") or f"{location['table']} t"
    counts: dict[str, int] = {}
    for part in chunks(sorted(set(report_ids)), CHUNK):
        sql = (f"SELECT CAST({phase} AS nvarchar(64)), COUNT_BIG(*) FROM {source} "
               f"WHERE {key} IN ({_in_list(part)}) GROUP BY {phase}")
        for name, n in _rows(conn, sql):
            counts[name or "unknown"] = counts.get(name or "unknown", 0) + int(n)
    return counts


# --------------------------------------------------------------------------------------------
# Run facts
# --------------------------------------------------------------------------------------------

# pyodbc can't read datetimeoffset (ODBC type -155), so offsets are normalised to UTC datetime2 in the query.
RUN_REPORT_COLUMNS = ("Id, FacilityId, Frequency, "
                      "CAST(SWITCHOFFSET(ReportStartDate, '+00:00') AS datetime2), "
                      "CAST(SWITCHOFFSET(ReportEndDate, '+00:00') AS datetime2), Status, CreateDate, "
                      "CAST(SWITCHOFFSET(CompletedDate, '+00:00') AS datetime2)")


def run_reports(conn, since: datetime | None = None, report_ids: list[str] | None = None) -> list[dict]:
    """The run's reports: those listed, or those created since `since`."""
    if report_ids:
        rows = []
        for part in chunks(sorted(set(report_ids)), CHUNK):
            rows += _rows(conn, f"SELECT {RUN_REPORT_COLUMNS} FROM dbo.ReportSchedule WHERE Id IN ({_in_list(part)})")
    elif since is not None:
        rows = _rows(conn, f"SELECT {RUN_REPORT_COLUMNS} FROM dbo.ReportSchedule WHERE CreateDate >= ?",
                     (_naive_utc(since),))
    else:
        return []
    reports = []
    for r in rows:
        start, end, completed = r[3], r[4], r[7]
        lag = (completed - end).total_seconds() / 86_400 if completed is not None and end is not None else None
        reports.append({"id": normalize_guid(r[0]), "facility_id": r[1], "frequency": r[2],
                        "cadence": cadence_of(r[2]), "start": iso(start), "end": iso(end), "status": r[5],
                        "created": iso(r[6]), "completed": iso(completed), "completion_lag_days": lag})
    return reports


def entries_by_report(conn, report_ids: list[str]) -> dict[str, int]:
    return attribution_counts(conn, {"table": "dbo.ReportEntry", "attribution": {"key": "ReportScheduleId"}},
                              report_ids) if report_ids else {}


def census_patients(conn, since: datetime) -> dict:
    """Encounters and distinct (facility, MRN) patients created in Census since `since`."""
    r = _rows(conn, "SELECT COUNT_BIG(*), COUNT_BIG(DISTINCT CONCAT(FacilityId, N'|', MedicalRecordNumber)) "
                    "FROM dbo.PatientEncounters WHERE CreateDate >= ?", (_naive_utc(since),))[0]
    return {"encounters": int(r[0]), "patients": int(r[1])}


def facility_count(conn) -> int:
    return int(_rows(conn, "SELECT COUNT_BIG(*) FROM dbo.Facilities")[0][0])


# --------------------------------------------------------------------------------------------
# Context and contention (subtask C)
# --------------------------------------------------------------------------------------------

def server_info(conn) -> dict:
    r = _rows(conn, SERVER_INFO)[0]
    return {"engine_edition": r[0], "product_version": r[1], "server_edition": r[2], "database_edition": r[3],
            "service_objective": r[4], "database": r[5]}


def wait_stats(conn, engine_edition: int | None) -> dict[str, dict]:
    sql = WAITS_DATABASE if engine_edition == AZURE_SQL_DATABASE else WAITS_SERVER
    return {r[0]: {"tasks": int(r[1]), "wait_ms": int(r[2]), "signal_ms": int(r[3])} for r in _rows(conn, sql)}


def blocking(conn) -> dict:
    """A point-in-time sample of blocked requests: counts and waits only, never SQL text."""
    rows = _rows(conn, BLOCKING)
    by_type: dict[str, int] = {}
    by_db: dict[str, int] = {}
    for wait_type, _, db in rows:
        by_type[wait_type or "unknown"] = by_type.get(wait_type or "unknown", 0) + 1
        by_db[db or "unknown"] = by_db.get(db or "unknown", 0) + 1
    return {"blocked": len(rows), "max_wait_ms": max((int(r[1] or 0) for r in rows), default=0),
            "by_wait_type": by_type, "by_database": by_db}


def parse_deadlock(xml_text: str) -> dict:
    """Databases, objects and victim count from one deadlock report. Input buffers are never kept."""
    root = ET.fromstring(xml_text)
    dbs = sorted({p.get("currentdbname") for p in root.iter("process") if p.get("currentdbname")})
    objects = sorted({el.get("objectname") for el in root.iter() if el.get("objectname")})
    victims = len(list(root.iter("victimProcess")))
    return {"databases": dbs, "objects": objects, "victims": victims}


def deadlocks(conn, since: datetime, engine_edition: int | None, keep_xml: bool = False) -> dict:
    sql = DEADLOCKS_AZURE if engine_edition == AZURE_SQL_DATABASE else DEADLOCKS_SYSTEM_HEALTH
    events, raw = [], []
    for xml_text, ts in _rows(conn, sql, (_naive_utc(since),)):
        try:
            events.append({"time": iso(ts), **parse_deadlock(xml_text)})
        except ET.ParseError:
            events.append({"time": iso(ts), "databases": [], "objects": [], "victims": 0, "unparsed": True})
        if keep_xml:
            raw.append(xml_text)
    result = {"count": len(events), "events": events}
    if keep_xml:
        result["xml"] = raw
    return result
