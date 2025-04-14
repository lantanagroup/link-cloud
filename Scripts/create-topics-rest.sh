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
while IFS=: read -r TOPIC PARTITIONS REPLICAS; do
  [[ -z "$TOPIC" ]] && continue  # skip empty lines

  echo "Checking if topic '$TOPIC' exists..."

  STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -u "$USERNAME:$PASSWORD" \
    "$REST_PROXY_URL/topics/$TOPIC")

  echo "HTTP status code for topic '$TOPIC': $STATUS_CODE"

  if [[ "$STATUS_CODE" != "404" ]]; then
    echo "Topic '$TOPIC' already exists."
  else
    echo "Creating topic '$TOPIC' with $PARTITIONS partitions and $REPLICAS replicas..."
    curl -s -u "$USERNAME:$PASSWORD" \
      -X POST "$REST_PROXY_URL/v3/clusters/$CLUSTER_ID/topics" \
      -H "Content-Type: application/json" \
      -d "{\"topic_name\": \"$TOPIC\", \"partitions_count\": $PARTITIONS, \"replication_factor\": $REPLICAS}"
  fi

  echo
done < "$TOPICS_FILE"