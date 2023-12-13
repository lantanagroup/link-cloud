# Link

## Running Kafka

Kafka is a pre-requisite to running any of the services in Link.

Launch Kafka and supporting components in docker using `docker-compose-kafka-and-ui.yml` docker compose file: `docker compose -f docker-compose-kafka-and-ui.yml up -d`

This compose file sets up the following services for Kafka:

* Zookeeper (port 2181)
* Broker (port 29092)
* UI (port 8091)
* REST API (port 38082)