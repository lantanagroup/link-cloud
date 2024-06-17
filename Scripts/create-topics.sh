#!/bin/bash

# Check if the correct number of arguments are provided
if [ "$#" -ne 3 ]; then
    echo "Usage: $0 REST_PROXY_URL USERNAME PASSWORD"
    exit 1
fi

# Assign arguments to variables
REST_PROXY_URL=$1
USERNAME=$2
PASSWORD=$3

# Kafka configuration
PARTITIONS=1
REPLICATION_FACTOR=1

# List of topics to create
TOPICS=(
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

# Check if topic exists and create if it does not
for TOPIC in "${TOPICS[@]}"; do
    echo "Checking if topic: $TOPIC exists"
    STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" -u $USERNAME:$PASSWORD "$REST_PROXY_URL/topics/$TOPIC")

    if [ "$STATUS_CODE" -eq 404 ]; then
        echo "Creating topic: $TOPIC"
        curl -X POST "$REST_PROXY_URL/topics/$TOPIC" \
            -H "Content-Type: application/vnd.kafka.v2+json" \
            -u $USERNAME:$PASSWORD \
            -d "{\"partitions\": $PARTITIONS, \"replication_factor\": $REPLICATION_FACTOR}"
    else
        echo "Topic $TOPIC already exists."
    fi
    echo
done
