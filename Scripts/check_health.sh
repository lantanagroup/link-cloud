#!/bin/bash

# Configurable timeout and interval
PROJECT_NAME=${1:-link}  # Default project name "link"
HEALTH_CHECK_TIMEOUT=${2:-60}  # Default to 60 seconds if not provided
CHECK_INTERVAL=${3:-5}         # Default to 5 seconds if not provided

echo "🔍 Checking health for project: $PROJECT_NAME"
echo "⏳ Waiting up to $HEALTH_CHECK_TIMEOUT seconds for services to become healthy..."

# Ensure we have a directory to store logs if things go wrong
rm -rf service-logs
mkdir -p service-logs

start_time=$(date +%s)
end_time=$((start_time + HEALTH_CHECK_TIMEOUT))

while true; do
  echo "Checking service health status..."
  unhealthy_services=0
  unhealthy_services_list=()

  # Get correct container names (avoids JSON formatting issues)
  containers=$(docker ps --filter "name=${PROJECT_NAME}|fhir-" --format '{{.Names}}')
  
  for container in $containers; do
    health_status=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}healthy{{end}}' "$container" 2>/dev/null)
    
    # Fallback to 'healthy' if empty (no health check defined)
    if [[ -z "$health_status" ]]; then
      health_status="healthy"
    fi

    echo "$container health: $health_status"

    if [[ "$health_status" != "healthy" ]]; then
      unhealthy_services=$((unhealthy_services + 1))
      unhealthy_services_list+=("$container")
    fi
  done

  if [[ $unhealthy_services -eq 0 ]]; then
    echo "✅ All services in '$PROJECT_NAME' are healthy!"
    exit 0
  fi

  current_time=$(date +%s)
  
  if [[ $current_time -ge $end_time ]]; then
    echo "❌ Timeout reached! Some services in '$PROJECT_NAME' are still unhealthy."

    for container in "${unhealthy_services_list[@]}"; do
      echo "------------------------------------------------"
      echo "🔍 DUMPING LOGS FOR UNHEALTHY SERVICE: $container"
      docker logs "$container" > "service-logs/${container}.log" 2>&1
      cat "service-logs/${container}.log"
      
      echo ""
      echo "📋 DOCKER INSPECT (Health State): $container"
      docker inspect --format '{{json .State.Health}}' "$container"
      echo "------------------------------------------------"
    done

    exit 1
  fi

  echo "⏳ Waiting for $CHECK_INTERVAL seconds before rechecking..."
  sleep $CHECK_INTERVAL
done
