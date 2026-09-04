"""Tag the App Configuration exports with the services that consume each row.

Knowing which services read a given key is currently archaeology: you grep the code, follow
the .csproj reference graph, and hope. Scripts/AzureAppConfig/extract_config_keys.py already computes it, so
this writes the answer onto the rows as a tag.

This edits the committed exports in the private `link-cac` repository only - it does not talk
to Azure. They are the source that LEGLINK-775's pipeline will import, so a change belongs
there first, where it is reviewable as a diff, and reaches a store only when that pipeline runs.
The diff lands in a different repository from this script; commit it there.

Why tags and not labels
-----------------------
Labels are a *selector*. Each service issues Select("*", null) then Select("*", <its label>),
so a labeled row overrides the unlabeled one for that service and is invisible to every other
service. Recording "Report and Submission read this" as labels would duplicate the value onto
a row per consumer - up to fifteen copies of one setting, each able to drift, and each
changing what some service actually resolves.

Tags are inert: neither provider selects on them, so adding one cannot change what any service
reads. That is what makes this a documentation change rather than a configuration change.

A labeled row is attributed to the single service that selects that label, not to every
consumer of the key. `ConnectionStrings:DatabaseConnection [Account]` is Account's override
and the other ten readers of that key never see it; tagging it with all eleven would state the
opposite of the resolution rules.

Usage:
    python Scripts/AzureAppConfig/apply_appconfig_tags.py --env dev            # show the plan
    python Scripts/AzureAppConfig/apply_appconfig_tags.py --env dev --write    # edit the export
    python Scripts/AzureAppConfig/apply_appconfig_tags.py --all --write
"""

import argparse
import json
import os
import sys
from typing import Any, Dict, List, Optional, Set, Tuple

import yaml

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import config_findings as findings_mod
import config_key_matching as matching  # noqa: E402

DEFAULT_CATALOG = "app-config.yaml"
DEFAULT_INVENTORY = "Scripts/AzureAppConfig/config-key-inventory.json"
CONSUMERS_TAG = "link:consumers"


INVENTORY_HINT = ("Generate it first:\n"
                  "    dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet "
                  "Scripts/AzureAppConfig/config_symbols.json\n"
                  "    python Scripts/AzureAppConfig/extract_config_keys.py")


def label_owners(catalog_path: str) -> Dict[str, str]:
    """Map each App Configuration label back to the service that selects it."""
    with open(catalog_path, "r", encoding="utf-8") as handle:
        meta = (yaml.safe_load(handle) or {}).get("serviceMeta") or {}
    return {info["label"]: name for name, info in meta.items()
            if isinstance(info, dict) and info.get("label")}


def consumers_for_row(item: Dict[str, Any], inventory: List[Dict[str, Any]],
                      owners: Dict[str, str]) -> Set[str]:
    """The services that read this specific row.

    Matching runs through the same rules the check and the reconciler use, so a row for
    `KafkaConnection:BootstrapServers:0` is attributed to the consumers of
    `KafkaConnection:BootstrapServers`, and `/spring/datasource/url` to those of
    `spring.datasource.url`.
    """
    index = matching.build_store_index([item])
    # The inventory records one representative element per array (CORS:AllowedHeaders:0), so
    # a row for CORS:AllowedHeaders:4 would not match it. Index-normalise both sides, or the
    # tail of every array ends up untagged while its first element is tagged - inconsistent
    # in the portal for no reason, since all the elements are read together.
    normalised = {matching.normalize_indices(key): labels for key, labels in index.items()}

    readers: Set[str] = set()
    for entry in inventory:
        if not entry.get("consumers"):
            continue
        runtime = entry.get("runtime", "dotnet")
        if (matching.resolve(entry["key"], runtime, index)
                or matching.resolve(matching.normalize_indices(entry["key"]), runtime,
                                    normalised)):
            readers.update(entry["consumers"])
    if not readers:
        return set()

    label = item.get("label")
    if label:
        owner = owners.get(label)
        # An unrecognised label means no service fetches the row at all. Leave it untagged
        # rather than inventing a consumer; check_required_config already reports it.
        return {owner} & readers if owner else set()
    return readers


def plan_for(items: List[Dict[str, Any]], inventory: List[Dict[str, Any]],
             owners: Dict[str, str]) -> List[Dict[str, Any]]:
    """Rows whose consumers tag is missing or out of date."""
    plan: List[Dict[str, Any]] = []
    for position, item in enumerate(items):
        key = item.get("key")
        if not isinstance(key, str) or key.startswith(".appconfig"):
            continue
        consumers = consumers_for_row(item, inventory, owners)
        if not consumers:
            continue
        desired = ",".join(sorted(consumers))
        existing = (item.get("tags") or {}).get(CONSUMERS_TAG)
        if existing == desired:
            continue
        plan.append({"position": position, "key": key, "label": item.get("label") or "",
                     "desired": desired, "existing": existing})
    return plan


def apply_plan(items: List[Dict[str, Any]], plan: List[Dict[str, Any]]) -> None:
    """Set the consumers tag, preserving every other tag already on the row."""
    for entry in plan:
        item = items[entry["position"]]
        tags = dict(item.get("tags") or {})
        tags[CONSUMERS_TAG] = entry["desired"]
        item["tags"] = tags


def process(env: str, config_dir: str, inventory: List[Dict[str, Any]],
            owners: Dict[str, str], write: bool, hint: Optional[str] = None) -> int:
    path = os.path.join(config_dir, f"app-config.{env}.json")
    document = findings_mod.load_json_file(path, hint=hint)
    items = document.get("items", [])
    plan = plan_for(items, inventory, owners)

    print(f"{env:5} {len(items):4} rows   {len(plan):4} to tag")
    for entry in plan[:8]:
        label = f"[{entry['label']}]" if entry["label"] else "[no label]"
        action = "add" if entry["existing"] is None else "update"
        print(f"        {action:6} {entry['key']} {label} -> {entry['desired']}")
    if len(plan) > 8:
        print(f"        ... and {len(plan) - 8} more")

    if write and plan:
        apply_plan(items, plan)
        # Byte-exact match for how `az appconfig kv export --profile appconfig/kvset` writes
        # these files: tab indent, LF endings, no trailing newline. Any other combination
        # reformats all ~2300 lines and buries the actual change.
        text = json.dumps(document, indent="\t", ensure_ascii=False)
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
        print(f"        wrote {path}")
    return len(plan)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Tag the App Config exports with their consuming services.")
    parser.add_argument("--env", help="Which export to process")
    parser.add_argument("--all", action="store_true", help="Process every environment")
    parser.add_argument("--config-dir", default=matching.default_config_dir(),
                        help="Where link-cac's exports are (default: %(default)s; "
                             "also settable with LINK_CAC_CONFIG_DIR)")
    parser.add_argument("--inventory", default=DEFAULT_INVENTORY)
    parser.add_argument("--catalog", default=DEFAULT_CATALOG)
    parser.add_argument("--write", action="store_true",
                        help="Edit the export files. Without this, nothing is written.")
    args = parser.parse_args()

    if not args.env and not args.all:
        parser.error("pass --env <name> or --all")

    inventory = findings_mod.load_json_file(
        args.inventory, hint=INVENTORY_HINT).get("keys", [])
    catalog = findings_mod.load_yaml_file(args.catalog)
    environments = matching.environment_names(catalog)
    owners = label_owners(args.catalog)

    if args.env and args.env not in environments:
        parser.error(f"unknown environment {args.env!r}; "
                     f"{args.catalog} declares {', '.join(environments)}")
    targets = environments if args.all else [args.env]

    total = sum(process(env, args.config_dir, inventory, owners, args.write,
                        matching.missing_export_hint(catalog, env, args.catalog,
                                                     args.config_dir))
                for env in targets)

    if not total:
        print("\nNothing to do: every attributable row already carries the right tag.")
    elif not args.write:
        print("\nDRY RUN - nothing was written. Re-run with --write to edit the export(s).")
    else:
        print(f"\nTagged {total} row(s) in {args.config_dir}. The only diff should be tags,")
        print("and it is in the link-cac repository, not this one:")
        print(f"    git -C {os.path.dirname(os.path.abspath(args.config_dir))} diff")
    return 0


if __name__ == "__main__":
    sys.exit(main())
