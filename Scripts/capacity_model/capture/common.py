"""Helpers shared by the collectors, snapshot.py and diff_snapshots.py."""

from __future__ import annotations

import json
import re
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable, Iterator

CADENCES = {"daily": "daily", "monthly": "monthly"}  # ReportSchedule.Frequency text -> model cadence


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso(dt: datetime | None) -> str | None:
    if dt is None:
        return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_iso(text: str | None) -> datetime | None:
    if not text:
        return None
    dt = datetime.fromisoformat(text.replace("Z", "+00:00"))
    return dt if dt.tzinfo else dt.replace(tzinfo=timezone.utc)


def cadence_of(frequency: str | None) -> str:
    """Model cadence for a ReportSchedule.Frequency value; anything other than daily or monthly is `other`."""
    return CADENCES.get((frequency or "").strip().lower(), "other")


def normalize_guid(value: object) -> str:
    """Lower-case canonical GUID text. Raises ValueError for anything that isn't a GUID."""
    return str(uuid.UUID(str(value).strip("{}")))


def chunks(items: list, size: int) -> Iterator[list]:
    for i in range(0, len(items), size):
        yield items[i:i + size]


class Unavailable:
    """Collects what a snapshot couldn't capture, with the reason, so a snapshot never fails for one source."""

    def __init__(self) -> None:
        self.items: list[dict] = []

    def add(self, source: str, item: str, reason: object) -> None:
        self.items.append({"source": source, "item": item, "reason": str(reason).strip()[:500]})


def write_json(path: Path, data: object) -> None:
    """The canonical JSON layout for files under Scripts/capacity_model: two-space indent, LF, trailing newline."""
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def read_json(path: Path) -> object:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def read_series(path: Path) -> list[dict]:
    """A JSON-lines series written by `snapshot.py --interval`."""
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


# --------------------------------------------------------------------------------------------
# Store locations (the `location` block in stores.json)
# --------------------------------------------------------------------------------------------

_TABLE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*$")
_COLUMN = re.compile(r"^(?:[A-Za-z]\.)?[A-Za-z_][A-Za-z0-9_]*$")
_FROM = re.compile(r"^[A-Za-z0-9_ .=]+$")
_FIELD = re.compile(r"^[A-Za-z_][A-Za-z0-9_.]*$")


def location_errors(store: dict) -> list[str]:
    """Problems with one store's `location`; empty when it is usable by the collectors."""
    loc = store.get("location")
    sid = store.get("id", "?")
    if not isinstance(loc, dict):
        return [f"{sid}: missing location"]
    if "measured_with" in loc:
        return []
    kind = store.get("kind")
    errors = []
    if kind == "sql":
        tables = loc.get("tables") or ([loc["table"]] if "table" in loc else [])
        if not loc.get("service") or not tables:
            errors.append(f"{sid}: sql location needs service and table(s)")
        errors += [f"{sid}: bad table name {t!r}" for t in tables if not _TABLE.match(t)]
        attr = loc.get("attribution")
        if attr is not None:
            if not _COLUMN.match(attr.get("key", "")):
                errors.append(f"{sid}: bad attribution key {attr.get('key')!r}")
            if "phase" in attr and not _COLUMN.match(attr["phase"]):
                errors.append(f"{sid}: bad attribution phase column {attr['phase']!r}")
            if "from" in attr and not _FROM.match(attr["from"]):
                errors.append(f"{sid}: attribution 'from' has unexpected characters")
            if "from" in attr and "." in attr["key"] and not attr["from"].startswith(f"{loc.get('table')} t "):
                errors.append(f"{sid}: attribution 'from' must start with '<table> t'")
    elif kind == "mongo":
        if not loc.get("service") or not loc.get("collection"):
            errors.append(f"{sid}: mongo location needs service and collection")
        if loc.get("report_key") and not _FIELD.match(loc["report_key"]):
            errors.append(f"{sid}: bad report_key {loc['report_key']!r}")
    elif kind == "kafka":
        if not loc.get("topics"):
            errors.append(f"{sid}: kafka location needs topics")
    elif kind == "loki":
        if not loc.get("selector") or not loc.get("group_by"):
            errors.append(f"{sid}: loki location needs selector and group_by")
    else:
        errors.append(f"{sid}: unknown kind {kind!r}")
    return errors


def stores_of_kind(stores: Iterable[dict], kind: str) -> list[dict]:
    return [s for s in stores if s.get("kind") == kind and "measured_with" not in s.get("location", {})]
