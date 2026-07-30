"""Validate Azure App Configuration JSON exports for leaked secrets.

The files under Config/ are exports of the Azure App Configuration stores and are
committed to a PUBLIC repository. App Configuration does not prevent anyone from
storing a literal credential, so an export can silently carry one into permanent
git history. This script is the gate in front of that.

Three classes of finding:

  ERROR  A value matches a known credential shape (storage account key, inline
         password, Mongo URI with credentials, PEM block, JWT, ...). These are
         credentials regardless of what the key is called.

  ERROR  A Key Vault reference whose content_type is not the keyvaultref type.
         App Configuration serves such an entry as a literal JSON string instead
         of resolving it, so the service reads '{"uri": "..."}' as its password.
         Silent misconfiguration, not just untidiness.

  WARN   A secret-shaped key (password / secret / connection-string / signing-key
         / webhook / jaas / ...) holding a plain literal rather than a Key Vault
         reference. Not every such value is a credential, so this is surfaced for
         review rather than failing the build. Keys listed in ENDPOINT_ONLY_KEYS
         are exempt: they are named "...ConnectionString" but hold only a bare
         host:port, with the password supplied separately from Key Vault. Those
         keys are still checked for credential shapes, and additionally warn if
         they carry comma-delimited Redis config syntax, which would break
         ConfigurationOptions.EndPoints. Use --strict to make warnings fatal.

Exit code is 0 when no errors are found (and, with --strict, no warnings either).

Usage:
    python Scripts/validate_aac_secrets.py Config/app-config.dev.json
    python Scripts/validate_aac_secrets.py Config/*.json --strict
    python Scripts/validate_aac_secrets.py            # defaults to Config/*.json
"""

import argparse
import glob
import json
import os
import re
import sys
from typing import Any, Dict, List, Tuple

KEY_VAULT_REF_CONTENT_TYPE = "application/vnd.microsoft.appconfig.keyvaultref+json"

# Substrings that mark a key as expected to hold a secret. Matched against the
# whole key with separators stripped, so "ConnectionStrings:DatabaseConnection"
# and "KafkaConnection:SaslPassword" both match despite the secret word sitting
# in different segments.
SECRET_KEY_WORDS = (
    "password",
    "passwd",
    "secret",
    "connectionstring",
    "signingkey",
    "hmackey",
    "apikey",
    "accesskey",
    "accountkey",
    "sastoken",
    "webhook",
    "jaas",
    "credential",
    "privatekey",
)

# Keys exempt from the key-name heuristic above. These name the secret-management
# subsystem itself rather than holding a secret; their values are vault URIs and
# booleans. They remain subject to every value-based check.
SECRET_KEY_WORD_EXEMPT_PREFIXES = (
    "secretmanagement:",
    "/secret-management/",
)

# Keys named "...ConnectionString" that in fact hold only a bare host:port. Both
# are assigned to ConfigurationOptions.EndPoints and paired with a separate
# Key Vault-referenced password:
#   ConnectionStrings:Redis              -> RedisCacheExtension.cs, password from Redis:Password
#   ResourceCache:Redis:ConnectionString -> ResourceCacheExtensions.cs, password from
#                                           ResourceCache:Redis:Password
# A credential here would not merely leak, it would break the connection --
# EndPoints.Add() rejects the comma-delimited StackExchange.Redis config syntax.
# Exempt from the secret-shaped-key warning; every value-based check still applies.
ENDPOINT_ONLY_KEYS = (
    "connectionstrings:redis",
    "resourcecache:redis:connectionstring",
)

# Value shapes that are credentials no matter which key holds them.
CREDENTIAL_VALUE_PATTERNS: Tuple[Tuple[str, str], ...] = (
    (r"AccountKey\s*=\s*[A-Za-z0-9+/]{20,}={0,2}", "Azure Storage account key"),
    (r"(?i)SharedAccessKey\s*=\s*[A-Za-z0-9+/]{20,}={0,2}", "Service Bus / Event Hub shared access key"),
    (r"(?i)SharedAccessSignature\s*=", "Azure SAS token"),
    (r"[?&]sig=[A-Za-z0-9%+/]{20,}", "SAS signature"),
    (r"(?i)\b(?:password|pwd)\s*=\s*[^;,\s\"']{4,}", "inline password in a connection string"),
    (r"(?i)mongodb(?:\+srv)?://[^/\s:@]+:[^@\s]+@", "MongoDB URI with embedded credentials"),
    (r"(?i)amqps?://[^/\s:@]+:[^@\s]+@", "AMQP URI with embedded credentials"),
    (r"(?i)redis://[^/\s:@]+:[^@\s]+@", "Redis URI with embedded credentials"),
    (r"-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----", "PEM private key"),
    (r"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}", "JSON Web Token"),
    (r"(?i)\bBearer\s+[A-Za-z0-9._-]{20,}", "bearer token"),
    (r"(?i)\bapi[_-]?key\s*[=:]\s*[A-Za-z0-9_-]{16,}", "inline API key"),
    # Both shapes are bearer credentials: /services/ is an incoming webhook,
    # /triggers/ is a Workflow Builder trigger. Possession alone allows posting.
    (r"https://hooks\.slack\.com/(?:services|triggers)/[A-Za-z0-9/]{20,}",
     "Slack webhook URL"),
    (r"(?i)\bsasl\.jaas\.config\s*=.*password\s*=", "Kafka JAAS config with inline password"),
)


class Finding:
    """One problem found in one config entry."""

    def __init__(self, severity: str, path: str, index: int, key: str,
                 label: str, message: str):
        self.severity = severity
        self.path = path
        self.index = index
        self.key = key
        self.label = label
        self.message = message

    def format(self) -> str:
        label_str = f" [label: {self.label}]" if self.label else ""
        return (f"  [{self.severity}] {os.path.basename(self.path)} "
                f"(item {self.index}): {self.key}{label_str}\n"
                f"           {self.message}")


def load_config(file_path: str) -> Dict:
    """Load an Azure App Config JSON export from file."""
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File not found: {file_path}", file=sys.stderr)
        sys.exit(2)
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in file {file_path}: {e}", file=sys.stderr)
        sys.exit(2)


def is_key_vault_reference(value: Any) -> bool:
    """Check if value is a JSON object with only a 'uri' field (Key Vault reference)."""
    try:
        obj = json.loads(value)
        return isinstance(obj, dict) and set(obj.keys()) == {"uri"}
    except (json.JSONDecodeError, TypeError):
        return False


def looks_like_vault_uri(value: str) -> bool:
    """Check if a value mentions a Key Vault secret URI in any shape."""
    return isinstance(value, str) and ".vault.azure.net/secrets/" in value


def is_secret_shaped_key(key: str) -> bool:
    """Check if the key name implies it should hold a secret."""
    lowered = key.lower()
    for prefix in SECRET_KEY_WORD_EXEMPT_PREFIXES:
        if lowered.startswith(prefix):
            return False
    squashed = re.sub(r"[:/._\-\s]", "", lowered)
    return any(word in squashed for word in SECRET_KEY_WORDS)


def is_endpoint_only_key(key: str) -> bool:
    """Check if the key holds a bare host:port despite a secret-sounding name."""
    return key.strip().lower() in ENDPOINT_ONLY_KEYS


def is_non_secret_scalar(value: str) -> bool:
    """Check if a value is a boolean or number, which no credential ever is.

    Keeps toggles such as CORS:AllowCredentials -- whose name contains
    'credential' -- out of the secret-shaped-key warning.
    """
    stripped = value.strip().strip('"')
    if stripped.lower() in ("true", "false", "null", ""):
        return True
    try:
        float(stripped)
        return True
    except ValueError:
        return False


def check_item(path: str, index: int, item: Dict) -> List[Finding]:
    """Run every check against a single config entry."""
    findings: List[Finding] = []
    key = item.get("key") or "(missing key)"
    label = item.get("label") or ""
    value = item.get("value")
    content_type = item.get("content_type") or ""

    if not isinstance(value, str):
        return findings

    is_kv_ref = is_key_vault_reference(value)
    declared_kv_ref = KEY_VAULT_REF_CONTENT_TYPE in content_type

    # A Key Vault reference that App Configuration will not resolve, because the
    # content type does not declare it as one. The service receives the literal
    # JSON text instead of the secret.
    if is_kv_ref and not declared_kv_ref:
        findings.append(Finding(
            "ERROR", path, index, key, label,
            f"Value is a Key Vault reference but content_type is '{content_type}'. "
            f"App Configuration will serve the literal JSON string, not the secret. "
            f"Set content_type to '{KEY_VAULT_REF_CONTENT_TYPE};charset=utf-8'."))

    # Declared as a reference but not shaped like one -- resolution will fail.
    if declared_kv_ref and not is_kv_ref:
        findings.append(Finding(
            "ERROR", path, index, key, label,
            "content_type declares a Key Vault reference but the value is not a "
            '{"uri": "..."} object.'))

    # A vault URI smuggled into a value that is not a well-formed reference.
    if looks_like_vault_uri(value) and not is_kv_ref:
        findings.append(Finding(
            "ERROR", path, index, key, label,
            "Value mentions a Key Vault secret URI but is not a well-formed "
            "Key Vault reference."))

    # Credential-shaped values. Skipped for well-formed references, whose value is
    # only a vault URI.
    if not is_kv_ref:
        for pattern, description in CREDENTIAL_VALUE_PATTERNS:
            if re.search(pattern, value):
                findings.append(Finding(
                    "ERROR", path, index, key, label,
                    f"Value looks like a credential ({description}). "
                    f"Move it to Key Vault and reference it instead."))
                break

    # An endpoint-only key carrying StackExchange.Redis configuration syntax.
    # EndPoints.Add() cannot parse it, so this breaks the connection as well as
    # being the shape a pasted credential arrives in.
    if is_endpoint_only_key(key) and "," in value:
        findings.append(Finding(
            "WARN", path, index, key, label,
            f"Value contains ',' but this key is assigned to "
            f"ConfigurationOptions.EndPoints, which accepts only host:port. "
            f"Redis options belong in the sibling settings, not here."))

    # Secret-shaped key holding a literal. Not always a credential, so warn.
    if (is_secret_shaped_key(key) and value.strip() and not is_kv_ref
            and not is_non_secret_scalar(value)
            and not is_endpoint_only_key(key)):
        preview = value if len(value) <= 60 else value[:57] + "..."
        findings.append(Finding(
            "WARN", path, index, key, label,
            f"Key name implies a secret but the value is a literal: '{preview}'. "
            f"Confirm this is not a credential."))

    # value and content_type look transposed -- a real malformed-entry pattern
    # seen in these exports.
    if content_type.strip().lower() in ("true", "false") or (
            not value.strip() and content_type.strip()
            and "/" not in content_type):
        findings.append(Finding(
            "WARN", path, index, key, label,
            f"Malformed entry: value='{value}' content_type='{content_type}'. "
            f"These look transposed."))

    return findings


def check_duplicates(path: str, items: List[Dict]) -> List[Finding]:
    """Report (key, label) pairs defined more than once in one export."""
    findings: List[Finding] = []
    seen: Dict[Tuple[str, str], int] = {}
    for index, item in enumerate(items):
        key = item.get("key") or ""
        label = item.get("label") or ""
        composite = (key, label)
        if composite in seen:
            findings.append(Finding(
                "WARN", path, index, key, label,
                f"Duplicate of item {seen[composite]}; the later value silently wins."))
        else:
            seen[composite] = index
    return findings


def validate_file(path: str) -> List[Finding]:
    """Validate one export file and return every finding."""
    config = load_config(path)
    items = config.get("items", [])
    if not isinstance(items, list):
        return [Finding("ERROR", path, 0, "(root)", "",
                        "Export has no 'items' array.")]

    findings: List[Finding] = []
    for index, item in enumerate(items):
        if isinstance(item, dict):
            findings.extend(check_item(path, index, item))
    findings.extend(check_duplicates(path, items))
    return findings


def resolve_paths(patterns: List[str]) -> List[str]:
    """Expand glob patterns, since the shell may not do it on Windows."""
    resolved: List[str] = []
    for pattern in patterns:
        matches = sorted(glob.glob(pattern))
        if matches:
            resolved.extend(matches)
        else:
            resolved.append(pattern)
    return resolved


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate Azure App Config exports for leaked secrets.")
    parser.add_argument(
        "paths", nargs="*", default=["Config/*.json"],
        help="Export files to validate (default: Config/*.json)")
    parser.add_argument(
        "--strict", action="store_true",
        help="Treat warnings as errors")
    args = parser.parse_args()

    paths = resolve_paths(args.paths or ["Config/*.json"])
    if not paths:
        print("No files to validate.")
        return 0

    all_findings: List[Finding] = []
    for path in paths:
        all_findings.extend(validate_file(path))

    errors = [f for f in all_findings if f.severity == "ERROR"]
    warnings = [f for f in all_findings if f.severity == "WARN"]

    print(f"Scanned {len(paths)} file(s): {', '.join(os.path.basename(p) for p in paths)}")

    if errors:
        print(f"\nERRORS ({len(errors)}):")
        for finding in errors:
            print(finding.format())

    if warnings:
        print(f"\nWARNINGS ({len(warnings)}):")
        for finding in warnings:
            print(finding.format())

    if not all_findings:
        print("\nOK: no secrets or malformed entries found.")
        return 0

    print(f"\nSummary: {len(errors)} error(s), {len(warnings)} warning(s).")

    if errors:
        print("\nA credential in a public repository must be rotated, not just "
              "deleted -- git history is permanent.")
        return 1
    if warnings and args.strict:
        print("\nFailing because --strict treats warnings as errors.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
