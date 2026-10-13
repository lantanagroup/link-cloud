#!/bin/bash
# Runs docker compose from this repo after making sure the Shared_BOTW_Feed token in
# .azure-artifacts.env exists and has at least 10 minutes left. Every argument goes to
# docker compose unchanged, for example: bash ./Scripts/compose.sh build automation-ui
# AZURE_ARTIFACTS_PAT is set only for the docker process. The token is not printed.
# No profile setup is needed.
set +x
scripts_dir=$(cd "$(dirname "$0")" && pwd)
if [ -z "${LINK_CLOUD_REPO_ROOT_OVERRIDE:-}" ]; then
  LINK_CLOUD_REPO_ROOT_OVERRIDE=$(dirname "$scripts_dir")
fi
export LINK_CLOUD_REPO_ROOT_OVERRIDE
# shellcheck disable=SC1091
. "$scripts_dir/docker-compose.feed-token-profile.sh"
link_cloud_launch compose "$@"
