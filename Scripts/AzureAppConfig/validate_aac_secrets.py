"""Validate Azure App Configuration JSON exports for leaked secrets.

The files it reads are exports of the Azure App Configuration stores, committed to
the private `link-cac` repository (LEGLINK-912 moved them out of this public one).
App Configuration does not prevent anyone from storing a literal credential, so an
export can silently carry one into permanent git history - which no repository's
visibility undoes. This script is the gate in front of that.

It is stdlib-only so it can run anywhere, but in CI it runs only in link-cac,
which checks this repository out for the script. It must NOT be wired into this
repository's CI: two of its warnings quote the offending value, and Actions logs
on a public repository are world-readable, so a finding would publish the very
value it is complaining about. A pull request here cannot change link-cac's
exports in any case.

link-cac has no pre-commit hook, so this is the only gate on that side and it
first fires when a pull request opens.

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
         review rather than failing the build. Keys listed in
         PASSWORDLESS_CONNECTION_STRING_KEYS are exempt: they use a
         comma-delimited Redis connection string with the password supplied
         separately from Key Vault. Keys listed in
         NON_PRODUCTION_FIXTURE_SECRET_KEYS are exempt for a different reason:
         they are fixture credentials for the mock DMRP surface, which is never
         provisioned in a production store. Both sets are still checked for
         credential shapes. Use --strict to make warnings fatal.

Exit code is 0 when no errors are found (and, with --strict, no warnings either).

Usage:
    python Scripts/AzureAppConfig/validate_aac_secrets.py            # link-cac cloned as a sibling
    python Scripts/AzureAppConfig/validate_aac_secrets.py --strict
    python Scripts/AzureAppConfig/validate_aac_secrets.py <path-to>/link-cac/Config/app-config.dev.json
"""

import argparse
import glob
import json
import os
import re
import sys
from typing import Any, Dict, List, Tuple

import config_findings as findings_mod
import config_key_matching as matching
from config_findings import ERROR, WARN, Finding, entry_location

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

# Redis connection strings that carry connection options but pair with a separate
# Key Vault-referenced password:
#   ConnectionStrings:Redis              -> RedisCacheExtension.cs, password from Redis:Password
#   ResourceCache:Redis:ConnectionString -> ResourceCacheExtensions.cs, password from
#                                           ResourceCache:Redis:Password
# Exempt from the secret-shaped-key warning; every value-based check still applies,
# including the inline password check below.
PASSWORDLESS_CONNECTION_STRING_KEYS = (
    "connectionstrings:redis",
    "resourcecache:redis:connectionstring",
)

# Fixture credentials for the mock DMRP surface. These are real secrets in the sense
# that the deployed mock checks them, but they authenticate nothing outside it: the
# upstream DMRP is simulated, and app-config.yaml requires the mock be provisioned
# "never in a production store" (MockDmrpApi:Enabled absent means every route answers
# 503). Rotating them costs a config push and grants an attacker a token the mock
# itself accepts -- no real system trusts it.
#
# Exempt from the secret-shaped-key warning only. Every value-based check still runs,
# so a genuine credential pasted over one of these values is still an ERROR.
#
# Matched on the exact key rather than a "mockdmrpapi:" prefix so that a future
# MockDmrpApi key holding something real is not silently exempted too.
NON_PRODUCTION_FIXTURE_SECRET_KEYS = (
    "mockdmrpapi:authclientsecret",
    "mockdmrpapi:signingkey",
)

# Value shapes that are credentials no matter which key holds them.
CREDENTIAL_VALUE_PATTERNS: Tuple[Tuple[str, str], ...] = (
    (r"AccountKey\s*=\s*[A-Za-z0-9+/]{20,}={0,2}", "Azure Storage account key"),
    (r"(?i)SharedAccessKey\s*=\s*[A-Za-z0-9+/]{20,}={0,2}", "Service Bus / Event Hub shared access key"),
    (r"(?i)SharedAccessSignature\s*=", "Azure SAS token"),
    (r"[?&]sig=[A-Za-z0-9%+/]{20,}", "SAS signature"),
    (r"(?i)\b(?:password|pwd)\s*=\s*[^;,\s\"']+", "inline password in a connection string"),
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


def is_passwordless_connection_string_key(key: str) -> bool:
    """Check if the connection string gets its password from a separate key."""
    return key.strip().lower() in PASSWORDLESS_CONNECTION_STRING_KEYS


def is_non_production_fixture_key(key: str) -> bool:
    """Check if the key holds a fixture credential for a non-production mock."""
    return key.strip().lower() in NON_PRODUCTION_FIXTURE_SECRET_KEYS


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
            "ERROR", entry_location(path, index, key, label),
            f"Value is a Key Vault reference but content_type is '{content_type}'. "
            f"App Configuration will serve the literal JSON string, not the secret. "
            f"Set content_type to '{KEY_VAULT_REF_CONTENT_TYPE};charset=utf-8'."))

    # Declared as a reference but not shaped like one -- resolution will fail.
    if declared_kv_ref and not is_kv_ref:
        findings.append(Finding(
            "ERROR", entry_location(path, index, key, label),
            "content_type declares a Key Vault reference but the value is not a "
            '{"uri": "..."} object.'))

    # A vault URI smuggled into a value that is not a well-formed reference.
    if looks_like_vault_uri(value) and not is_kv_ref:
        findings.append(Finding(
            "ERROR", entry_location(path, index, key, label),
            "Value mentions a Key Vault secret URI but is not a well-formed "
            "Key Vault reference."))

    # Credential-shaped values. Skipped for well-formed references, whose value is
    # only a vault URI.
    if not is_kv_ref:
        for pattern, description in CREDENTIAL_VALUE_PATTERNS:
            if re.search(pattern, value):
                findings.append(Finding(
            "ERROR", entry_location(path, index, key, label),
                    f"Value looks like a credential ({description}). "
                    f"Move it to Key Vault and reference it instead."))
                break

    # Secret-shaped key holding a literal. Not always a credential, so warn.
    if (is_secret_shaped_key(key) and value.strip() and not is_kv_ref
            and not is_non_secret_scalar(value)
            and not is_passwordless_connection_string_key(key)
            and not is_non_production_fixture_key(key)):
        preview = value if len(value) <= 60 else value[:57] + "..."
        findings.append(Finding(
            "WARN", entry_location(path, index, key, label),
            f"Key name implies a secret but the value is a literal: '{preview}'. "
            f"Confirm this is not a credential."))

    # value and content_type look transposed -- a real malformed-entry pattern
    # seen in these exports.
    if content_type.strip().lower() in ("true", "false") or (
            not value.strip() and content_type.strip()
            and "/" not in content_type):
        findings.append(Finding(
            "WARN", entry_location(path, index, key, label),
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
            "WARN", entry_location(path, index, key, label),
                f"Duplicate of item {seen[composite]}; the later value silently wins."))
        else:
            seen[composite] = index
    return findings


def validate_file(path: str) -> List[Finding]:
    """Validate one export file and return every finding."""
    config = findings_mod.load_json_file(path)
    items = config.get("items", [])
    if not isinstance(items, list):
        return [Finding("ERROR", entry_location(path, 0, "(root)"),
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
    # app-config.*.json rather than *.json: link-cac's Config/ also holds the derived
    # config-key-inventory.json, which is not an export and is gitignored, so a bare *.json
    # makes a local run scan a different set of files than CI does.
    default_glob = os.path.join(matching.default_config_dir(), "app-config.*.json")
    parser.add_argument(
        "paths", nargs="*", default=[default_glob],
        help=f"Export files to validate (default: {default_glob}; the directory is also "
             f"settable with LINK_CAC_CONFIG_DIR)")
    parser.add_argument(
        "--strict", action="store_true",
        help="Treat warnings as errors")
    args = parser.parse_args()

    requested = args.paths or [default_glob]
    # resolve_paths hands an unmatched pattern straight back, so validate_file reports it as a
    # missing file. That is the right answer for a mistyped path, but not for the default: it
    # now points at a different repository, and the overwhelmingly likely cause is that link-cac
    # is not checked out beside this one - a gate that did not run rather than one that passed.
    paths = resolve_paths(requested)
    if requested == [default_glob] and not any(os.path.exists(p) for p in paths):
        print(f"Error: no App Configuration exports found at {default_glob}.", file=sys.stderr)
        print("They live in the private link-cac repository. Clone it beside link-cloud, set "
              "LINK_CAC_CONFIG_DIR, or pass the paths explicitly.", file=sys.stderr)
        return findings_mod.EXIT_UNUSABLE

    all_findings: List[Finding] = []
    for path in paths:
        all_findings.extend(validate_file(path))

    return findings_mod.report(
        all_findings,
        headline=(f"Scanned {len(paths)} file(s): "
                  f"{', '.join(os.path.basename(p) for p in paths)}"),
        all_clear="OK: no secrets or malformed entries found.",
        strict=args.strict,
        epilogue=("A leaked credential must be rotated, not just deleted -- git history is "
                  "permanent, and link-cac being private does not change that."))


if __name__ == "__main__":
    sys.exit(main())
