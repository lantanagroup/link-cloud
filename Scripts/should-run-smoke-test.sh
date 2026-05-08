#/usr/bin/env bash
set -Eeuo pipefail

# Usage:
#   should-run-smoke-test.sh [<diff-range>]
# Examples:
#   should-run-smoke-test.sh "origin/${GITHUB_BASE_REF}...HEAD"
#   should-run-smoke-test.sh "HEAD^ HEAD"
#
# If no arg is provided, it will try to use PR base…HEAD, or fallback to HEAD^…HEAD.

DIFF_RANGE="${1:-}"

if [[ -z "${DIFF_RANGE}" ]]; then
  # Prefer PR base…HEAD if available, else default to previous commit…HEAD
  if [[ -n "${GITHUB_BASE_REF:-}" ]]; then
    DIFF_RANGE="origin/${GITHUB_BASE_REF}...HEAD"
  else
    DIFF_RANGE="HEAD^...HEAD"
  fi
fi

echo "Checking changes in range: ${DIFF_RANGE}" >&2

# Folders where changes ALONE should NOT trigger the job
ALLOWED_PREFIXES=(
  "Azure_Pipelines/"
  "docs/"
  "Scripts/"
  ".github/"
)

# Collect changed files (added/changed/renamed)
mapfile -t FILES < <(git diff --name-only --diff-filter=ACMRD ${DIFF_RANGE})

# Default: skip job if there are zero changes or only allowed folders changed
run_job="false"

if ((${#FILES[@]} == 0)); then
  # no changes → skip
  echo "No changes detected in range ${DIFF_RANGE}." >&2
  echo "run_job=${run_job}"
  exit 0
fi

for file in "${FILES[@]}"; do
  # If file does NOT start with any allowed prefix, we should run the job
  allowed="false"
  for prefix in "${ALLOWED_PREFIXES[@]}"; do
    if [[ "${file}" == ${prefix}* ]]; then
      allowed="true"
      echo "File '${file}' is in allowed prefix '${prefix}'. It does not trigger the job." >&2
      break
    fi
  done

  if [[ "${allowed}" == "false" ]]; then
    echo "File '${file}' is NOT in any allowed prefix. Job must run." >&2
    run_job="true"
    break
  fi
done

if [[ "${run_job}" == "false" ]]; then
  echo "All changed files are in allowed prefixes. Skipping job." >&2
fi

echo "run_job=${run_job}"