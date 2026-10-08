#!/bin/bash
# Bash wrapper tests. They mock az and docker. They do not need a network.
set -u
passed=0
failed=0
sentinel='unit-test-feed-token-sentinel'
root=$(cd "$(dirname "$0")" && pwd)
repo=$(mktemp -d)
log="$repo/docker-log"

pass() { passed=$((passed + 1)); printf '%s\n' "PASS $1"; }
fail() { failed=$((failed + 1)); printf '%s\n' "FAIL $1"; }

cleanup() { rm -rf "$repo"; }
trap cleanup EXIT

cat > "$repo/docker-mock" <<'EOF'
#!/bin/bash
printf 'ARGC:%s\n' "$#" >> "$LINK_CLOUD_MOCK_LOG"
i=1
for arg in "$@"; do
  printf 'ARG:%s\n' "$arg" >> "$LINK_CLOUD_MOCK_LOG"
  i=$((i + 1))
done
if [ -n "${AZURE_ARTIFACTS_PAT:-}" ]; then
  if [ "$AZURE_ARTIFACTS_PAT" = "$LINK_CLOUD_MOCK_SENTINEL" ]; then
    printf '%s\n' 'TOKEN:match' >> "$LINK_CLOUD_MOCK_LOG"
  else
    printf '%s\n' 'TOKEN:other' >> "$LINK_CLOUD_MOCK_LOG"
  fi
else
  printf '%s\n' 'TOKEN:absent' >> "$LINK_CLOUD_MOCK_LOG"
fi
exit "${LINK_CLOUD_MOCK_EXIT:-0}"
EOF
chmod +x "$repo/docker-mock"

cat > "$repo/fetch-ok" <<EOF
#!/bin/bash
printf '%s\n' "AZURE_ARTIFACTS_PAT=${sentinel}" "AZURE_ARTIFACTS_PAT_EXPIRES_ON=\${LINK_CLOUD_MOCK_EXPIRES}" > "\$1/.azure-artifacts.env"
chmod 600 "\$1/.azure-artifacts.env"
exit 0
EOF
chmod +x "$repo/fetch-ok"

cat > "$repo/fetch-fail" <<'EOF'
#!/bin/bash
printf '%s\n' 'mock fetch failed' >&2
exit 1
EOF
chmod +x "$repo/fetch-fail"

export LINK_CLOUD_DOCKER_EXE="$repo/docker-mock"
export LINK_CLOUD_MOCK_LOG="$log"
export LINK_CLOUD_MOCK_SENTINEL="$sentinel"
export LINK_CLOUD_SKIP_RELOAD=1
export LINK_CLOUD_NOW_EPOCH=1700000000
unset LINK_CLOUD_REPO_ROOT_OVERRIDE || true
unset AZURE_ARTIFACTS_PAT || true

# shellcheck disable=SC1091
. "$root/docker-compose.feed-token-profile.sh"

reset_log() { : > "$log"; }

# Missing file and fetch failure.
reset_log
export LINK_CLOUD_REPO_ROOT_OVERRIDE="$repo"
export LINK_CLOUD_FETCH_SCRIPT="$repo/fetch-fail"
rm -f "$repo/.azure-artifacts.env"
set +e
docker compose up >"$repo/out.txt" 2>"$repo/err.txt"
code=$?
set -e
err=$(cat "$repo/err.txt")
if [ "$code" -ne 0 ]; then pass 'missing file fetch failure is non-zero'; else fail 'missing file fetch failure is non-zero'; fi
if [ ! -s "$log" ]; then pass 'missing file fetch failure does not call docker'; else fail 'missing file fetch failure does not call docker'; fi
printf '%s' "$err" | grep -F 'Azure token missing. Run this one-time setup: powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1' >/dev/null && pass 'bash missing message has the PowerShell setup line' || fail 'bash missing message has the PowerShell setup line'
printf '%s' "$err" | grep -F 'Git Bash: bash ./Scripts/docker-compose.feed-token-install.sh' >/dev/null && pass 'bash missing message has the Git Bash setup line' || fail 'bash missing message has the Git Bash setup line'
printf '%s' "$err" | grep -F 'link-cloud-feed-token' >/dev/null && pass 'bash missing message has a profile line' || fail 'bash missing message has a profile line'
printf '%s' "$err" | grep -F "$sentinel" >/dev/null && fail 'bash missing message does not contain the token' || pass 'bash missing message does not contain the token'

# Valid token, spaces, exit code, no fetch.
reset_log
export LINK_CLOUD_MOCK_EXIT=9
export LINK_CLOUD_FETCH_SCRIPT="$repo/fetch-fail"
printf '%s\n' "AZURE_ARTIFACTS_PAT=${sentinel}" 'AZURE_ARTIFACTS_PAT_EXPIRES_ON=1700003600' > "$repo/.azure-artifacts.env"
set +e
docker compose up 'my service' >"$repo/out.txt" 2>"$repo/err.txt"
code=$?
set -e
if [ "$code" -eq 9 ]; then pass 'bash docker exit code is propagated'; else fail 'bash docker exit code is propagated'; fi
grep -F 'ARG:compose' "$log" >/dev/null && grep -F 'ARG:up' "$log" >/dev/null && grep -F 'ARG:my service' "$log" >/dev/null && pass 'bash args with spaces are passed through' || fail 'bash args with spaces are passed through'
grep -F 'TOKEN:match' "$log" >/dev/null && pass 'bash token is visible to the docker child' || fail 'bash token is visible to the docker child'
if [ -z "${AZURE_ARTIFACTS_PAT:-}" ]; then pass 'bash token is not kept in the parent shell'; else fail 'bash token is not kept in the parent shell'; fi
calls=$(grep -c '^ARGC:' "$log")
if [ "$calls" -eq 1 ]; then pass 'bash docker is invoked once'; else fail 'bash docker is invoked once'; fi

# Near expiry refreshes. fetch-ok writes a fresh expiry.
reset_log
unset LINK_CLOUD_MOCK_EXIT || true
export LINK_CLOUD_MOCK_EXIT=0
export LINK_CLOUD_MOCK_EXPIRES=1700003600
export LINK_CLOUD_FETCH_SCRIPT="$repo/fetch-ok"
printf '%s\n' "AZURE_ARTIFACTS_PAT=${sentinel}" 'AZURE_ARTIFACTS_PAT_EXPIRES_ON=1700000599' > "$repo/.azure-artifacts.env"
before=$(wc -c < "$repo/.azure-artifacts.env" | tr -d ' ')
set +e
docker compose ps >"$repo/out.txt" 2>"$repo/err.txt"
code=$?
set -e
after=$(sed -n 's/^AZURE_ARTIFACTS_PAT_EXPIRES_ON=//p' "$repo/.azure-artifacts.env")
if [ "$code" -eq 0 ] && [ "$after" = "1700003600" ]; then pass 'bash near-expiry token refreshes'; else fail 'bash near-expiry token refreshes'; fi

# Outside the repo.
reset_log
export LINK_CLOUD_REPO_ROOT_OVERRIDE=""
export LINK_CLOUD_FETCH_SCRIPT="$repo/fetch-fail"
set +e
docker compose logs >"$repo/out.txt" 2>"$repo/err.txt"
code=$?
set -e
grep -F 'ARG:compose' "$log" >/dev/null && grep -F 'ARG:logs' "$log" >/dev/null && pass 'bash outside the repo passes compose through' || fail 'bash outside the repo passes compose through'
grep -F 'TOKEN:absent' "$log" >/dev/null && pass 'bash outside the repo does not set the token' || fail 'bash outside the repo does not set the token'

# Non-compose.
reset_log
export LINK_CLOUD_REPO_ROOT_OVERRIDE="$repo"
export LINK_CLOUD_MOCK_EXIT=4
set +e
docker version >"$repo/out.txt" 2>"$repo/err.txt"
code=$?
set -e
if [ "$code" -eq 4 ]; then pass 'bash non-compose exit code is propagated'; else fail 'bash non-compose exit code is propagated'; fi
grep -F 'ARG:version' "$log" >/dev/null && pass 'bash non-compose command is passed through' || fail 'bash non-compose command is passed through'
grep -F 'TOKEN:absent' "$log" >/dev/null && pass 'bash non-compose command does not set the token' || fail 'bash non-compose command does not set the token'

# Installer idempotence.
install_dir="$repo/home-link"
profile_path="$repo/bashrc"
export LINK_CLOUD_INSTALL_DIR="$install_dir"
export LINK_CLOUD_PROFILE_PATH="$profile_path"
bash "$root/docker-compose.feed-token-install.sh" >"$repo/install-out.txt"
bash "$root/docker-compose.feed-token-install.sh" >"$repo/install-out.txt"
count=$(grep -c 'link-cloud-feed-token' "$profile_path")
if [ "$count" -eq 1 ]; then pass 'bash installer adds the profile line once'; else fail 'bash installer adds the profile line once'; fi
grep -F '. "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token' "$profile_path" >/dev/null && pass 'bash installer writes the profile snippet' || fail 'bash installer writes the profile snippet'
if [ -f "$install_dir/docker-compose.feed-token.sh" ]; then pass 'bash installer copies the profile script'; else fail 'bash installer copies the profile script'; fi

# Output files must not contain the sentinel.
if grep -F "$sentinel" "$repo/out.txt" "$repo/err.txt" "$repo/install-out.txt" "$log" >/dev/null 2>&1; then
  fail 'bash test output does not contain the token'
else
  pass 'bash test output does not contain the token'
fi

printf '%s\n' "PASSED=$passed FAILED=$failed TOTAL=$((passed + failed))"
if [ "$failed" -ne 0 ]; then
  exit 1
fi
exit 0
