"""Derive the set of configuration keys the .NET services actually read.

`app-config.yaml` is meant to catalog every configuration key the platform reads, but
nothing has ever checked it against the code. This script produces the other side of that
comparison: an inventory built from the source, so drift in either direction becomes visible.

Two properties of this codebase defeat plain text matching:

  * Constant indirection. Only 13 of 122 GetSection/GetRequiredSection calls pass a string
    literal; the other 109 pass a constant such as `KafkaConstants.SectionName` or
    `ConfigurationConstants.AppSettings.CORS`, or `nameof(ConsumerSettings)`. A literal-only
    scan would miss 89% of section reads.

  * Section-to-class binding. `Configure<ResourceCacheSettings>(config.GetSection(...))`
    reads roughly eight sub-keys that appear nowhere as strings. They exist only as members
    of the bound type, some of them inherited.

So the work is split between two tools, each doing what it is good at:

  Scripts/AzureAppConfig/dump_config_symbols.cs  (Roslyn, run as a file-based app)
      Resolves what only a symbol model can: constant values whether declared `const` or
      `static`, ambiguity when several classes share a member name, inherited members, and
      the difference between a bindable property and a public field that ConfigurationBinder
      silently ignores. Emits Scripts/AzureAppConfig/config_symbols.json.

  this script
      Scans the configuration API call sites, resolves each argument through the constant
      map, expands each bound type through the type map, and attributes keys to services via
      the .csproj ProjectReference graph - so a key read in DotNet/Shared attributes to every
      service referencing it, and one in DotNet/DataAcquisition.Domain to both DataAcquisition
      and the AcquisitionWorker.

Known blind spots, stated rather than papered over:

  * Keys assembled by string concatenation at runtime are invisible.
  * Keys read reflectively are invisible.
  * GetSection called on an already-scoped section object is read as though it were
    root-relative. ResourceCacheExtensions does this, yielding a spurious top-level
    `BlobStorage:*` alongside the real `ResourceCache:BlobStorage:*`.
  * Framework-owned schemas are not derivable from Link's source at all. YARP binds
    `ReverseProxy:Routes:<arbitrary-name>:*` and Serilog binds `Serilog:WriteTo:<index>:*`
    from their own schemas over names this codebase never declares.
  * Java is not scanned here; its bindings come from Scripts/AzureAppConfig/java_config_audit.json.

Because of those, the inventory is evidence for a human to adjudicate, not a verdict. The
reconciliation step compares it against the catalog in both directions: a store key with no
inventory entry is either a missed read or dead weight, and either answer is useful.

Usage:
    dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet Scripts/AzureAppConfig/config_symbols.json
    python Scripts/AzureAppConfig/extract_config_keys.py
"""

import argparse
import glob
import json
import os
import re
import sys
from collections import defaultdict
from typing import Any, Dict, List, Optional, Set, Tuple

# Imported for default_config_dir alone, so this stays runnable without PyYAML - which the
# fallbacks below deliberately support. config_key_matching imports nothing beyond the stdlib.
import config_key_matching as matching

# ---------------------------------------------------------------------------
# Scanning helpers
# ---------------------------------------------------------------------------

SKIP_DIR_PARTS = {"bin", "obj", ".vs", "node_modules", "Migrations"}
# Test and generator projects are not deployed services; their config reads would pollute
# the inventory with keys no environment needs to provision.
SKIP_PROJECTS = {"ServiceTests", "Audit.Specification"}

COLLECTION_RE = re.compile(r"^(?:List|IList|IEnumerable|ICollection|IReadOnlyList|HashSet)<(.+)>$")
DICT_RE = re.compile(r"^(?:Dictionary|IDictionary|IReadOnlyDictionary)<\s*string\s*,\s*(.+)>$")
ARRAY_RE = re.compile(r"^(.+)\[\]$")

PRIMITIVES = {
    "string", "int", "long", "short", "byte", "bool", "double", "float", "decimal",
    "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "Uri", "object", "char",
}


def iter_source_files(root: str, suffix: str = ".cs"):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS]
        parts = set(dirpath.replace("\\", "/").split("/"))
        if parts & SKIP_PROJECTS:
            continue
        for name in filenames:
            if name.endswith(suffix):
                yield os.path.join(dirpath, name)


def read_text(path: str) -> str:
    with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
        return handle.read()


def normalize_with_lines(text: str) -> Tuple[str, List[int]]:
    """Collapse whitespace so multi-line calls match, keeping a char -> line index.

    C# call chains routinely span lines. Matching on the raw text needs fragile patterns;
    matching on fully-collapsed text loses the line number. This keeps both.
    """
    out: List[str] = []
    lines: List[int] = []
    line = 1
    in_space = False
    for ch in text:
        if ch == "\n":
            line += 1
        if ch.isspace():
            if not in_space:
                out.append(" ")
                lines.append(line)
                in_space = True
            continue
        in_space = False
        out.append(ch)
        lines.append(line)
    return "".join(out), lines


# ---------------------------------------------------------------------------
# Passes A and B - supplied by Roslyn (Scripts/AzureAppConfig/dump_config_symbols.cs)
# ---------------------------------------------------------------------------

def load_symbols(path: str) -> Tuple[Dict[str, Optional[str]], Dict[str, Dict[str, Any]]]:
    """Load the constant and type maps produced by the Roslyn dump.

    Deriving these from text was wrong in four ways - static-vs-const declarations, six
    classes sharing a member name, inherited settings properties, and fields that look like
    properties but are never bound. Roslyn resolves all four from the symbol model, so they
    are correct by construction rather than by iteration.

    There is deliberately no regex fallback: silently degrading to the less accurate method
    would hide the staleness this whole exercise exists to surface.
    """
    if not os.path.exists(path):
        print(f"Error: {path} not found.", file=sys.stderr)
        print("Generate it first:", file=sys.stderr)
        print(f"    dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet {path}",
              file=sys.stderr)
        sys.exit(2)
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    return payload.get("constants", {}), payload.get("types", {})


def members_of(type_name: str, types: Dict[str, Dict[str, Any]],
               seen: Optional[Set[str]] = None) -> List[Dict[str, Any]]:
    """Members of a type including inherited ones, base classes last."""
    seen = set(seen or ())
    if type_name in seen or type_name not in types:
        return []
    seen.add(type_name)
    entry = types[type_name]
    members = list(entry.get("members", []))
    known = {m["name"] for m in members}
    for base in entry.get("bases", []):
        for member in members_of(base, types, seen):
            if member["name"] not in known:
                members.append(member)
                known.add(member["name"])
    return members


def expand_type(type_name: str, prefix: str, types: Dict[str, Dict[str, Any]],
                depth: int = 0, seen: Optional[Set[str]] = None) -> List[Tuple[str, bool]]:
    """Expand a bound settings type into the config sub-keys it supplies.

    Returns (key, bindable) pairs. `bindable` is False for public fields, which
    ConfigurationBinder does not bind: the key may exist in a store and still never apply.
    """
    if depth > 4:
        return []
    seen = set(seen or ())
    if type_name in seen:
        return []
    seen.add(type_name)

    keys: List[Tuple[str, bool]] = []
    for member in members_of(type_name, types):
        key = prefix + ":" + member["name"] if prefix else member["name"]
        bare = member["type"].strip().rstrip("?")
        bindable = member.get("bindable", True)

        if DICT_RE.match(bare):
            # A dictionary section can be stored either as one JSON blob on the parent key or
            # as one row per entry, so both shapes are legitimate.
            keys.append((key, bindable))
            keys.append((key + ":{Placeholder}", bindable))
            continue

        coll = COLLECTION_RE.match(bare) or ARRAY_RE.match(bare)
        if coll:
            inner = coll.group(1).strip().rstrip("?")
            if inner in PRIMITIVES or inner not in types:
                keys.append((key + ":0", bindable))
            else:
                keys.extend(expand_type(inner, key + ":0", types, depth + 1, seen))
            continue

        if bare in PRIMITIVES or bare not in types:
            keys.append((key, bindable))
        else:
            nested = expand_type(bare, key, types, depth + 1, seen)
            keys.extend(nested or [(key, bindable)])
    return keys


# ---------------------------------------------------------------------------
# Pass C - call sites
# ---------------------------------------------------------------------------

# A key may be a literal, a nameof(), a constant reference, or an interpolated string whose
# first hole is a constant holding the section - Automation.UI writes
# `$"{ApiBearerConfigSection}:Enabled"`. Without the last form those keys look dead, and
# deleting them from the catalog would remove config the service genuinely reads.
ARG = (r'(?:\$"\{\s*(?P<interp>[A-Za-z_][\w\.]*)\s*\}(?P<isuffix>[^"{}]*)"'
       r'|"(?P<lit>[^"]*)"'
       r'|nameof\(\s*(?P<nam>[A-Za-z_][\w\.]*)\s*\)'
       r'|(?P<ref>[A-Za-z_][\w\.]*))')

PATTERNS = [
    ("configure-bind", re.compile(
        r"Configure<\s*(?P<type>[A-Za-z_]\w*)\s*>\s*\(\s*[^()]*?Get(?:Required)?Section\(\s*" + ARG + r"\s*\)")),
    ("section-get", re.compile(
        r"Get(?:Required)?Section\(\s*" + ARG + r"\s*\)\s*\.\s*Get<\s*(?P<type>[A-Za-z_]\w*)\s*>")),
    # AddOptions<T>().Bind(config.GetSection(...)) - Terminology uses this to get
    # .Validate()/.ValidateOnStart() on top of the binding.
    ("addoptions-bind", re.compile(
        r"AddOptions<\s*(?P<type>[A-Za-z_]\w*)\s*>\s*\(\s*\)\s*(?:\.\s*\w+\s*\([^()]*\)\s*)*?"
        r"\.\s*Bind\(\s*[^()]*?Get(?:Required)?Section\(\s*" + ARG + r"\s*\)")),
    ("bind", re.compile(
        r"Get(?:Required)?Section\(\s*" + ARG + r"\s*\)\s*\.\s*Bind\(\s*new\s+(?P<type>[A-Za-z_]\w*)")),
    ("section", re.compile(r"Get(?:Required)?Section\(\s*" + ARG + r"\s*\)")),
    ("getvalue", re.compile(r"GetValue<[^>]+>\(\s*" + ARG + r"\s*[,)]")),
    ("connstring", re.compile(r"GetConnectionString\(\s*" + ARG + r"\s*\)")),
    ("indexer", re.compile(r"Configuration\[\s*" + ARG + r"\s*\]")),
]

# `var section = config.GetSection(X); services.Configure<T>(section);` - the section is
# held in a local before being bound, so the direct patterns cannot see the pairing.
# ResourceCacheExtensions.cs uses this shape for the whole ResourceCache tree.
LOCAL_SECTION_RE = re.compile(
    r"\bvar\s+(?P<var>[A-Za-z_]\w*)\s*=\s*[^;()]*?Get(?:Required)?Section\(\s*" + ARG + r"\s*\)")


def local_binding_patterns(var: str) -> List[Tuple[str, "re.Pattern"]]:
    name = re.escape(var)
    return [
        ("configure-bind", re.compile(
            r"Configure<\s*(?P<type>[A-Za-z_]\w*)\s*>\s*\(\s*" + name + r"\s*\)")),
        ("section-get", re.compile(
            r"\b" + name + r"\s*\.\s*Get<\s*(?P<type>[A-Za-z_]\w*)\s*>")),
    ]


def lookup_constant(ref: str, constants: Dict[str, Optional[str]]) -> Optional[str]:
    """Most-qualified match wins; an ambiguous name maps to None and stays unresolved."""
    tail = ref.split(".")
    for start in range(len(tail)):
        candidate = ".".join(tail[start:])
        if candidate in constants:
            return constants[candidate]
    return None


def resolve_arg(match: re.Match, constants: Dict[str, Optional[str]]) -> Optional[str]:
    groups = match.groupdict()
    if groups.get("interp"):
        base = lookup_constant(groups["interp"], constants)
        return None if base is None else base + (groups.get("isuffix") or "")
    if match.group("lit") is not None:
        return match.group("lit")
    if match.group("nam"):
        return match.group("nam").split(".")[-1]
    ref = match.group("ref")
    if not ref:
        return None
    return lookup_constant(ref, constants)


# ---------------------------------------------------------------------------
# Project graph
# ---------------------------------------------------------------------------

def owning_project(path: str, projects: Dict[str, str]) -> Optional[str]:
    current = os.path.dirname(os.path.abspath(path))
    while True:
        for name, proj_dir in projects.items():
            if os.path.abspath(proj_dir) == current:
                return name
        parent = os.path.dirname(current)
        if parent == current:
            return None
        current = parent


def build_project_graph(root: str) -> Tuple[Dict[str, str], Dict[str, Set[str]]]:
    projects: Dict[str, str] = {}
    refs: Dict[str, Set[str]] = defaultdict(set)
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS]
        for name in filenames:
            if name.endswith(".csproj"):
                proj = name[:-len(".csproj")]
                projects[proj] = dirpath
                text = read_text(os.path.join(dirpath, name))
                for ref in re.findall(r'ProjectReference\s+Include="([^"]+)"', text):
                    refs[proj].add(os.path.basename(ref).replace(".csproj", ""))
    return projects, refs


def transitive_consumers(project: str, refs: Dict[str, Set[str]],
                         services: Dict[str, str]) -> List[str]:
    """Which services reach this project, directly or through the reference graph."""
    consumers = []
    for service, service_project in services.items():
        seen: Set[str] = set()
        stack = [service_project]
        while stack:
            current = stack.pop()
            if current in seen:
                continue
            seen.add(current)
            stack.extend(refs.get(current, ()))
        if project in seen:
            consumers.append(service)
    return sorted(consumers)


# ---------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------

def normalize_key(kind: str, value: str) -> Optional[str]:
    value = value.strip()
    if not value:
        return None
    if kind == "connstring":
        return "ConnectionStrings:" + value
    return value


def extract(root: str, service_projects: Dict[str, str], symbols_path: str) -> Dict[str, Dict[str, Any]]:
    files = sorted(iter_source_files(root))
    constants, types = load_symbols(symbols_path)
    projects, refs = build_project_graph(root)

    consumer_cache: Dict[str, List[str]] = {}
    inventory: Dict[str, Dict[str, Any]] = {}

    def record(key: str, path: str, line: int, kind: str, type_name: Optional[str],
               derived: bool, bindable: bool = True):
        project = owning_project(path, projects)
        if project in SKIP_PROJECTS:
            return
        if project not in consumer_cache:
            consumer_cache[project] = transitive_consumers(project, refs, service_projects)
        entry = inventory.setdefault(key, {
            "key": key, "runtime": "dotnet", "consumers": set(), "derived": derived,
            "bindable": bindable, "evidence": [],
        })
        entry["consumers"].update(consumer_cache[project])
        if not derived:
            entry["derived"] = False
        if not bindable:
            entry["bindable"] = False
        rel = os.path.relpath(path, ".").replace("\\", "/")
        item = {"file": rel, "line": line, "kind": kind}
        if type_name:
            item["type"] = type_name
        if item not in entry["evidence"]:
            entry["evidence"].append(item)

    for path in files:
        text = read_text(path)
        flat, line_of = normalize_with_lines(text)
        claimed: Set[Tuple[int, int]] = set()

        for kind, pattern in PATTERNS:
            for match in pattern.finditer(flat):
                span = match.span()
                # A "section" match inside an already-claimed configure-bind/section-get span
                # is the same call seen twice; keep the richer interpretation.
                if kind == "section" and any(s <= span[0] and span[1] <= e for s, e in claimed):
                    continue
                value = resolve_arg(match, constants)
                if value is None:
                    continue
                key = normalize_key(kind, value)
                if not key:
                    continue
                line = line_of[span[0]] if span[0] < len(line_of) else 0
                type_name = match.groupdict().get("type")

                record(key, path, line, kind, type_name, derived=False)
                if type_name:
                    claimed.add(span)
                    for sub, bindable in expand_type(type_name, key, types):
                        record(sub, path, line, kind + "+expand", type_name,
                               derived=True, bindable=bindable)

        # Sections held in a local before being bound.
        for local in LOCAL_SECTION_RE.finditer(flat):
            key = resolve_arg(local, constants)
            if not key:
                continue
            for kind, pattern in local_binding_patterns(local.group("var")):
                for use in pattern.finditer(flat, local.end()):
                    type_name = use.group("type")
                    line = line_of[use.start()] if use.start() < len(line_of) else 0
                    record(key, path, line, kind + "+local", type_name, derived=False)
                    for sub, bindable in expand_type(type_name, key, types):
                        record(sub, path, line, kind + "+local+expand", type_name,
                               derived=True, bindable=bindable)

    for entry in inventory.values():
        entry["consumers"] = sorted(entry["consumers"])
    return inventory


def load_java_audit(path: Optional[str]) -> List[Dict[str, Any]]:
    if not path or not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle).get("keys", [])


def store_presence(environments: List[str],
                   config_dir: Optional[str] = None) -> Dict[str, List[str]]:
    """Which environments hold each key, for the human-facing table."""
    config_dir = matching.default_config_dir() if config_dir is None else config_dir
    presence: Dict[str, List[str]] = defaultdict(list)
    for env in environments:
        path = os.path.join(config_dir, "app-config." + env + ".json")
        if not os.path.exists(path):
            continue
        with open(path, "r", encoding="utf-8") as handle:
            for item in json.load(handle).get("items", []):
                presence[item["key"]].append(env)
    return presence


def store_key_forms(key: str, runtime: str) -> List[str]:
    """The shapes a catalog key may legitimately take in a store."""
    forms = [key]
    if runtime == "java":
        forms.append("/" + key.replace(".", "/"))
    else:
        forms.append(key + ":0")
    return forms


SERVICE_DIRS = {
    "Account": "DotNet/Account", "AdminBFF": "DotNet/Admin.BFF", "Audit": "DotNet/Audit",
    "AutomationUI": "DotNet/Automation.UI", "Census": "DotNet/Census",
    "DataAcquisition": "DotNet/DataAcquisition",
    "DataAcquisitionWorker": "DotNet/DataAcquisition.AcquisitionWorker",
    "Normalization": "DotNet/Normalization", "Notification": "DotNet/Notification",
    "QueryDispatch": "DotNet/QueryDispatch", "Report": "DotNet/Report",
    "Submission": "DotNet/Submission", "Tenant": "DotNet/Tenant",
    "Terminology": "DotNet/Terminology",
    "MeasureEval": "Java/measureeval", "Validation": "Java/validation",
}


def service_owns(service: str, file_path: str) -> bool:
    directory = SERVICE_DIRS.get(service)
    return bool(directory) and file_path.startswith(directory + "/")


def write_markdown(keys: List[Dict[str, Any]], path: str, catalog_keys: Set[str],
                   environments: List[str], config_dir: Optional[str] = None) -> None:
    """Emit the human-readable inventory.

    app-config.yaml stays curated and scannable as the deployment hand-off artifact, so the
    exhaustive picture lives here instead. Generated from the same data, so it cannot drift.
    """
    presence = store_presence(environments, config_dir)
    by_service: Dict[str, List[Dict[str, Any]]] = defaultdict(list)
    for entry in keys:
        for service in entry["consumers"] or ["(unattributed)"]:
            by_service[service].append(entry)

    lines: List[str] = []
    lines.append("# Configuration key inventory")
    lines.append("")
    lines.append("Every configuration key the code reads, derived from source by")
    lines.append("`Scripts/AzureAppConfig/extract_config_keys.py`. **Generated - do not edit by hand.**")
    lines.append("")
    lines.append("Regenerate with:")
    lines.append("")
    lines.append("```powershell")
    lines.append("python Scripts/AzureAppConfig/extract_config_keys.py")
    lines.append("```")
    lines.append("")
    lines.append("This is the exhaustive reference. `/app-config.yaml` is the curated catalog of")
    lines.append("keys that must be provisioned per environment, and is deliberately much shorter.")
    lines.append("")
    lines.append("Columns: **Catalog** - present in app-config.yaml. **Stores** - environments")
    lines.append("holding a row for it. **Source** - where the code reads it.")
    lines.append("")

    unbindable = [e for e in keys if not e.get("bindable", True)]
    if unbindable:
        lines.append("## Declared but not bindable")
        lines.append("")
        lines.append("`ConfigurationBinder` binds public *properties*. These are public **fields**, so a")
        lines.append("value set in a store can never take effect.")
        lines.append("")
        lines.append("| Key | Declaring type | In stores |")
        lines.append("|---|---|---|")
        for entry in unbindable:
            ev = entry["evidence"][0]
            envs = ", ".join(presence.get(entry["key"], [])) or "-"
            lines.append(f"| `{entry['key']}` | `{ev.get('type', '')}` | {envs} |")
        lines.append("")

    lines.append("## Keys by service")
    lines.append("")
    for service in sorted(by_service):
        entries = sorted(by_service[service], key=lambda e: e["key"])
        lines.append(f"### {service}")
        lines.append("")
        lines.append(f"{len(entries)} keys.")
        lines.append("")
        lines.append("| Key | Runtime | Catalog | Stores | Source |")
        lines.append("|---|---|---|---|---|")
        for entry in entries:
            forms = store_key_forms(entry["key"], entry["runtime"])
            envs = sorted({e for f in forms for e in presence.get(f, [])})
            in_catalog = "yes" if entry["key"] in catalog_keys else "-"
            # Show the read site belonging to this service where there is one. A shared key is
            # read in many services' Program.cs, and citing whichever was recorded first would
            # put another service's file against this service's row.
            ev = next((e for e in entry["evidence"] if service_owns(service, e["file"])),
                      entry["evidence"][0])
            src = f"{ev['file']}:{ev['line']}"
            lines.append(
                f"| `{entry['key']}` | {entry['runtime']} | {in_catalog} | "
                f"{', '.join(envs) or '-'} | `{src}` |")
        lines.append("")

    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines))


def catalog_environments(path: str, config_dir: Optional[str] = None) -> List[str]:
    """Environments the catalog declares, for the "stores" column.

    Falls back to whichever exports are present in link-cac. That is a weaker answer than the
    catalog - it cannot tell a declared environment apart from a forgotten one, and it reads
    empty when link-cac is not checked out beside this repository - but for a documentation
    column it is still the right question, and it keeps the inventory generatable without
    PyYAML installed.
    """
    config_dir = matching.default_config_dir() if config_dir is None else config_dir
    try:
        import yaml
        with open(path, "r", encoding="utf-8") as handle:
            declared = list(((yaml.safe_load(handle) or {}).get("environments") or {}).keys())
        if declared:
            return declared
        print(f"Warning: {path} declares no environments; falling back to the committed "
              f"exports.", file=sys.stderr)
    except ImportError:
        print(f"Warning: PyYAML is not installed, so the environments in {path} cannot be "
              f"read; falling back to the committed exports.", file=sys.stderr)
    except OSError:
        print(f"Warning: {path} could not be read; falling back to the committed exports.",
              file=sys.stderr)

    found = sorted(re.sub(r"^app-config\.|\.json$", "", os.path.basename(p))
                   for p in glob.glob(os.path.join(config_dir, "app-config.*.json")))
    return found


def catalog_key_set(path: str) -> Set[str]:
    """Catalog keys, for the "in catalog" column. Degrades to empty rather than failing.

    An empty set is indistinguishable in the output from a catalog that documents nothing:
    every row reads "-". Since docs/config-key-inventory.md is committed, a silent empty set
    means publishing a document that claims none of the keys are catalogued. Both ways of
    getting there therefore say so on stderr.
    """
    try:
        import yaml
    except ImportError:
        print(f"Warning: PyYAML is not installed, so {path} cannot be read. The 'in catalog' "
              f"column will read '-' for every key. Install it with: pip install pyyaml",
              file=sys.stderr)
        return set()
    if not os.path.exists(path):
        print(f"Warning: catalog not found at {path}. The 'in catalog' column will read '-' "
              f"for every key.", file=sys.stderr)
        return set()
    with open(path, "r", encoding="utf-8") as handle:
        document = yaml.safe_load(handle) or {}
    keys = {e["key"] for e in document.get("global", []) if isinstance(e, dict)}
    for entries in (document.get("services") or {}).values():
        keys |= {e["key"] for e in entries if isinstance(e, dict)}
    return keys


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Derive the configuration keys the .NET services read.")
    parser.add_argument("--root", default="DotNet", help="Source root to scan (default: DotNet)")
    parser.add_argument("--catalog", default="app-config.yaml",
                        help="Catalog, read only for the serviceMeta project mapping")
    parser.add_argument("--symbols", default="Scripts/AzureAppConfig/config_symbols.json",
                        help="Roslyn symbol dump from Scripts/AzureAppConfig/dump_config_symbols.cs")
    parser.add_argument("--java-audit", default="Scripts/AzureAppConfig/java_config_audit.json",
                        help="Hand-maintained Java binding audit to fold in")
    parser.add_argument("--config-dir", default=matching.default_config_dir(),
                        help="Where link-cac's exports are, for the 'stores' column "
                             "(default: %(default)s; also settable with LINK_CAC_CONFIG_DIR)")
    parser.add_argument("--json", default="Scripts/AzureAppConfig/config-key-inventory.json",
                        help="Where to write the machine-readable inventory")
    parser.add_argument("--markdown", default="docs/config-key-inventory.md",
                        help="Where to write the human-readable inventory")
    parser.add_argument("--summary", action="store_true",
                        help="Print a summary instead of writing files")
    args = parser.parse_args()

    service_projects = {
        "Account": "Account", "AdminBFF": "Admin.BFF", "Audit": "Audit",
        "AutomationUI": "Automation.UI", "Census": "Census",
        "DataAcquisition": "DataAcquisition",
        "DataAcquisitionWorker": "DataAcquisition.AcquisitionWorker",
        "Normalization": "Normalization", "Notification": "Notification",
        "QueryDispatch": "QueryDispatch", "Report": "Report",
        "Submission": "Submission", "Tenant": "Tenant", "Terminology": "Terminology",
    }

    inventory = extract(args.root, service_projects, args.symbols)
    for entry in load_java_audit(args.java_audit):
        existing = inventory.get(entry["key"])
        if existing:
            existing["consumers"] = sorted(set(existing["consumers"]) | set(entry.get("consumers", [])))
        else:
            inventory[entry["key"]] = entry

    keys = [inventory[k] for k in sorted(inventory)]

    if args.summary:
        dotnet = [k for k in keys if k["runtime"] == "dotnet"]
        java = [k for k in keys if k["runtime"] == "java"]
        derived = [k for k in keys if k.get("derived")]
        print(f"keys: {len(keys)}  (.NET {len(dotnet)}, Java {len(java)}, derived {len(derived)})")
        orphans = [k["key"] for k in keys if not k["consumers"]]
        print(f"keys with no attributed consumer: {len(orphans)}")
        for key in orphans[:15]:
            print(f"    {key}")
        return 0

    os.makedirs(os.path.dirname(args.json) or ".", exist_ok=True)
    with open(args.json, "w", encoding="utf-8", newline="\n") as handle:
        json.dump({"keys": keys}, handle, indent=2)
        handle.write("\n")
    print(f"Wrote {args.json} ({len(keys)} keys)")

    if args.markdown:
        os.makedirs(os.path.dirname(args.markdown) or ".", exist_ok=True)
        write_markdown(keys, args.markdown, catalog_key_set(args.catalog),
                       catalog_environments(args.catalog, args.config_dir), args.config_dir)
        print(f"Wrote {args.markdown}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
