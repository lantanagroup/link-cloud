"""Validate app-config.yaml against the JSON Schema embedded in itself.

app-config.yaml is unusual: it is both the schema and the data. The top of the file
declares `properties`, `required` and `$defs`; the bottom carries the actual `global`,
`services` and `serviceMeta` content. Nothing has ever checked one against the other, so
every constraint in it -- `additionalProperties: false`, the enum on `runtime`, and the
`sensitive`/`defaultValue` guard -- has been decorative.

This script closes that gap. It reads the rules out of the document's own `$defs` rather
than hardcoding them, so adding a field to the schema automatically extends the check
instead of silently bypassing it.

Checks:

  ERROR  A required top-level section is missing.
  ERROR  An entry omits `key` or `description`, or carries a property the schema does not
         declare. `additionalProperties: false` means a typo'd field name is a silent
         no-op today; here it fails.
  ERROR  A property has the wrong type, or a value outside its `enum`.
  ERROR  An entry is `sensitive: true` and also carries a `defaultValue`. Secrets and Key
         Vault references are supplied per environment and must never be seeded from this
         file. This is the guard the file's own comment calls load-bearing.
  ERROR  A service in `services` has no `serviceMeta` entry, or vice versa. Tooling reads
         `serviceMeta` for the label and runtime; a service missing from it would have its
         keys silently unchecked.
  ERROR  The same key appears twice within one section.

  WARN   An entry omits `required`. The schema documents `default: true`, but JSON Schema
         defaults are annotation only and are never applied during validation, so the
         entry's true meaning depends on every reader making the same assumption.

Exit code is 0 when no errors are found (and, with --strict, no warnings either).

Usage:
    python Scripts/AzureAppConfig/validate_app_config_schema.py
    python Scripts/AzureAppConfig/validate_app_config_schema.py app-config.yaml --strict
"""

import argparse
import os
import sys
from typing import Any, Dict, List, Optional

import yaml

import config_findings as findings_mod
from config_findings import ERROR, WARN, Finding

DEFAULT_PATH = "app-config.yaml"


def type_matches(value: Any, declared: str) -> bool:
    """Check a value against a JSON Schema primitive type name."""
    if declared == "string":
        return isinstance(value, str)
    if declared == "boolean":
        return isinstance(value, bool)
    if declared == "array":
        return isinstance(value, list)
    if declared == "object":
        return isinstance(value, dict)
    if declared == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if declared == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    return True


def check_against_def(entry: Any, definition: Dict[str, Any], where: str) -> List[Finding]:
    """Validate one mapping against a $defs definition, driven by the definition itself."""
    findings: List[Finding] = []

    if not isinstance(entry, dict):
        return [Finding("ERROR", where, f"Expected a mapping, found {type(entry).__name__}.")]

    declared = definition.get("properties", {})

    for name in definition.get("required", []):
        if name not in entry:
            findings.append(Finding("ERROR", where, f"Missing required property '{name}'."))

    if definition.get("additionalProperties") is False:
        for name in entry:
            if name not in declared:
                findings.append(Finding(
                    "ERROR", where,
                    f"Property '{name}' is not declared in the schema. The schema sets "
                    f"additionalProperties: false, so this value is ignored by every reader."))

    for name, value in entry.items():
        spec = declared.get(name)
        if not isinstance(spec, dict):
            continue
        expected = spec.get("type")
        if expected and not type_matches(value, expected):
            findings.append(Finding(
                "ERROR", where,
                f"Property '{name}' should be {expected}, found "
                f"{type(value).__name__} ({value!r})."))
            continue
        allowed = spec.get("enum")
        if allowed and value not in allowed:
            findings.append(Finding(
                "ERROR", where,
                f"Property '{name}' is {value!r}, which is not one of {allowed}."))
        if expected == "array":
            item_type = spec.get("items", {}).get("type")
            for index, item in enumerate(value):
                if item_type and not type_matches(item, item_type):
                    findings.append(Finding(
                        "ERROR", where,
                        f"Property '{name}[{index}]' should be {item_type}, "
                        f"found {type(item).__name__}."))

    return findings


def check_sensitive_guard(entry: Dict[str, Any], where: str) -> List[Finding]:
    """A sensitive entry must never carry a defaultValue.

    Mirrors the if/then in the document. Kept as an explicit rule rather than a generic
    if/then interpreter because it is the one constraint whose failure would put a secret
    into a file committed to a public repository.
    """
    if not isinstance(entry, dict):
        return []
    if entry.get("sensitive") is True and "defaultValue" in entry:
        return [Finding(
            "ERROR", where,
            "Entry is sensitive: true and also sets defaultValue. Secrets and Key Vault "
            "references are supplied per environment and must never be seeded here.")]
    return []


def entry_label(entry: Any, section: str, index: int) -> str:
    key = entry.get("key") if isinstance(entry, dict) else None
    return f"{section}[{index}]" + (f" key='{key}'" if key else "")


def check_entries(document: Dict[str, Any], defs: Dict[str, Any]) -> List[Finding]:
    findings: List[Finding] = []
    entry_def = defs.get("configEntry")
    if not isinstance(entry_def, dict):
        return [Finding("ERROR", "$defs", "configEntry definition is missing.")]

    sections: List[tuple] = [("global", document.get("global"))]
    services = document.get("services")
    if isinstance(services, dict):
        sections.extend((f"services.{name}", entries) for name, entries in services.items())

    for section, entries in sections:
        if entries is None:
            findings.append(Finding("ERROR", section, "Section is missing or empty."))
            continue
        if not isinstance(entries, list):
            findings.append(Finding("ERROR", section, "Section should be a list of entries."))
            continue

        seen: Dict[tuple, int] = {}
        for index, entry in enumerate(entries):
            where = entry_label(entry, section, index)
            findings.extend(check_against_def(entry, entry_def, where))
            findings.extend(check_sensitive_guard(entry, where))

            if isinstance(entry, dict):
                key = entry.get("key")
                if isinstance(key, str):
                    composite = (key, entry.get("label") or "")
                    if composite in seen:
                        findings.append(Finding(
                            "ERROR", where,
                            f"Duplicate of {section}[{seen[composite]}]; the later entry wins "
                            f"and the earlier one is unreachable."))
                    else:
                        seen[composite] = index
                if "required" not in entry:
                    findings.append(Finding(
                        "WARN", where,
                        "No 'required' property. The schema documents default: true, but "
                        "JSON Schema defaults are annotation only and are never applied, so "
                        "the entry's meaning depends on the reader."))

    return findings


def check_service_meta(document: Dict[str, Any], defs: Dict[str, Any]) -> List[Finding]:
    findings: List[Finding] = []
    meta = document.get("serviceMeta")
    services = document.get("services")
    meta_def = defs.get("serviceMetaEntry")

    if not isinstance(meta, dict):
        return [Finding("ERROR", "serviceMeta", "Section is missing or is not a mapping.")]

    if isinstance(meta_def, dict):
        for name, entry in meta.items():
            findings.extend(check_against_def(entry, meta_def, f"serviceMeta.{name}"))

    if isinstance(services, dict):
        for name in services:
            if name not in meta:
                findings.append(Finding(
                    "ERROR", f"serviceMeta.{name}",
                    f"Service '{name}' is defined in services but has no serviceMeta entry. "
                    f"Tooling reads serviceMeta for the label and runtime, so its keys would "
                    f"go unchecked."))
        for name in meta:
            if name not in services:
                findings.append(Finding(
                    "ERROR", f"serviceMeta.{name}",
                    f"serviceMeta defines '{name}' but there is no matching entry in services."))

    labels: Dict[str, str] = {}
    for name, entry in meta.items():
        if not isinstance(entry, dict):
            continue
        label = entry.get("label")
        if isinstance(label, str):
            if label in labels:
                findings.append(Finding(
                    "ERROR", f"serviceMeta.{name}",
                    f"Label '{label}' is already used by '{labels[label]}'. Two services "
                    f"selecting the same label would read each other's overrides."))
            else:
                labels[label] = name
            if ":" in label:
                findings.append(Finding(
                    "ERROR", f"serviceMeta.{name}",
                    f"Label '{label}' contains ':'. The <Service>:<Environment> label tier "
                    f"does not resolve (ExternalConfigurationExtension.cs:64 concatenates the "
                    f"environment object rather than its name), so such a label matches nothing."))

    return findings


def check_top_level(document: Dict[str, Any]) -> List[Finding]:
    findings: List[Finding] = []
    for name in document.get("required", []):
        if name not in document:
            findings.append(Finding(
                "ERROR", "(root)",
                f"Section '{name}' is listed in the schema's required list but is absent."))
    return findings


def validate(path: str) -> List[Finding]:
    document = findings_mod.load_yaml_file(path)
    defs = document.get("$defs")
    if not isinstance(defs, dict):
        return [Finding("ERROR", "(root)", "$defs is missing; there is no schema to validate against.")]

    findings = check_top_level(document)
    findings.extend(check_entries(document, defs))
    findings.extend(check_service_meta(document, defs))
    return findings


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate app-config.yaml against its own embedded JSON Schema.")
    parser.add_argument("path", nargs="?", default=DEFAULT_PATH,
                        help=f"Path to the catalog (default: {DEFAULT_PATH})")
    parser.add_argument("--strict", action="store_true", help="Treat warnings as errors")
    args = parser.parse_args()

    findings = validate(args.path)
    return findings_mod.report(
        findings,
        headline=f"Validated {os.path.basename(args.path)} against its embedded schema.",
        all_clear="OK: catalog conforms to its schema.",
        strict=args.strict)


if __name__ == "__main__":
    sys.exit(main())
