"""Shared rules for deciding whether a catalog key is present in an App Configuration store.

Both `reconcile_config_catalog.py` and the required-key PR check need to answer the same
question, and the answer is not a string comparison. Six things get in the way, all of them
consequences of how the two runtimes actually read configuration:

  1. Notation. Java keys are catalogued in Spring's dotted form (`spring.datasource.url`) but
     stored in slash form (`/spring/datasource/url`). The provider strips the leading slash
     and converts the rest, so the two are one key.

  2. Arrays. `KafkaConnection:BootstrapServers` is catalogued singular and stored as
     `KafkaConnection:BootstrapServers:0`.

  3. Templates. `ReverseProxy:Clusters:{Service}:Destinations:destination1:Address` stands for
     thirteen concrete rows.

  4. JSON blobs. A row whose content_type is `application/json` is flattened by the provider,
     so a single `/authentication` row supplies `authentication.authority` and its siblings.
     Searching for that key literally finds nothing.

  5. Labels. A service loads unlabeled rows and its own label, the latter overriding. So a
     service-scoped requirement is met by an unlabeled row *or* by one carrying that service's
     label - but not by another service's label.

  6. Serilog and YARP index positionally (`Serilog:WriteTo:1:Args:uri`), so those keys carry
     array indices in the middle rather than at the end.

Keeping these in one module means the reconciler and the check can never disagree about what
"present" means.
"""

import json
import re
from typing import Any, Dict, Iterable, List, Optional, Set, Tuple

JSON_CONTENT_TYPES = ("application/json",)
EXCLUDED_CONTENT_TYPES = ("keyvaultref", "appconfig.ff")


def dotted_to_slash(key: str) -> str:
    """Spring property name -> App Configuration key. `a.b.c` becomes `/a/b/c`."""
    return "/" + key.replace(".", "/")


def slash_to_dotted(key: str) -> str:
    """App Configuration key -> Spring property name. `/a/b/c` becomes `a.b.c`."""
    return key.lstrip("/").replace("/", ".")


KEY_VAULT_REF_CONTENT_TYPE = "application/vnd.microsoft.appconfig.keyvaultref+json"


def is_key_vault_ref(content_type: str) -> bool:
    """Whether a row's value is a Key Vault reference rather than a literal.

    The store resolving a reference is the environment's own statement that the value is a
    secret, which is what `sensitive: true` in the catalog is meant to record.
    """
    return KEY_VAULT_REF_CONTENT_TYPE in (content_type or "")


def is_json_blob(content_type: str) -> bool:
    lowered = (content_type or "").lower()
    if any(marker in lowered for marker in EXCLUDED_CONTENT_TYPES):
        return False
    return any(marker in lowered for marker in JSON_CONTENT_TYPES)


def flatten_blob(key: str, value: str) -> List[str]:
    """Child keys a JSON-valued row supplies once the provider flattens it.

    Separator follows the key's own notation: slash keys are Java and yield dotted children,
    colon keys are .NET and yield colon children.
    """
    try:
        parsed = json.loads(value)
    except (json.JSONDecodeError, TypeError):
        return []
    if not isinstance(parsed, (dict, list)):
        return []

    java = key.startswith("/")
    base = slash_to_dotted(key) if java else key
    separator = "." if java else ":"

    out: List[str] = []

    def walk(node: Any, prefix: str) -> None:
        if isinstance(node, dict):
            for name, child in node.items():
                walk(child, prefix + separator + str(name))
        elif isinstance(node, list):
            for index, child in enumerate(node):
                walk(child, prefix + separator + str(index))
        else:
            out.append(prefix)

    walk(parsed, base)
    return out


def build_store_index(items: Iterable[Dict[str, Any]]) -> Dict[str, Set[str]]:
    """Map every key form a store supplies to the labels it supplies it under.

    Includes the children of JSON blobs, since those are real properties as far as any
    consuming service is concerned.
    """
    index: Dict[str, Set[str]] = {}

    def add(key: str, label: str) -> None:
        index.setdefault(key, set()).add(label)

    for item in items:
        key = item.get("key")
        if not isinstance(key, str):
            continue
        label = item.get("label") or ""
        add(key, label)
        # No dotted alias for slash keys here. candidate_forms() already derives the slash
        # form for entries whose runtime is java, so aliasing in the index as well would let
        # a .NET entry be satisfied by a Java row - and the two runtimes read disjoint key
        # spaces. Blob children below are a different case: those really are dotted
        # properties once the provider flattens them.
        if is_json_blob(item.get("content_type") or ""):
            for child in flatten_blob(key, item.get("value") or ""):
                add(child, label)
    return index


ARRAY_INDEX_RE = re.compile(r"(?<=[:.])\d+(?=[:.]|$)")


def relax(key: str) -> str:
    """Canonical form under Spring's relaxed binding.

    Spring treats `telemetry.exporterEndpoint`, `telemetry.exporter-endpoint` and
    `telemetry.exporter_endpoint` as one property. The stores and the code disagree on
    spelling for several keys - `/telemetry/exporterEndpoint` is stored camelCase while
    TelemetryConfig declares `exporter-endpoint` - so comparing them literally reports keys
    as dead when the service reads them perfectly well.
    """
    return key.lower().replace("-", "").replace("_", "")


def normalize_indices(key: str) -> str:
    """Collapse array indices so `X:3` and `X:0` compare equal.

    Store rows carry concrete indices (`CORS:AllowedHeaders:4`) while the inventory records a
    representative element (`CORS:AllowedHeaders:0`). Without this the tail of every array
    looks uncatalogued.
    """
    return ARRAY_INDEX_RE.sub("0", key)


def candidate_forms(key: str, runtime: str) -> List[str]:
    """Every spelling of a catalog key that a store might legitimately hold."""
    forms = [key]
    if runtime == "java":
        forms.append(dotted_to_slash(key))
    return forms


def _index_labels(index: Dict[str, Set[str]], form: str) -> Optional[Set[str]]:
    """Labels holding `form`, allowing for array indices and template placeholders."""
    if form in index:
        return index[form]

    # X is satisfied by X:0, X:1, ... (a stored array)
    prefix = form + ":"
    found: Set[str] = set()
    for key, labels in index.items():
        if key.startswith(prefix) and key[len(prefix):].split(":")[0].isdigit():
            found |= labels
    if found:
        return found

    # {Placeholder} segments stand for any single segment
    if "{" in form:
        escaped = re.escape(form).replace(r"\{", "{").replace(r"\}", "}")
        pattern = re.compile(re.sub(r"\{[^}]*\}", "[^:/]+", escaped))
        for key, labels in index.items():
            if pattern.fullmatch(key):
                found |= labels
        if found:
            return found

    return None


def resolve(key: str, runtime: str, index: Dict[str, Set[str]]) -> Optional[Set[str]]:
    """Labels under which a catalog key is present, or None if absent entirely."""
    labels: Set[str] = set()
    for form in candidate_forms(key, runtime):
        found = _index_labels(index, form)
        if found:
            labels |= found
    return labels or None


def is_satisfied(key: str, runtime: str, index: Dict[str, Set[str]],
                 service_label: Optional[str]) -> bool:
    """Whether a store supplies this key to the service that needs it.

    A global entry (service_label None) is satisfied by a row under any label. A
    service-scoped entry is satisfied by an unlabeled row, which every service loads, or by a
    row carrying that service's own label - mirroring the two Select() calls each service
    issues. Another service's label does not count, because this service never fetches it.
    """
    labels = resolve(key, runtime, index)
    if labels is None:
        return False
    if service_label is None:
        return True
    return "" in labels or service_label in labels


def environment_names(document: Dict[str, Any]) -> List[str]:
    """Environments the catalog declares, in the order it lists them.

    Declared in one place because it was previously a tuple repeated in four scripts, and a
    fourth environment appearing meant finding all four. Each name is the suffix of its export:
    `qa2` is Config/app-config.qa2.json. Callers should let a declared environment with no
    export fail rather than skipping it - a check that silently covers three stores instead of
    four still reports success.
    """
    return list((document.get("environments") or {}).keys())


def environment_store(document: Dict[str, Any], name: str) -> Optional[str]:
    """The Azure resource name for an environment, or None if it is not declared."""
    entry = (document.get("environments") or {}).get(name) or {}
    return entry.get("store")


def missing_export_hint(document: Dict[str, Any], name: str,
                        catalog: str = "app-config.yaml",
                        config_dir: str = "Config") -> str:
    """What to do when a declared environment has no export.

    Every tool here loads the same set of exports, so all of them hit this together. Saying
    only "file not found" leaves the reader to work out that the catalog is what expects it.
    """
    store = environment_store(document, name) or f"the {name} store"
    return (f"{name} is declared under 'environments' in {catalog}, so its export is "
            f"required. Produce it with:\n"
            f"    Scripts\\AzureAppConfig\\export-appconfigs.bat {store} "
            f"{config_dir}\\app-config.{name}.json\n"
            f"Or remove {name} from the catalog if it should not be checked.")


def catalog_entries(document: Dict[str, Any]) -> List[Tuple[str, Dict[str, Any], str, Optional[str]]]:
    """Flatten the catalog into (section, entry, runtime, service_label) tuples."""
    meta = document.get("serviceMeta") or {}
    out: List[Tuple[str, Dict[str, Any], str, Optional[str]]] = []

    for entry in document.get("global") or []:
        if isinstance(entry, dict):
            out.append(("global", entry, entry.get("runtime", "dotnet"), None))

    for service, entries in (document.get("services") or {}).items():
        info = meta.get(service) or {}
        runtime = info.get("runtime", "dotnet")
        label = info.get("label")
        for entry in entries or []:
            if isinstance(entry, dict):
                out.append((service, entry, entry.get("runtime", runtime), label))

    return out
