"""Loki collector: bytes ingested per service over a window, from one LogQL instant query."""

from __future__ import annotations

from datetime import datetime


def query_text(selector: str, group_by: str, window_seconds: int) -> str:
    return f"sum by ({group_by}) (bytes_over_time({selector}[{max(1, window_seconds)}s]))"


def parse_vector(payload: dict, group_by: str) -> dict[str, float]:
    """{label value: bytes} from a Loki instant-query vector response."""
    if payload.get("status") != "success":
        raise ValueError(f"Loki query failed: {payload.get('error') or payload.get('status')}")
    result = payload.get("data", {}).get("result", [])
    return {r.get("metric", {}).get(group_by, "unlabelled"): float(r["value"][1]) for r in result}


def bytes_by_group(loki: dict, selector: str, group_by: str, since: datetime, until: datetime,
                   http_get=None) -> dict:
    window = int((until - since).total_seconds())
    headers = {}
    if loki.get("org_id"):
        headers["X-Scope-OrgID"] = loki["org_id"]
    if loki.get("bearer_token"):
        headers["Authorization"] = f"Bearer {loki['bearer_token']}"
    if http_get is None:
        import requests  # noqa: PLC0415 - optional dependency
        http_get = requests.get
    response = http_get(f"{loki['url'].rstrip('/')}/loki/api/v1/query",
                        params={"query": query_text(selector, group_by, window), "time": until.timestamp()},
                        headers=headers, timeout=60)
    response.raise_for_status()
    return {"window_s": window, "bytes_by_component": parse_vector(response.json(), group_by)}
