"""Connection targets for one environment: a targets file, `.env` substitution and CLI overrides.

The argument names mirror Scripts/clean.py, and the `.env` / `${VAR}` handling and SQL connection logic are
clean.py's own (`load_env_vars`, `substitute_vars`, `get_mssql_connection`, `mask_conn_str`).
"""

from __future__ import annotations

import argparse
import copy
import json
import re
import sys
from pathlib import Path

SCRIPTS_DIR = Path(__file__).resolve().parents[2]
TARGETS_DIR = Path(__file__).resolve().parents[1] / "targets"
DEFAULT_TARGETS = TARGETS_DIR / "docker-compose.json"

# Internal service keys (stores.json `location.service`) -> clean.py's flag spelling.
SQL_SERVICES = {"report": "report", "census": "census", "dataacquisition": "data-acquisition", "audit": "audit",
                "querydispatch": "query-dispatch", "validation": "validation", "tenant": "tenant"}
MONGO_SERVICES = {"measureeval": "measureeval"}

_UNRESOLVED = re.compile(r"\$\{([^}]+)\}")


def _clean():
    if str(SCRIPTS_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPTS_DIR))
    import clean  # noqa: PLC0415 - Scripts/clean.py; its main() is guarded
    return clean


def add_arguments(p: argparse.ArgumentParser) -> None:
    g = p.add_argument_group("targets (override the targets file)")
    g.add_argument("--targets", type=Path, default=DEFAULT_TARGETS, help="targets file (default: %(default)s)")
    g.add_argument("--env-file", help="a .env file for ${VAR} substitution (default: .env in the current directory)")
    g.add_argument("--environment", help="environment name recorded in the snapshot")
    g.add_argument("--db-connection-string", help="server-level SQL Server connection string")
    for flag in SQL_SERVICES.values():
        g.add_argument(f"--{flag}-db-name")
        g.add_argument(f"--{flag}-connection-string")
    g.add_argument("--mongo-connection-string")
    g.add_argument("--measureeval-db-name")
    g.add_argument("--kafka-broker")
    g.add_argument("--kafka-username")
    g.add_argument("--kafka-password")
    g.add_argument("--kafka-security-protocol")
    g.add_argument("--kafka-sasl-mechanism")
    g.add_argument("--loki-url")
    g.add_argument("--loki-org-id", help="X-Scope-OrgID header for multi-tenant Loki")
    g.add_argument("--loki-bearer-token")
    g.add_argument("--cosmos-resource-id", help="Azure resource ID of the Cosmos DB account (for RU and 429 metrics)")
    g.add_argument("--replicas-provider", choices=("docker", "kubectl", "none"))
    g.add_argument("--kube-namespace")
    g.add_argument("--kube-context")


def _substitute(value, env: dict, sub):
    if isinstance(value, str):
        return sub(value, env)
    if isinstance(value, dict):
        return {k: _substitute(v, env, sub) for k, v in value.items()}
    if isinstance(value, list):
        return [_substitute(v, env, sub) for v in value]
    return value


def _unresolved(value, path="") -> list[str]:
    if isinstance(value, str):
        return [f"{path} needs ${{{name}}}" for name in _UNRESOLVED.findall(value)]
    if isinstance(value, dict):
        return [e for k, v in value.items() for e in _unresolved(v, f"{path}.{k}" if path else k)]
    if isinstance(value, list):
        return [e for i, v in enumerate(value) for e in _unresolved(v, f"{path}[{i}]")]
    return []


def resolve(args: argparse.Namespace) -> dict:
    """The targets file with CLI overrides applied and ${VAR} references substituted.

    Raises ValueError naming any variable that is still unresolved (never its value).
    """
    t = copy.deepcopy(json.loads(Path(args.targets).read_text(encoding="utf-8")))
    t.pop("_comment", None)
    sql, mongo = t.setdefault("sql", {}), t.setdefault("mongo", {})
    kafka, loki = t.setdefault("kafka", {}), t.setdefault("loki", {})
    sql.setdefault("databases", {})
    sql.setdefault("connection_strings", {})
    mongo.setdefault("databases", {})

    def override(section: dict, key: str, value) -> None:
        if value is not None:
            section[key] = value

    override(t, "environment", args.environment)
    override(sql, "connection_string", args.db_connection_string)
    for service, flag in SQL_SERVICES.items():
        attr = flag.replace("-", "_")
        override(sql["databases"], service, getattr(args, f"{attr}_db_name"))
        override(sql["connection_strings"], service, getattr(args, f"{attr}_connection_string"))
    override(mongo, "connection_string", args.mongo_connection_string)
    override(mongo["databases"], "measureeval", args.measureeval_db_name)
    override(kafka, "bootstrap_servers", args.kafka_broker)
    override(kafka, "username", args.kafka_username)
    override(kafka, "password", args.kafka_password)
    override(kafka, "security_protocol", args.kafka_security_protocol)
    override(kafka, "sasl_mechanism", args.kafka_sasl_mechanism)
    override(loki, "url", args.loki_url)
    override(loki, "org_id", args.loki_org_id)
    override(loki, "bearer_token", args.loki_bearer_token)
    override(t.setdefault("cosmos", {}), "resource_id", args.cosmos_resource_id)
    replicas = t.setdefault("replicas", {})
    override(replicas, "provider", args.replicas_provider)
    override(replicas, "namespace", args.kube_namespace)
    override(replicas, "context", args.kube_context)

    clean = _clean()
    env_file = args.env_file or (".env" if Path(".env").exists() else None)
    t = _substitute(t, clean.load_env_vars(env_file), clean.substitute_vars)
    problems = _unresolved(t)
    if problems:
        raise ValueError("unresolved variables in targets: " + "; ".join(problems))
    t.setdefault("environment", "unnamed")
    return t


def sql_connect(targets: dict, service: str, timeout: int = 10):
    """A pyodbc connection for one service's database, or None (clean.py logs the reason)."""
    sql = targets.get("sql", {})
    clean = _clean()
    if clean.pyodbc is None:
        raise RuntimeError("pyodbc is not installed (pip install -r Scripts/capacity_model/requirements-capture.txt)")
    own = sql.get("connection_strings", {}).get(service)
    db = sql.get("databases", {}).get(service)
    if not own and not (sql.get("connection_string") and db):
        return None
    return clean.get_mssql_connection(own, db, sql.get("connection_string"), timeout=timeout)


def sql_server_reachable(targets: dict, service: str, timeout: int = 10) -> bool:
    """After a database connection fails: does the server itself accept a connection (to its default database)?"""
    sql = targets.get("sql", {})
    if sql.get("connection_strings", {}).get(service) or not sql.get("connection_string"):
        return True  # can't tell from a service-specific string; don't skip anything on its account
    conn = _clean().get_mssql_connection(None, None, sql["connection_string"], timeout=timeout)
    if conn is None:
        return False
    conn.close()
    return True


def sql_server_key(targets: dict, service: str) -> str:
    """Identifies the server a service's database lives on, without credentials, for server-scoped counters."""
    sql = targets.get("sql", {})
    conn = sql.get("connection_strings", {}).get(service) or sql.get("connection_string") or ""
    m = re.search(r"(?:Server|Data Source|Address)\s*=\s*([^;]+)", conn, re.IGNORECASE)
    return m.group(1).strip().lower() if m else "unknown"


def mask(text: str) -> str:
    return _clean().mask_conn_str(text)
