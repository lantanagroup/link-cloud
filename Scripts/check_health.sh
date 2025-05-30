#!/bin/bash

# Configurable timeout and interval
PROJECT_NAME=${1:-link}  # Default project name "link"
HEALTH_CHECK_TIMEOUT=${2:-60}  # Default to 60 seconds if not provided
CHECK_INTERVAL=${3:-5}         # Default to 5 seconds if not provided

echo "🔍 Checking health for project: $PROJECT_NAME"
echo "⏳ Waiting up to $HEALTH_CHECK_TIMEOUT seconds for services to become healthy..."

start_time=$(date +%s)
end_time=$((start_time + HEALTH_CHECK_TIMEOUT))

while true; do
  echo "Checking service health status..."
  unhealthy_services=0

  # Get correct container names (avoids JSON formatting issues)
  containers=$(docker ps --filter "name=${PROJECT_NAME}|fhir-" --format '{{.Names}}')
  
  for container in $containers; do
    if [[ "$container" == "${PROJECT_NAME}-admin-ui" ]]; then
      # Skip this container and continue with the next iteration
      continue
    fi

    health_status=$(docker inspect --format '{{.State.Health.Status}}' "$container" 2>/dev/null || echo "no-healthcheck")

    echo "$container health: $health_status"

    if [[ "$health_status" != "healthy" && "$health_status" != "no-healthcheck" ]]; then
      unhealthy_services=$((unhealthy_services + 1))
    fi
  done

  if [[ $unhealthy_services -eq 0 ]]; then
    echo "✅ All services in '$PROJECT_NAME' are healthy!"
    exit 0
  fi

  current_time=$(date +%s)
  
  if [[ $current_time -ge $end_time ]]; then
    echo "❌ Timeout reached! Some services in '$PROJECT_NAME' are still unhealthy."
    exit 1
  fi

  echo "⏳ Waiting for $CHECK_INTERVAL seconds before rechecking..."
  sleep $CHECK_INTERVAL
done
