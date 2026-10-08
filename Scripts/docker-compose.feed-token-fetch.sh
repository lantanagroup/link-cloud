#!/bin/bash
# Writes a short-lived Azure DevOps token for local docker compose.
# The token is stored only in the gitignored env file. This script does not print it.
set -eu

resource="499b84ac-1321-427f-aa17-267ca6975798"

if [ -n "${1:-}" ]; then
  root=$1
else
  root=$(cd "$(dirname "$0")/.." && pwd)
fi
env_file="$root/.azure-artifacts.env"

fail() {
  printf '%s\n' "$1" >&2
  exit 1
}

find_az() {
  if command -v az >/dev/null 2>&1; then
    command -v az
    return 0
  fi
  if command -v az.cmd >/dev/null 2>&1; then
    command -v az.cmd
    return 0
  fi
  return 1
}

refresh_path() {
  extra=""
  if [ -n "${LOCALAPPDATA:-}" ] && command -v cygpath >/dev/null 2>&1; then
    local_root=$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || true)
    if [ -n "$local_root" ]; then
      extra="$extra:$local_root/Programs/Azure CLI/wbin:$local_root/Microsoft/WindowsApps"
    fi
  fi
  if command -v cygpath >/dev/null 2>&1; then
    pf=$(cygpath -u "${ProgramFiles:-C:\Program Files}" 2>/dev/null || true)
    if [ -n "$pf" ]; then
      extra="$extra:$pf/Microsoft SDKs/Azure/CLI2/wbin"
    fi
  fi
  if [ -n "$extra" ]; then
    PATH="$PATH$extra"
    export PATH
  fi
}

if ! az_bin=$(find_az); then
  winget_bin=""
  if command -v winget >/dev/null 2>&1; then
    winget_bin=$(command -v winget)
  elif command -v winget.exe >/dev/null 2>&1; then
    winget_bin=$(command -v winget.exe)
  fi
  if [ -z "$winget_bin" ]; then
    fail "Azure CLI (az) is not installed and winget is not available, so it cannot be installed for the current user. Install Azure CLI yourself (no admin) from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token-fetch.sh."
  fi
  printf '%s\n' "Azure CLI (az) is not installed. Installing it for the current user with winget." >&2
  set +e
  "$winget_bin" install --id Microsoft.AzureCLI --scope user --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
  winget_code=$?
  set -e
  if [ "$winget_code" -ne 0 ] && [ "$winget_code" -ne 255 ]; then
    # winget's "already installed" code does not always survive Git Bash. A later az lookup decides.
    printf '%s\n' "winget returned $winget_code. Checking whether az is now on PATH." >&2
  fi
  refresh_path
  if ! az_bin=$(find_az); then
    fail "winget could not make Azure CLI available. Install it for the current user from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token-fetch.sh."
  fi
fi

set +e
"$az_bin" account show --output none
show_code=$?
set -e
if [ "$show_code" -ne 0 ]; then
  printf '%s\n' "No Azure CLI session. Starting device-code sign-in. Complete it in a browser, or cancel and this script will stop." >&2
  set +e
  "$az_bin" login --use-device-code
  login_code=$?
  set -e
  if [ "$login_code" -ne 0 ]; then
    fail "Azure sign-in did not complete. Run 'az login' and then Scripts/docker-compose.feed-token-fetch.sh."
  fi
fi

set +e
json=$("$az_bin" account get-access-token --resource "$resource" --output json)
token_code=$?
set -e
if [ "$token_code" -ne 0 ] || [ -z "$json" ]; then
  fail "az account get-access-token failed. Run 'az login' and then Scripts/docker-compose.feed-token-fetch.sh."
fi

token=$(printf '%s\n' "$json" | sed -n 's/.*"accessToken"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)
expires=$(printf '%s\n' "$json" | sed -n 's/.*"expires_on"[[:space:]]*:[[:space:]]*"\{0,1\}\([0-9][0-9]*\)"\{0,1\}.*/\1/p' | head -n 1)
unset json
if [ -z "$token" ] || [ -z "$expires" ]; then
  unset token expires
  fail "az account get-access-token did not return a usable token expiry. Run 'az login' and then Scripts/docker-compose.feed-token-fetch.sh."
fi

umask 077
tmp=$(mktemp)
printf '%s\n' "AZURE_ARTIFACTS_PAT=${token}" "AZURE_ARTIFACTS_PAT_EXPIRES_ON=${expires}" > "$tmp"
chmod 600 "$tmp"
mv "$tmp" "$env_file"
chmod 600 "$env_file"
unset token expires
exit 0
