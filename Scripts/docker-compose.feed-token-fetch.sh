#!/bin/bash
# Writes a short-lived Azure DevOps token for local docker compose.
# The token is stored only in the gitignored env file. This script does not print it.
# An inherited xtrace would print the token. Turn it off for this process only.
set +x
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

on_windows_bash() {
  case "$(uname -s 2>/dev/null || printf '%s' unknown)" in
    MINGW*|MSYS*|CYGWIN*) return 0 ;;
  esac
  return 1
}

# Git Bash chmod does not remove inherited NTFS permissions. The PowerShell
# script creates the file, restricts the ACL, then writes the token.
delegate_to_windows_acl() {
  local ps win_root script code
  ps=$(command -v powershell.exe || true)
  if [ -z "$ps" ]; then
    fail "This is Windows Git Bash. chmod cannot limit the token file to the current user, and powershell.exe was not found. Run Scripts/docker-compose.feed-token.ps1."
  fi
  if ! command -v cygpath >/dev/null 2>&1; then
    fail "This is Windows Git Bash and cygpath is missing, so the token file cannot be locked down. Run Scripts/docker-compose.feed-token.ps1."
  fi
  if [ ! -f "$root/Scripts/docker-compose.feed-token.ps1" ]; then
    fail "Scripts/docker-compose.feed-token.ps1 was not found, so the token file cannot be locked down."
  fi
  win_root=$(cygpath -w "$root")
  script=$(cygpath -w "$root/Scripts/docker-compose.feed-token.ps1")
  set +e
  "$ps" -NoProfile -ExecutionPolicy Bypass -File "$script" -RepoRoot "$win_root"
  code=$?
  set -e
  if [ "$code" -ne 0 ]; then
    fail "The Windows token script failed (exit $code). The token file was not kept."
  fi
  exit 0
}

file_mode_is_600() {
  local mode
  mode=$(stat -c %a "$1" 2>/dev/null || stat -f %Lp "$1" 2>/dev/null || printf '%s' '')
  mode=${mode#0}
  [ "$mode" = "600" ]
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

if on_windows_bash; then
  delegate_to_windows_acl
fi

if ! az_bin=$(find_az); then
  winget_bin=""
  if command -v winget >/dev/null 2>&1; then
    winget_bin=$(command -v winget)
  elif command -v winget.exe >/dev/null 2>&1; then
    winget_bin=$(command -v winget.exe)
  fi
  if [ -z "$winget_bin" ]; then
    fail "Azure CLI (az) is not installed and winget is not available. On Windows, Scripts/docker-compose.feed-token.ps1 installs the per-user ZIP. Otherwise install Azure CLI from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token-fetch.sh."
  fi
  printf '%s\n' "Azure CLI (az) is not installed. Trying winget, which may need an administrator. On Windows, Scripts/docker-compose.feed-token.ps1 installs the per-user ZIP instead." >&2
  set +e
  "$winget_bin" install --exact --id Microsoft.AzureCLI --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
  winget_code=$?
  set -e
  if [ "$winget_code" -ne 0 ] && [ "$winget_code" -ne 255 ]; then
    # winget's "already installed" code does not always survive Git Bash. A later az lookup decides.
    printf '%s\n' "winget returned $winget_code. Checking whether az is now on PATH." >&2
  fi
  refresh_path
  if ! az_bin=$(find_az); then
    fail "winget could not make Azure CLI available. That installer may need an administrator. Install Azure CLI from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token-fetch.sh."
  fi
fi

set +e
"$az_bin" account show --output none
show_code=$?
set -e
if [ "$show_code" -ne 0 ]; then
  printf '%s\n' "No Azure CLI session. Starting device-code sign-in. Complete it in a browser, or cancel and this script will stop." >&2
  set +e
  "$az_bin" login --use-device-code --allow-no-subscriptions
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
# Same directory as the env file, so mv is a rename rather than a cross-filesystem copy.
tmp=$(mktemp "${root}/.azure-artifacts.XXXXXXXXXX")
# A failed mv would otherwise leave the token next to the env file.
trap 'rm -f "$tmp"' EXIT
chmod 600 "$tmp" || true
printf '%s\n' "AZURE_ARTIFACTS_PAT=${token}" "AZURE_ARTIFACTS_PAT_EXPIRES_ON=${expires}" > "$tmp"
unset token expires
if ! file_mode_is_600 "$tmp"; then
  fail "The token file could not be limited to the current user, so it was not kept."
fi
mv "$tmp" "$env_file"
trap - EXIT
chmod 600 "$env_file" || true
if ! file_mode_is_600 "$env_file"; then
  rm -f "$env_file"
  fail "The token file could not be limited to the current user, so it was not kept."
fi
exit 0
