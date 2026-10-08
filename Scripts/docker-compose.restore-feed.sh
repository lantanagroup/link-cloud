#!/bin/sh
# Restore one project against Shared_BOTW_Feed.
# The feed token is the BuildKit secret feed_accesstoken. It is written into a
# temporary NuGet config for this RUN only, then deleted before the layer ends.
set -eu

project="${1:-}"
secret="/run/secrets/feed_accesstoken"

print_missing() {
  printf '%s\n' 'Azure token missing. Run this one-time setup: powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1 -ProfilePath "$PROFILE"'
  printf '%s\n' 'PowerShell profile line: . "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token'
  printf '%s\n' 'Git Bash: bash ./Scripts/docker-compose.feed-token-install.sh'
  printf '%s\n' 'Git Bash profile line: . "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token'
}

if [ -z "$project" ]; then
  echo "docker-compose.restore-feed.sh requires the project path." >&2
  exit 1
fi

if [ ! -s "$secret" ]; then
  print_missing
  exit 1
fi

token=$(cat "$secret")
case "$token" in
  *\"*|*\<*|*\>*|*\&*)
    echo "The Azure DevOps token could not be placed in the NuGet config." >&2
    print_missing
    exit 1
    ;;
esac

cleanup() {
  rm -f nuget.config.feed
}
trap cleanup EXIT

{
  sed '/<\/configuration>/d' nuget.config
  printf '%s\n' '  <packageSourceCredentials>'
  printf '%s\n' '    <Shared_BOTW_Feed>'
  printf '%s\n' '      <add key="Username" value="docker" />'
  printf '      <add key="ClearTextPassword" value="%s" />\n' "$token"
  printf '%s\n' '    </Shared_BOTW_Feed>'
  printf '%s\n' '  </packageSourceCredentials>'
  printf '%s\n' '</configuration>'
} > nuget.config.feed
unset token

rm -rf /root/.nuget/packages/lantanagroup.thetis.generation.abstractions \
       /root/.nuget/packages/lantanagroup.thetis.generation.engine

dotnet restore "$project" \
  --configfile nuget.config.feed \
  /p:RestorePackagesWithLockFile=false
