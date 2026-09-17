"""Reconcile app-config.yaml against the code inventory and the environment stores.

Three descriptions of the platform's configuration exist and none has ever been checked
against the others:

    app-config.yaml            what should be provisioned per environment  (intent)
    config-key-inventory.json  what the code actually reads                (reality)
    app-config.*.json          what each store actually holds              (deployed)

The first two are in this repository; the exports are in the private `link-cac` repository,
found via config_key_matching.default_config_dir.

This joins all three and reports where they disagree. Nothing is changed; the output is the
worklist for editing the catalog by hand, because most of the calls are judgement rather than
mechanism.

Buckets:

  A. Read by code, not in the catalog. Candidates for addition. Expect this to be large and
     mostly noise - the catalog is deliberately curated, not exhaustive. Add a key only if it
     is provisioned per environment, has no safe default, or is a documented operational knob.
     Everything else belongs in docs/config-key-inventory.md, which already lists it.

  B. In the catalog, read by no code. Dead entries: either the key was renamed and the catalog
     not updated, or the feature went away.

  C. In a store, but neither catalogued nor read. Dead store rows - config someone provisioned
     for code that no longer wants it.

  D. Required but absent from at least one store. Under the rule that `required: true` means a
     row must exist, each of these needs an explicit call: provision the row, or downgrade the
     entry to `required: false` because the shipped default is the right answer. The report
     shows whether a default exists in appsettings.json or application.yml, which is the fact
     that decision turns on.

Usage:
    python Scripts/AzureAppConfig/reconcile_config_catalog.py
    python Scripts/AzureAppConfig/reconcile_config_catalog.py --bucket D
"""

import argparse
import glob
import json
import os
import sys
from typing import Any, Dict, List, Optional, Set, Tuple

import yaml

import config_findings as findings_mod
import config_key_matching as matching

DEFAULT_CATALOG = "app-config.yaml"
DEFAULT_INVENTORY = "Scripts/AzureAppConfig/config-key-inventory.json"

# Framework-owned schemas, on both sides. YARP binds ReverseProxy:Routes:<name>:* and Serilog
# binds Serilog:WriteTo:<index>:* from their own schemas; on the Java side Spring Boot's
# auto-configuration owns spring.*, management.*, springdoc.*, server.* and logging.*. None of
# these is declared by Link's code, so no analysis of it can produce them. Reporting them as
# "read by nobody" would be false and would bury the real findings.
FRAMEWORK_PREFIXES = (
    "ReverseProxy:", "Serilog:", "Logging:LogLevel", "AllowedHosts",
    "spring.", "springdoc.", "management.", "server.", "logging.",
    "/spring/", "/springdoc/", "/management/", "/server/", "/logging/",
)


INVENTORY_HINT = ("Generate it first:\n"
                  "    dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet "
                  "Scripts/AzureAppConfig/config_symbols.json\n"
                  "    python Scripts/AzureAppConfig/extract_config_keys.py")


def load_inventory(path: str) -> Dict[str, Dict[str, Any]]:
    payload = findings_mod.load_json_file(path, hint=INVENTORY_HINT)
    return {entry["key"]: entry for entry in payload.get("keys", [])}


def shipped_defaults() -> Dict[str, Tuple[str, Any]]:
    """Every setting with a value baked into appsettings.json or application.yml.

    This is the fact bucket D turns on: a required key absent from every store is only a real
    problem if nothing supplies it in code.
    """
    defaults: Dict[str, Tuple[str, Any]] = {}

    def walk(node: Any, prefix: str, separator: str, source: str) -> None:
        if isinstance(node, dict):
            for name, child in node.items():
                key = f"{prefix}{separator}{name}" if prefix else name
                if isinstance(child, dict):
                    walk(child, key, separator, source)
                else:
                    defaults.setdefault(key, (source, child))

    for path in glob.glob(os.path.join("DotNet", "*", "appsettings.json")):
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                walk(json.load(handle), "", ":", os.path.basename(os.path.dirname(path)))
        except (OSError, json.JSONDecodeError):
            continue

    for path in glob.glob(os.path.join("Java", "*", "src", "main", "resources", "application.yml")):
        try:
            module = path.replace("\\", "/").split("/")[1]
            walk(findings_mod.load_yaml_file(path), "", ".", module)
        except (OSError, yaml.YAMLError):
            continue

    return defaults


def inventory_covers(store_key: str, store_value: str, content_type: str,
                     inventory_forms: Set[str]) -> bool:
    """Whether some inventory entry accounts for this store key.

    Compares on index-normalised forms, so a store row for `CORS:AllowedHeaders:4` is covered
    by the inventory's representative `CORS:AllowedHeaders:0`. A JSON blob is covered when its
    flattened children are, since those children are what services actually read - the parent
    key itself is never bound to anything.
    """
    forms = {store_key}
    if store_key.startswith("/"):
        forms.add(matching.slash_to_dotted(store_key))
    if any(canonical(f) in inventory_forms for f in forms):
        return True

    if matching.is_json_blob(content_type):
        children = matching.flatten_blob(store_key, store_value)
        if children and all(canonical(c) in inventory_forms for c in children):
            return True

    return False


def canonical(key: str) -> str:
    """Index-normalised and relaxed-binding form, for set comparison."""
    return matching.relax(matching.normalize_indices(key))


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Reconcile the config catalog against code and stores.")
    parser.add_argument("--catalog", default=DEFAULT_CATALOG)
    parser.add_argument("--inventory", default=DEFAULT_INVENTORY)
    parser.add_argument("--config-dir", default=matching.default_config_dir(),
                        help="Where link-cac's exports are (default: %(default)s; "
                             "also settable with LINK_CAC_CONFIG_DIR)")
    parser.add_argument("--bucket", choices=["A", "B", "C", "D"],
                        help="Show only one bucket")
    args = parser.parse_args()

    catalog = findings_mod.load_yaml_file(args.catalog)
    environments = matching.environment_names(catalog)
    inventory = load_inventory(args.inventory)
    defaults = shipped_defaults()

    stores: Dict[str, Dict[str, Set[str]]] = {}
    raw_keys: Dict[str, Set[str]] = {}
    all_items: Dict[str, Tuple[str, str]] = {}
    for env in environments:
        path = os.path.join(args.config_dir, f"app-config.{env}.json")
        items = findings_mod.load_json_file(
            path, hint=matching.missing_export_hint(
                catalog, env, args.catalog, args.config_dir)).get("items", [])
        stores[env] = matching.build_store_index(items)
        raw_keys[env] = {i["key"] for i in items if isinstance(i.get("key"), str)}
        for item in items:
            if isinstance(item.get("key"), str):
                all_items.setdefault(
                    item["key"], (item.get("value") or "", item.get("content_type") or ""))

    entries = matching.catalog_entries(catalog)
    catalog_keys = {entry["key"] for _, entry, _, _ in entries}

    show = lambda letter: args.bucket is None or args.bucket == letter  # noqa: E731

    # ---- A: read by code, not catalogued -------------------------------------------------
    if show("A"):
        missing = sorted(k for k in inventory if k not in catalog_keys)
        provisioned = [k for k in missing
                       if any(matching.resolve(k, inventory[k].get("runtime", "dotnet"), s)
                              for s in stores.values())]
        print(f"=== A. Read by code, not in the catalog: {len(missing)} "
              f"({len(provisioned)} of them provisioned in at least one store) ===")
        print("    Provisioned ones are the real candidates: something already sets them "
              "per environment.")
        for key in provisioned:
            entry = inventory[key]
            envs = [e for e, s in stores.items()
                    if matching.resolve(key, entry.get("runtime", "dotnet"), s)]
            print(f"    {key}")
            print(f"        stores: {', '.join(envs)}   consumers: "
                  f"{', '.join(entry['consumers']) or '-'}")
        print(f"    ({len(missing) - len(provisioned)} more are read but provisioned nowhere; "
              f"see docs/config-key-inventory.md)")
        print()

    # ---- B: catalogued, read by nobody --------------------------------------------------
    if show("B"):
        dead = []
        # The inventory shaped as a store index, so resolve() can apply its notation rules -
        # Java slash form, array elements, {Placeholder} templates - to it. Every key is given
        # the empty label because the inventory records what the code reads, which carries no
        # label. Built once: it does not vary across entries.
        inventory_index = {k: {""} for k in inventory}
        for section, entry, runtime, _ in entries:
            key = entry["key"]
            if key in inventory or key.startswith(FRAMEWORK_PREFIXES):
                continue
            if matching.resolve(key, runtime, inventory_index):
                continue
            dead.append((section, key, runtime))
        print(f"=== B. In the catalog, read by no code: {len(dead)} ===")
        for section, key, runtime in dead:
            envs = [e for e, s in stores.items() if matching.resolve(key, runtime, s)]
            note = f"still in stores: {', '.join(envs)}" if envs else "not in any store"
            print(f"    [{section}] {key}   ({note})")
        print()

    # ---- C: in a store, neither catalogued nor read -------------------------------------
    if show("C"):
        inventory_forms = {canonical(k) for k in inventory}
        catalog_forms = {canonical(k) for k in catalog_keys}
        orphans = []
        for key in sorted(all_items):
            if key.startswith(".appconfig") or key.startswith(FRAMEWORK_PREFIXES):
                continue
            dotted = matching.slash_to_dotted(key) if key.startswith("/") else key
            if canonical(dotted) in catalog_forms:
                continue
            value, content_type = all_items[key]
            if inventory_covers(key, value, content_type, inventory_forms):
                continue
            orphans.append(key)
        print(f"=== C. In a store, neither catalogued nor read: {len(orphans)} ===")
        for key in orphans:
            envs = [e for e in environments if key in raw_keys[e]]
            print(f"    {key}   ({', '.join(envs)})")
        print()

    # ---- D: required but absent ---------------------------------------------------------
    if show("D"):
        gaps = []
        for section, entry, runtime, label in entries:
            if not entry.get("required"):
                continue
            key = entry["key"]
            absent = [e for e in environments
                      if not matching.is_satisfied(key, runtime, stores[e], label)]
            if absent:
                gaps.append((section, key, runtime, absent, defaults.get(key)))
        print(f"=== D. required: true but absent from at least one store: {len(gaps)} ===")
        print("    Each needs a call: provision the row, or set required: false because the")
        print("    shipped default is the right answer.")
        print()
        with_default = [g for g in gaps if g[4]]
        without = [g for g in gaps if not g[4]]
        print(f"    -- has a shipped default ({len(with_default)}): "
              f"downgrading is usually correct --")
        for section, key, _, absent, default in with_default:
            source, value = default
            print(f"    [{section}] {key}")
            print(f"        absent from: {', '.join(absent)}   default: {value!r} ({source})")
        print()
        print(f"    -- no default anywhere ({len(without)}): provision, or the service "
              f"relies on nothing --")
        for section, key, _, absent, _ in without:
            print(f"    [{section}] {key}")
            print(f"        absent from: {', '.join(absent)}")
        print()

    return 0


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    sys.exit(main())
