#!/bin/bash
# Shell functions for local docker compose feed tokens.
# `docker compose` inside this repo refreshes .azure-artifacts.env when the
# token is missing or has less than 10 minutes left, then runs the real
# docker binary with AZURE_ARTIFACTS_PAT set only for that child process.

link_cloud_missing_message() {
  printf '%s\n' 'Azure token missing. Run this one-time setup: powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1'
  printf '%s\n' 'PowerShell profile line: . "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token'
  printf '%s\n' 'Git Bash: bash ./Scripts/docker-compose.feed-token-install.sh'
  printf '%s\n' 'Git Bash profile line: . "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token'
}

find_link_cloud_root() {
  local dir parent
  if [ -n "${LINK_CLOUD_REPO_ROOT_OVERRIDE+x}" ]; then
    if [ -z "$LINK_CLOUD_REPO_ROOT_OVERRIDE" ]; then
      return 1
    fi
    printf '%s\n' "$LINK_CLOUD_REPO_ROOT_OVERRIDE"
    return 0
  fi
  dir=$(pwd)
  while [ -n "$dir" ] && [ "$dir" != "/" ]; do
    if [ -f "$dir/docker-compose.yml" ] && [ -f "$dir/Scripts/docker-compose.feed-token.ps1" ]; then
      printf '%s\n' "$dir"
      return 0
    fi
    parent=$(dirname "$dir")
    if [ "$parent" = "$dir" ]; then
      break
    fi
    dir=$parent
  done
  return 1
}

if [ -z "${LINK_CLOUD_DOCKER_EXE:-}" ]; then
  LINK_CLOUD_DOCKER_EXE=$(command -v docker || true)
fi

link_cloud_now() {
  if [ -n "${LINK_CLOUD_NOW_EPOCH:-}" ]; then
    printf '%s\n' "$LINK_CLOUD_NOW_EPOCH"
    return 0
  fi
  date +%s
}

# One read, so a refresh cannot pair an old token with a new expiry.
link_cloud_load_token() {
  local file=$1
  local line
  token=""
  expires=""
  if [ ! -f "$file" ]; then
    return 1
  fi
  while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in
      AZURE_ARTIFACTS_PAT_EXPIRES_ON=*) expires=${line#AZURE_ARTIFACTS_PAT_EXPIRES_ON=} ;;
      AZURE_ARTIFACTS_PAT=*) token=${line#AZURE_ARTIFACTS_PAT=} ;;
    esac
  done < "$file"
}

link_cloud_fetch() {
  local root=$1
  local fetch
  if [ -n "${LINK_CLOUD_FETCH_SCRIPT:-}" ]; then
    bash "$LINK_CLOUD_FETCH_SCRIPT" "$root"
    return $?
  fi
  fetch="$root/Scripts/docker-compose.feed-token-fetch.sh"
  if [ ! -f "$fetch" ]; then
    printf '%s\n' "The feed token script was not found at $fetch." >&2
    return 1
  fi
  bash "$fetch" "$root"
  return $?
}

link_cloud_token_fresh() {
  local token=$1
  local expires=$2
  local now=$3
  local remaining
  if [ -z "$token" ] || [ -z "$expires" ]; then
    return 1
  fi
  remaining=$((expires - now))
  [ "$remaining" -ge 600 ]
}

# A refresh can return the cached token while it is still valid.
# That result is usable. The 10 minute check only decides whether to try.
link_cloud_token_unexpired() {
  local token=$1
  local expires=$2
  local now=$3
  local remaining
  if [ -z "$token" ] || [ -z "$expires" ]; then
    return 1
  fi
  remaining=$((expires - now))
  [ "$remaining" -gt 0 ]
}

link_cloud_is_compose() {
  local expect_value=0
  local arg
  for arg in "$@"; do
    if [ "$expect_value" -eq 1 ]; then
      expect_value=0
      continue
    fi
    case "$arg" in
      compose) return 0 ;;
      --) return 1 ;;
      --context|--config|--host|--log-level|--tlscacert|--tlscert|--tlskey|-c|-H|-l)
        expect_value=1
        ;;
      --*=*|-c*|-H*|-l*) ;;
      -*) ;;
      *) return 1 ;;
    esac
  done
  return 1
}

link_cloud_launch() {
  local root envfile now token expires
  root=$(find_link_cloud_root) || {
    printf '%s\n' "compose is only available inside the link-cloud repo." >&2
    return 1
  }
  envfile="$root/.azure-artifacts.env"
  now=$(link_cloud_now)
  token=""
  expires=""
  if [ -f "$envfile" ]; then
    link_cloud_load_token "$envfile"
  fi
  if ! link_cloud_token_fresh "$token" "$expires" "$now"; then
    if ! link_cloud_fetch "$root"; then
      link_cloud_missing_message >&2
      return 1
    fi
    if [ ! -f "$envfile" ]; then
      link_cloud_missing_message >&2
      return 1
    fi
    token=""
    expires=""
    link_cloud_load_token "$envfile"
    now=$(link_cloud_now)
    if ! link_cloud_token_unexpired "$token" "$expires" "$now"; then
      link_cloud_missing_message >&2
      return 1
    fi
  fi
  if [ -z "${LINK_CLOUD_DOCKER_EXE:-}" ]; then
    printf '%s\n' "docker was not found on PATH." >&2
    return 1
  fi
  AZURE_ARTIFACTS_PAT="$token" "$LINK_CLOUD_DOCKER_EXE" "$@"
  return $?
}

compose() {
  link_cloud_launch compose "$@"
}

docker() {
  local root repo_profile current repo_full
  if [ -z "${LINK_CLOUD_SKIP_RELOAD:-}" ]; then
    root=$(find_link_cloud_root || true)
    if [ -n "$root" ]; then
      repo_profile="$root/Scripts/docker-compose.feed-token-profile.sh"
      current=""
      if [ -n "${BASH_SOURCE[0]:-}" ]; then
        current=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")
      fi
      if [ -f "$repo_profile" ] && [ -n "$current" ]; then
        repo_full=$(cd "$(dirname "$repo_profile")" && pwd)/$(basename "$repo_profile")
        if [ "$repo_full" != "$current" ]; then
          # shellcheck disable=SC1090
          . "$repo_profile"
          docker "$@"
          return $?
        fi
      fi
    fi
  fi

  if link_cloud_is_compose "$@"; then
    root=$(find_link_cloud_root || true)
    if [ -n "$root" ]; then
      link_cloud_launch "$@"
      return $?
    fi
  fi
  if [ -z "${LINK_CLOUD_DOCKER_EXE:-}" ]; then
    printf '%s\n' "docker was not found on PATH." >&2
    return 1
  fi
  "$LINK_CLOUD_DOCKER_EXE" "$@"
  return $?
}
