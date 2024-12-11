# Kafka Retry Topic Pattern Documentation

## Overview
The Link platform implements a robust retry mechanism for handling failed Kafka message processing across various services. This pattern uses dedicated retry topics, denoted with a "-Retry" suffix, to manage failed message processing attempts and ensure reliable message handling.

## Implementation Components

### Retry Topics

Retry topics are defined in the `KafkaTopic` enum and follow a consistent naming pattern:
- Original Topic: `{TopicName}`
- Retry Topic: `{TopicName}-Retry`

Example pairs:
- PatientEvent → PatientEvent-Retry
- ReportScheduled → ReportScheduled-Retry
- ResourceAcquired → ResourceAcquired-Retry

### Core Components

#### RetryEntity

The `RetryEntity` class manages retry-related information including:
- Service name
- Facility ID
- Original topic
- Retry topic
- Retry count
- Next retry timestamp

#### RetryListener

A background service that:
- Consumes messages from retry topics
- Implements retry logic with exponential backoff
- Manages retry attempts and dead-letter handling
- Tracks retry counts and scheduling

#### RetryEntityFactory

Creates `RetryEntity` objects by:
- Processing message headers
- Calculating next retry attempts
- Managing retry topic naming
- Tracking retry metadata

## Retry Flow

1. **Initial Processing**
    - Service attempts to process message from primary topic
    - If processing fails, message is sent to corresponding retry topic

2. **Retry Processing**
    - RetryListener consumes from retry topic
    - Validates retry count and timing
    - Attempts reprocessing based on configured retry policy

3. **Retry Outcomes**
    - Success: Message is processed and acknowledged
    - Failure: Message is either:
        - Scheduled for another retry if within retry limits
        - Sent to dead-letter queue if retry limits exceeded

## Configuration

### Retry Settings

The retry settings are found in the `ConsumerSettings` property in each service's app/system configuration:

* DisableRetryConsumer: Disables the consumption of retry events
* RetryDuration: The duration for retry attempts. This is specified in [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601) format and is a list of retries. Each duration entry in the array represents the time to wait each time. If there are three entries, it will retry three times.

### Header Management

Messages in retry topics include headers for:

- Original topic name
- Retry count
- Facility ID
- Service-specific metadata

## Example Retry Topics

Common retry topics in the system include:

- AuditableEventOccurred-Retry
- DataAcquisitionRequested-Retry
- PatientEvent-Retry
- ReportScheduled-Retry
- ResourceAcquired-Retry
- SubmitReport-Retry

Each retry topic corresponds to a primary topic and follows the same processing pattern while maintaining service-specific handling requirements.