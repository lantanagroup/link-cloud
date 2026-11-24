#!/usr/bin/env bash
# Opt-in disk cleanup for this repository's docker compose project.
#
# Always:
#   docker compose down -v --remove-orphans
#   docker image prune of dangling images labeled for this compose project
#
# Only with --prune-build-cache, or after an interactive yes:
#   docker builder prune --keep-storage 10GB
# The build cache is shared by every project on this Docker daemon.
#
# Does not run docker system prune, docker volume prune, or docker image prune -a.
# Does not remove named images, and does not remove the Aspire AppHost volumes
# (link-aspire-*). Those are reset with the command in DotNet/Link.AspireAppHost/README.md.
#
# Usage, from anywhere:
#   ./Scripts/docker-compose.disk-cleanup.sh
#   ./Scripts/docker-compose.disk-cleanup.sh --prune-build-cache
#   ./Scripts/docker-compose.disk-cleanup.sh --skip-build-cache

set -euo pipefail

prune_build_cache=0
skip_build_cache=0

usage() {
  sed -n '2,20p' "$0"
}

while [ $# -gt 0 ]; do
  case "$1" in
    --prune-build-cache) prune_build_cache=1 ;;
    --skip-build-cache) skip_build_cache=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

if [ "$prune_build_cache" -eq 1 ] && [ "$skip_build_cache" -eq 1 ]; then
  echo 'Pass only one of --prune-build-cache and --skip-build-cache.' >&2
  exit 2
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/docker-compose.yml"
if [ ! -f "$COMPOSE_FILE" ]; then
  echo "docker-compose.yml was not found at $COMPOSE_FILE." >&2
  exit 1
fi
cd "$ROOT"

if ! docker info --format '{{.ServerVersion}}' >/dev/null 2>&1; then
  echo 'Docker is not running. Start Docker, then run this script again.' >&2
  exit 1
fi

config_json="$(docker compose config --format json)"
project="$(printf '%s' "$config_json" | python -c 'import json,sys; print(json.load(sys.stdin).get("name",""))')"
if ! printf '%s' "$project" | grep -Eq '^[a-z0-9][a-z0-9_-]*$'; then
  echo 'Could not read this compose project name. Refusing to prune images without it.' >&2
  exit 1
fi

echo "Compose project: $project"
echo "Compose file: $COMPOSE_FILE"
echo
echo 'Containers:'
docker compose ps --all
echo
echo 'Volumes labeled for this project:'
docker volume ls --filter "label=com.docker.compose.project=$project"

echo
echo 'docker compose down -v --remove-orphans'
docker compose down -v --remove-orphans

echo
echo "docker image prune --force --filter dangling=true --filter label=com.docker.compose.project=$project"
docker image prune --force --filter dangling=true --filter "label=com.docker.compose.project=$project"

if [ "$prune_build_cache" -eq 0 ] && [ "$skip_build_cache" -eq 0 ]; then
  if [ -t 0 ]; then
    echo
    echo 'The Docker build cache is shared by every project on this daemon.'
    printf 'Prune it down to 10GB? [y/N] '
    read -r answer
    case "$answer" in
      y|Y|yes|YES) prune_build_cache=1 ;;
      *) echo 'Leaving the build cache in place.' ;;
    esac
  else
    echo 'Build cache left in place. Re-run with --prune-build-cache to prune it to 10GB.'
  fi
fi

if [ "$prune_build_cache" -eq 1 ]; then
  echo
  echo 'docker builder prune --force --keep-storage 10GB'
  docker builder prune --force --keep-storage 10GB
fi

echo
echo 'Cleanup finished. Named images and other projects were left in place.'
