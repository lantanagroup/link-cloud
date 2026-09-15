"""Shared reporting and file loading for the configuration checks.

Four scripts here look for problems in the configuration catalog and the environment exports,
and each had grown its own copy of the same three things: a Finding record, an ERROR/WARN
report, and the rule mapping findings to an exit code. Two of the Finding classes were
byte-identical; the third differed only in assembling its location from parts.

That mattered more than the duplicated lines suggest. Three of these scripts gate CI, so
changing how --strict behaves meant editing three files and hoping none was missed. One
definition means one place to get it right.

Loading is here for the same reason. Six functions - load_config, load_document, load_store,
load_inventory, load_json, load_yaml - all did "read this file, or exit 2 with a message that
says which file and why".
"""

import json
import os
import sys
from typing import Any, Dict, List, Optional

ERROR = "ERROR"
WARN = "WARN"

# Reserved so a caller cannot confuse "could not run" with "ran and found problems". 1 means
# findings, 2 means the inputs were unusable.
EXIT_OK = 0
EXIT_FINDINGS = 1
EXIT_UNUSABLE = 2


class Finding:
    """One problem, located precisely enough to act on without hunting for it."""

    def __init__(self, severity: str, where: str, message: str):
        self.severity = severity
        self.where = where
        self.message = message

    def format(self) -> str:
        return f"  [{self.severity}] {self.where}\n           {self.message}"

    def __repr__(self) -> str:
        return f"Finding({self.severity}, {self.where!r})"


def entry_location(path: str, index: int, key: str, label: Optional[str] = None) -> str:
    """Location string for a finding about one row of an export.

    Callers that know the file, row index and key build the string here rather than carrying
    the parts through the Finding, so every script formats a location the same way.
    """
    label_part = f" [label: {label}]" if label else ""
    return f"{os.path.basename(path)} (item {index}): {key}{label_part}"


def _fail(message: str) -> None:
    print(f"Error: {message}", file=sys.stderr)
    sys.exit(EXIT_UNUSABLE)


def load_json_file(path: str, hint: Optional[str] = None) -> Any:
    """Read a JSON file, or exit 2 explaining which file and why.

    `hint` is printed after the error - use it to say how to produce a missing file, so the
    reader is not left to work that out.
    """
    try:
        with open(path, "r", encoding="utf-8-sig") as handle:
            return json.load(handle)
    except FileNotFoundError:
        print(f"Error: File not found: {path}", file=sys.stderr)
        if hint:
            print(hint, file=sys.stderr)
        sys.exit(EXIT_UNUSABLE)
    except json.JSONDecodeError as exc:
        _fail(f"Invalid JSON in {path}: {exc}")
    except OSError as exc:
        _fail(f"Could not read {path}: {exc}")


def load_yaml_file(path: str) -> Dict[str, Any]:
    """Read a YAML file that must contain a mapping, or exit 2."""
    import yaml  # imported here so the stdlib-only secret scanner does not require PyYAML

    try:
        with open(path, "r", encoding="utf-8") as handle:
            document = yaml.safe_load(handle)
    except FileNotFoundError:
        _fail(f"File not found: {path}")
    except yaml.YAMLError as exc:
        _fail(f"{path} is not valid YAML: {exc}")
    except OSError as exc:
        _fail(f"Could not read {path}: {exc}")

    if not isinstance(document, dict):
        _fail(f"{path} does not contain a mapping at the root.")
    return document


def report(findings: List[Finding], headline: str, all_clear: str,
           strict: bool = False, epilogue: Optional[str] = None) -> int:
    """Print findings by severity and return the exit code.

    Errors always fail. Warnings fail only under --strict, so a warning can describe something
    worth a human look without blocking every build until someone silences it - which is how
    checks stop being read.
    """
    errors = [f for f in findings if f.severity == ERROR]
    warnings = [f for f in findings if f.severity == WARN]

    print(headline)

    if errors:
        print(f"\nERRORS ({len(errors)}):")
        for finding in errors:
            print(finding.format())

    if warnings:
        print(f"\nWARNINGS ({len(warnings)}):")
        for finding in warnings:
            print(finding.format())

    if not findings:
        print(f"\n{all_clear}")
        return EXIT_OK

    print(f"\nSummary: {len(errors)} error(s), {len(warnings)} warning(s).")

    if errors:
        if epilogue:
            print(f"\n{epilogue}")
        return EXIT_FINDINGS
    if warnings and strict:
        print("\nFailing because --strict treats warnings as errors.")
        return EXIT_FINDINGS
    return EXIT_OK
