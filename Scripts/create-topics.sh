#!/bin/bash
# Ex URL: http://kafka.test.local/v3/clusters/<cluster_id>/topics
# Example:
# ./create-topics.sh http://kafka.test.local <cluster_id> <user> <pass> [partitions] [replication_factor]

# Check if minimum required parameters are provided
if [ "$#" -lt 4 ]; then
  echo "Usage: $0 <kafka-rest-url> <cluster-id> <username> <password> [partitions] [replication_factor]"
  exit 1
fi

# Required arguments
KAFKA_REST_URL=$1
CLUSTER_ID=$2
USERNAME=$3
PASSWORD=$4

# Optional arguments with defaults
PARTITIONS=${5:-5}
REPLICATION_FACTOR=${6:-2}

# Function to create a Kafka topic
create_topic() {
  local topic_name=$1
  local retention_ms=259200000
  local retention_bytes=1073741824

  # Payload for creating a topic
  local payload=$(cat <<EOF
{
  "topic_name": "$topic_name",
  "partitions_count": $PARTITIONS,
  "replication_factor": $REPLICATION_FACTOR,
  "configs": [
    {
      "name": "retention.ms",
      "value": "$retention_ms"
    },
    {
      "name": "retention.bytes",
      "value": "$retention_bytes"
    }
  ]
}
EOF
)

  # Make the POST request to create the topic
  response=$(curl -s -L -w "\n%{http_code}\n" -X POST \
    -H "Content-Type: application/json" \
    -u "$USERNAME:$PASSWORD" \
    -d "$payload" \
    "$KAFKA_REST_URL/v3/clusters/$CLUSTER_ID/topics")
  
  http_code=$(echo "$response" | tail -n1)
  response_body=$(echo "$response" | head -n -1)

  if [[ "$http_code" -eq 200 || "$http_code" -eq 201 ]]; then
    echo "Topic '$topic_name' created successfully."
  else
    echo "Failed to create topic '$topic_name'. HTTP status code: $http_code"
    echo "Response: $response_body"
  fi
}

# List of topics to create
topics=(
  "ReportScheduled"
  "ReportScheduled-Error"
  "ReportScheduled-Retry"
  "RetentionCheckScheduled"
  "RetentionCheckScheduled-Error"
  "RetentionCheckScheduled-Retry"
  "PatientCensusScheduled"
  "PatientCensusScheduled-Error"
  "PatientCensusScheduled-Retry"
  "PatientEvent"
  "PatientEvent-Error"
  "PatientEvent-Retry"
  "DataAcquisitionRequested"
  "DataAcquisitionRequested-Error"
  "DataAcquisitionRequested-Retry"
  "PatientIDsAcquired"
  "PatientIDsAcquired-Error"
  "PatientIDsAcquired-Retry"
  "ResourceAcquired"
  "ResourceAcquired-Error"
  "ResourceAcquired-Retry"
  "ResourceNormalized"
  "ResourceNormalized-Error"
  "ResourceNormalized-Retry"
  "ResourceEvaluated"
  "ResourceEvaluated-Error"
  "ResourceEvaluated-Retry"
  "SubmitReport"
  "SubmitReport-Error"
  "SubmitReport-Retry"
  "ReportSubmitted"
  "ReportSubmitted-Error"
  "ReportSubmitted-Retry"
  "NotificationRequested"
  "NotificationRequested-Error"
  "NotificationRequested-Retry"
  "AuditableEventOccurred"
  "AuditableEventOccurred-Error"
  "AuditableEventOccurred-Retry"
)

# Create each topic
for topic in "${topics[@]}"; do
  create_topic "$topic"
done