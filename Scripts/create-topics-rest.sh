#!/bin/bash

set -e

# === Parse Arguments ===
if [[ $# -lt 3 ]]; then
  echo "Usage: $0 REST_PROXY_URL USERNAME PASSWORD [TOPICS_FILE]"
  exit 1
fi

REST_PROXY_URL="$1"
USERNAME="$2"
PASSWORD="$3"
TOPICS_FILE="${4:-topics.txt}"
GROW_PARTITIONS="${5:-false}"

# === Validate topic file ===
if [[ ! -f "$TOPICS_FILE" ]]; then
  echo "ERROR: Topics file not found at: $TOPICS_FILE"
  exit 1
fi

# === Get Kafka Cluster ID ===
echo "Fetching Kafka cluster ID from $REST_PROXY_URL..."

CLUSTER_ID=$(curl -s -u "$USERNAME:$PASSWORD" "$REST_PROXY_URL/v3/clusters" | \
  sed -n 's/.*"cluster_id":"\([^"]*\)".*/\1/p')

if [[ -z "$CLUSTER_ID" ]]; then
  echo "ERROR: Failed to retrieve cluster ID."
  exit 1
fi

echo "Detected Kafka Cluster ID: $CLUSTER_ID"
echo "Using topics file: $TOPICS_FILE"
echo

# === Read and process each line from topics.txt ===
while IFS=: read -r TOPIC PARTITIONS REPLICAS PARAMETERS || [[ -n "$TOPIC" ]]; do
  [[ -z "$TOPIC" ]] && continue  # skip empty lines

  echo "Checking if topic '$TOPIC' exists..."

  STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -u "$USERNAME:$PASSWORD" \
    "$REST_PROXY_URL/topics/$TOPIC")

  echo "HTTP status code for topic '$TOPIC': $STATUS_CODE"

  if [[ "$STATUS_CODE" != "404" ]]; then
    echo "Topic '$TOPIC' already exists."
    if [[ "$GROW_PARTITIONS" == "true" && "$STATUS_CODE" == "200" ]]; then
      LIVE_PARTITIONS=$(curl -s -u "$USERNAME:$PASSWORD" \
        "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics/$TOPIC" \
        | grep -oE '"partitions_count":[0-9]+' | head -n 1 | cut -d: -f2)
      if [[ "$LIVE_PARTITIONS" =~ ^[0-9]+$ && "$PARTITIONS" =~ ^[0-9]+$ && "$LIVE_PARTITIONS" -lt "$PARTITIONS" ]]; then
        echo "Growing '$TOPIC' from $LIVE_PARTITIONS to $PARTITIONS partitions."
        GROW_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
          -u "$USERNAME:$PASSWORD" \
          -X PATCH "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics/$TOPIC" \
          -H "Content-Type: application/json" \
          -d "{\"partitions_count\":$PARTITIONS}")
        if [[ "$GROW_CODE" != "200" && "$GROW_CODE" != "204" ]]; then
          echo "ERROR: Failed to grow '$TOPIC' (HTTP $GROW_CODE)."
          exit 1
        fi
      fi
    fi
  else
    echo "Creating topic '$TOPIC' with $PARTITIONS partitions and $REPLICAS replicas..."
    CREATE_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
      -u "$USERNAME:$PASSWORD" \
      -X POST "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics" \
      -H "Content-Type: application/json" \
      -d "{\"topic_name\": \"$TOPIC\", \"partitions_count\": $PARTITIONS, \"replication_factor\": $REPLICAS}")
    if [[ "$CREATE_CODE" != "200" && "$CREATE_CODE" != "201" ]]; then
      echo "ERROR: Failed to create '$TOPIC' (HTTP $CREATE_CODE)."
      exit 1
    fi
  fi

  echo
done < "$TOPICS_FILE"

RETRY_FILE="$(dirname "$TOPICS_FILE")/kafka-retry-services.txt"
if [[ ! -f "$RETRY_FILE" && -f /kafka-retry-services.txt ]]; then
  RETRY_FILE="/kafka-retry-services.txt"
fi
if [[ ! -f "$RETRY_FILE" ]]; then
  echo "ERROR: kafka-retry-services.txt was not found. Per-service retry topics were not created."
  exit 1
fi
echo "Creating per-service retry and redrive topics from $RETRY_FILE"
  while IFS=: read -r MAIN_TOPIC SERVICES || [[ -n "$MAIN_TOPIC" ]]; do
    [[ -z "$MAIN_TOPIC" || "$MAIN_TOPIC" =~ ^# ]] && continue
    MAIN_PARTITIONS=$(awk -F: -v t="$MAIN_TOPIC" '$1==t {print $2; exit}' "$TOPICS_FILE")
    MAIN_REPLICAS=$(awk -F: -v t="$MAIN_TOPIC" '$1==t {print $3; exit}' "$TOPICS_FILE")
    MAIN_PARTITIONS=${MAIN_PARTITIONS:-3}
    MAIN_REPLICAS=${MAIN_REPLICAS:-1}
    IFS=',' read -ra SERVICE_LIST <<< "$SERVICES"
    for SERVICE in "${SERVICE_LIST[@]}"; do
      # retry-service-parse: begin
      SERVICE="${SERVICE#"${SERVICE%%[![:space:]]*}"}"
      SERVICE="${SERVICE%"${SERVICE##*[![:space:]]}"}"
      SUFFIXES=(Retry Redrive)
      if [[ "$SERVICE" == "~"* ]]; then
        SERVICE="${SERVICE#"~"}"
        SUFFIXES=(Redrive)
      fi
      if [[ -z "$SERVICE" || "$SERVICE" == *"~"* ]]; then
        echo "ERROR: invalid service for topic '$MAIN_TOPIC'." >&2
        exit 1
      fi
      # retry-service-parse: end
      for SUFFIX in "${SUFFIXES[@]}"; do
        DERIVED="${MAIN_TOPIC}-${SUFFIX}-${SERVICE}"
        DERIVED_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
          -u "$USERNAME:$PASSWORD" \
          "$REST_PROXY_URL/topics/$DERIVED")
        if [[ "$DERIVED_CODE" == "404" ]]; then
          echo "Creating '$DERIVED'."
          CREATE_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
            -u "$USERNAME:$PASSWORD" \
            -X POST "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics" \
            -H "Content-Type: application/json" \
            -d "{\"topic_name\":\"$DERIVED\",\"partitions_count\":$MAIN_PARTITIONS,\"replication_factor\":$MAIN_REPLICAS}")
          if [[ "$CREATE_CODE" != "200" && "$CREATE_CODE" != "201" ]]; then
            echo "ERROR: Failed to create '$DERIVED' (HTTP $CREATE_CODE)."
            exit 1
          fi
        elif [[ "$DERIVED_CODE" == "200" ]]; then
          echo "Topic '$DERIVED' already exists."
          if [[ "$GROW_PARTITIONS" == "true" ]]; then
            LIVE_DERIVED=$(curl -s -u "$USERNAME:$PASSWORD" \
              "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics/$DERIVED" \
              | grep -oE '"partitions_count":[0-9]+' | head -n 1 | cut -d: -f2)
            if [[ "$LIVE_DERIVED" =~ ^[0-9]+$ && "$MAIN_PARTITIONS" =~ ^[0-9]+$ && "$LIVE_DERIVED" -lt "$MAIN_PARTITIONS" ]]; then
              echo "Growing '$DERIVED' from $LIVE_DERIVED to $MAIN_PARTITIONS partitions."
              GROW_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
                -u "$USERNAME:$PASSWORD" \
                -X PATCH "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics/$DERIVED" \
                -H "Content-Type: application/json" \
                -d "{\"partitions_count\":$MAIN_PARTITIONS}")
              if [[ "$GROW_CODE" != "200" && "$GROW_CODE" != "204" ]]; then
                echo "ERROR: Failed to grow '$DERIVED' (HTTP $GROW_CODE)."
                exit 1
              fi
            fi
          fi
        else
          echo "ERROR: Unexpected status $DERIVED_CODE checking topic '$DERIVED'."
          exit 1
        fi
      done
    done
  done < "$RETRY_FILE"