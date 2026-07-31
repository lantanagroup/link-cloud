"""Verify every required catalog key is present in every environment store.

`app-config.yaml` marks a key `required: true` to mean a row for it must exist in each App
Configuration store. Nothing enforced that, so the catalog accumulated entries no environment
satisfied and nobody noticed. This is the gate.

LEGLINK-775 will make an Azure pipeline import Config/app-config.*.json into the stores, at
which point these files stop being a record of deployed configuration and start being its
source. A missing required key then becomes a deployment defect rather than a documentation
one, which is why this runs on every PR.

Deciding whether a key is "present" is not a string comparison - Java keys are stored in slash
notation, arrays are stored element-by-element, some rows are JSON blobs the provider flattens,
and labels scope which service can see a row at all. Those rules live in config_key_matching so
this check and reconcile_config_catalog cannot drift apart in their answers.

Checks:

  ERROR  A `required: true` entry has no row in one or more stores. Under the catalog's own
         rule that is a gap; either provision the row or mark the entry not required.

  ERROR  A store row carries a label containing ':'. The <Service>:<Environment> label tier
         does not resolve - ExternalConfigurationExtension.cs:64 concatenates the environment
         object rather than its name - so such a row is fetched by nobody. No store uses one
         today; this keeps it that way.

  ERROR  A store row carries a label no service selects. Labels are compiled into the services,
         so a label absent from serviceMeta is dead weight that looks live.

  WARN   The Serilog sink index the catalog names disagrees with what a store or an
         appsettings.json declares. Serilog addresses sinks positionally, so reordering the
         WriteTo array in a service silently repoints Serilog:WriteTo:<n>:Args:uri.

Exit code is 0 when no errors are found (and, with --strict, no warnings either).

Usage:
    python Scripts/AzureAppConfig/check_required_config.py
    python Scripts/AzureAppConfig/check_required_config.py --strict
"""

import argparse
import glob
import json
import os
import re
import sys
from typing import Any, Dict, List, Optional, Set, Tuple

import yaml

import config_findings as findings_mod
import config_key_matching as matching
from config_findings import ERROR, WARN, Finding

DEFAULT_CATALOG = "app-config.yaml"
DEFAULT_CONFIG_DIR = "Config"
ENVIRONMENTS = ("dev", "qa", "test")

SERILOG_SINK_RE = re.compile(r"^Serilog:WriteTo:(\d+):")
GRAFANA_SINK = "GrafanaLoki"


def check_required_keys(catalog: Dict[str, Any],
                        indexes: Dict[str, Dict[str, Set[str]]]) -> List[Finding]:
    findings: List[Finding] = []
    for section, entry, runtime, label in matching.catalog_entries(catalog):
        if not entry.get("required"):
            continue
        key = entry["key"]
        absent = [env for env, index in indexes.items()
                  if not matching.is_satisfied(key, runtime, index, label)]
        if not absent:
            continue

        scope = f"label '{label}' or no label" if label else "any label"
        forms = " or ".join(matching.candidate_forms(key, runtime))
        findings.append(Finding(
            "ERROR", f"{section} -> {key}",
            f"required: true but absent from {', '.join(absent)}. "
            f"Looked for {forms} under {scope}. "
            f"Provision the row, or set required: false if the shipped default is correct."))
    return findings


def check_labels(catalog: Dict[str, Any],
                 stores: Dict[str, List[Dict[str, Any]]]) -> List[Finding]:
    findings: List[Finding] = []
    known = {info.get("label") for info in (catalog.get("serviceMeta") or {}).values()}
    seen: Dict[Tuple[str, str], List[str]] = {}

    for env, items in stores.items():
        for item in items:
            label = item.get("label")
            if not label:
                continue
            seen.setdefault((label, item.get("key", "")), []).append(env)

    reported_colon: Set[str] = set()
    reported_unknown: Set[str] = set()
    for (label, key), envs in sorted(seen.items()):
        if ":" in label and label not in reported_colon:
            reported_colon.add(label)
            findings.append(Finding(
                "ERROR", f"label '{label}'",
                f"Contains ':', so no service ever selects it. The <Service>:<Environment> "
                f"tier does not resolve because ExternalConfigurationExtension.cs:64 "
                f"concatenates the environment object rather than its name. "
                f"First seen on '{key}' in {', '.join(envs)}."))
        elif label not in known and label not in reported_unknown:
            reported_unknown.add(label)
            findings.append(Finding(
                "ERROR", f"label '{label}'",
                f"No service selects this label; it is absent from serviceMeta. Rows carrying "
                f"it are fetched by nobody. First seen on '{key}' in {', '.join(envs)}."))
    return findings


def check_serilog_sink_order(catalog: Dict[str, Any],
                             stores: Dict[str, List[Dict[str, Any]]]) -> List[Finding]:
    """Guard the positional assumption the catalog's Serilog keys depend on.

    Serilog's WriteTo is an array, so `Serilog:WriteTo:1:Args:uri` means "the second sink",
    not "the Loki sink". Every appsettings.json ships [GrafanaLoki, Console], the opposite
    order, and .NET merges arrays by index - so on the shipped files alone index 1 would be
    Console and the Loki uri would land on the wrong sink.

    What makes it correct today is that each store also pins Serilog:WriteTo:1:Name to
    GrafanaLoki, overriding the file. That pin is load-bearing and invisible: delete it and
    logging silently reverts to the file's ordering. So the check is not "do the file and the
    catalog agree" - they deliberately do not - but "is the pin still there".
    """
    findings: List[Finding] = []
    indices = {int(m.group(1))
               for _, entry, _, _ in matching.catalog_entries(catalog)
               if (m := SERILOG_SINK_RE.match(entry["key"]))}
    if not indices:
        return findings

    shipped_order: Dict[str, int] = {}
    for path in sorted(glob.glob(os.path.join("DotNet", "*", "appsettings.json"))):
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                write_to = (json.load(handle).get("Serilog") or {}).get("WriteTo")
        except (OSError, json.JSONDecodeError):
            continue
        if isinstance(write_to, list):
            sinks = [s.get("Name") for s in write_to if isinstance(s, dict)]
            if GRAFANA_SINK in sinks:
                shipped_order[path.replace("\\", "/")] = sinks.index(GRAFANA_SINK)

    for index in sorted(indices):
        name_key = f"Serilog:WriteTo:{index}:Name"
        for env, items in stores.items():
            names = {i.get("value") for i in items if i.get("key") == name_key}
            if GRAFANA_SINK in names:
                continue
            conflicting = sorted(p for p, i in shipped_order.items() if i != index)
            if not names:
                detail = (f"does not set {name_key}, so the sink at index {index} is whatever "
                          f"appsettings.json puts there.")
                if conflicting:
                    detail += (f" {len(conflicting)} service(s) ship {GRAFANA_SINK} at a "
                               f"different index, e.g. {conflicting[0]} - the Loki uri would "
                               f"be applied to the wrong sink.")
            else:
                detail = (f"sets {name_key} to {', '.join(sorted(str(n) for n in names))}, but "
                          f"the catalog's Serilog:WriteTo:{index}:Args:uri describes the Loki "
                          f"sink.")
            findings.append(Finding("WARN", f"{env} -> {name_key}",
                                    f"The catalog references Serilog:WriteTo:{index}:*, "
                                    f"but this store {detail}"))
    return findings


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Verify required catalog keys exist in every environment store.")
    parser.add_argument("--catalog", default=DEFAULT_CATALOG)
    parser.add_argument("--config-dir", default=DEFAULT_CONFIG_DIR)
    parser.add_argument("--environments", nargs="*", default=list(ENVIRONMENTS))
    parser.add_argument("--strict", action="store_true", help="Treat warnings as errors")
    args = parser.parse_args()

    catalog = findings_mod.load_yaml_file(args.catalog)
    stores: Dict[str, List[Dict[str, Any]]] = {}
    indexes: Dict[str, Dict[str, Set[str]]] = {}
    for env in args.environments:
        items = findings_mod.load_json_file(
            os.path.join(args.config_dir, f"app-config.{env}.json")).get("items", [])
        stores[env] = items
        indexes[env] = matching.build_store_index(items)

    findings = check_required_keys(catalog, indexes)
    findings.extend(check_labels(catalog, stores))
    findings.extend(check_serilog_sink_order(catalog, stores))

    required = sum(1 for _, entry, _, _ in matching.catalog_entries(catalog)
                   if entry.get("required"))
    return findings_mod.report(
        findings,
        headline=(f"Checked {required} required keys against {len(stores)} store(s): "
                  f"{', '.join(args.environments)}"),
        all_clear="OK: every required key is present in every store.",
        strict=args.strict)


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    sys.exit(main())
