#!/bin/bash
# Adds one idempotent line to the bash startup file and installs the docker compose wrapper.
set -eu

source_root=$(cd "$(dirname "$0")" && pwd)
install_dir=${LINK_CLOUD_INSTALL_DIR:-"$HOME/.link-cloud"}
profile_path=${LINK_CLOUD_PROFILE_PATH:-"$HOME/.bashrc"}
line='. "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token'

mkdir -p "$install_dir"
cp "$source_root/docker-compose.feed-token-profile.sh" "$install_dir/docker-compose.feed-token.sh"

if [ -f "$profile_path" ] && grep -q 'link-cloud-feed-token' "$profile_path"; then
  printf '%s\n' "The profile line is already in $profile_path."
else
  if [ -f "$profile_path" ] && [ -s "$profile_path" ]; then
    printf '\n%s\n' "$line" >> "$profile_path"
  else
    printf '%s\n' "$line" >> "$profile_path"
  fi
  printf '%s\n' "Added the profile line to $profile_path."
fi

printf '%s\n' "Open a new Git Bash window in the repo. docker compose will fetch a short-lived Azure DevOps token when it needs one."
exit 0
