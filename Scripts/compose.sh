#!/bin/bash
# Runs docker compose from this repo. A shell AZURE_ARTIFACTS_PAT is passed through and
# Azure CLI is not called. Otherwise the token in .azure-artifacts.env is refreshed when
# it has under 10 minutes left, and a root .env value is used if that fetch fails.
# Every argument goes to docker compose unchanged, for example:
# bash ./Scripts/compose.sh build automation-ui
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
