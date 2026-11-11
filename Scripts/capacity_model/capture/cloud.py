"""Optional environment context from CLIs: Cosmos DB RU and throttling (az), and service replicas (docker/kubectl).

Each piece runs only when the targets file configures it, and every command is read-only. `run` is injectable so
the parsers can be tested against recorded output.
"""

from __future__ import annotations

import json
import shutil
import subprocess
from datetime import datetime

from .common import iso


def run_cli(args: list[str], timeout: int = 120) -> str:
    exe = shutil.which(args[0])
    if exe is None:
        raise RuntimeError(f"{args[0]} is not on PATH")
    done = subprocess.run([exe, *args[1:]], capture_output=True, text=True, timeout=timeout, check=False)
    if done.returncode != 0:
        raise RuntimeError(f"{args[0]} exited {done.returncode}: {done.stderr.strip()[:300]}")
    return done.stdout


# --------------------------------------------------------------------------------------------
# Cosmos DB metrics (Azure Monitor)
# --------------------------------------------------------------------------------------------

def metric_points(payload: dict, field: str) -> list[float]:
    return [float(point[field]) for metric in payload.get("value", []) for series in metric.get("timeseries", [])
            for point in series.get("data", []) if point.get(field) is not None]


def cosmos_metrics(resource_id: str, since: datetime, until: datetime, run=run_cli) -> dict:
    """Total and peak-minute RU, and HTTP 429 responses, over [since, until]."""
    common = ["--resource", resource_id, "--interval", "PT1M", "--start-time", iso(since), "--end-time", iso(until),
              "--output", "json"]
    ru = json.loads(run(["az", "monitor", "metrics", "list", "--metric", "TotalRequestUnits",
                         "--aggregation", "Total", *common]))
    throttled = json.loads(run(["az", "monitor", "metrics", "list", "--metric", "TotalRequests",
                                "--aggregation", "Count", "--filter", "StatusCode eq '429'", *common]))
    ru_points = metric_points(ru, "total")
    t_points = metric_points(throttled, "count")
    return {"request_units": sum(ru_points), "peak_ru_per_minute": max(ru_points, default=0.0),
            "throttled_requests": int(sum(t_points)), "peak_throttled_per_minute": int(max(t_points, default=0))}


# --------------------------------------------------------------------------------------------
# Replicas
# --------------------------------------------------------------------------------------------

def docker_replicas(run=run_cli) -> dict[str, dict]:
    out = run(["docker", "ps", "--format", '{{.Label "com.docker.compose.service"}}'])
    counts: dict[str, int] = {}
    for line in out.splitlines():
        if line.strip():
            counts[line.strip()] = counts.get(line.strip(), 0) + 1
    return {name: {"running": n} for name, n in sorted(counts.items())}


def kubectl_replicas(namespace: str | None, context: str | None, run=run_cli) -> dict[str, dict]:
    scope = (["--namespace", namespace] if namespace else []) + (["--context", context] if context else [])
    deployments = json.loads(run(["kubectl", "get", "deployments", *scope, "--output", "json"]))
    result = {d["metadata"]["name"]: {"desired": d.get("spec", {}).get("replicas"),
                                      "ready": d.get("status", {}).get("readyReplicas", 0)}
              for d in deployments.get("items", [])}
    try:
        hpas = json.loads(run(["kubectl", "get", "hpa", *scope, "--output", "json"]))
    except RuntimeError:
        hpas = {"items": []}
    for h in hpas.get("items", []):
        target = h.get("spec", {}).get("scaleTargetRef", {}).get("name")
        if target in result:
            result[target]["hpa_min"] = h["spec"].get("minReplicas")
            result[target]["hpa_max"] = h["spec"].get("maxReplicas")
    return result


def replicas(config: dict, run=run_cli) -> dict[str, dict] | None:
    provider = (config or {}).get("provider", "none")
    if provider == "docker":
        return docker_replicas(run)
    if provider == "kubectl":
        return kubectl_replicas(config.get("namespace"), config.get("context"), run)
    return None
