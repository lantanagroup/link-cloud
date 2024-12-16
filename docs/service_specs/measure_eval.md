[← Back Home](../README.md)

## Measure Eval Overview

The Measure Eval service is a Java based application that is primarily responsible for evaluating bundles of acquired patient resources against the measures that Link Cloud tenants are configured to evaluate with. The service utilizes the [CQF framework](https://github.com/cqframework/cqf-ruler) to perform the measure evaluations.

- **Technology**: .NET Core 8
- **Image Name**: link-measureeval
- **Port**: 8080
- **Database**: Mongo

## Environment Variables

| Name                                        | Secret? | Description                                                   |
|---------------------------------------------|---------|---------------------------------------------------------------|
| SPRING_CLOUD_AZURE_APPCONFIGURATION_ENABLED | No      | Boolean value to enable or disable use of Azure App Config    |
| AZURE_APP_CONFIG_ENDPOINT                   | No      | If App Config enabled, the URI to the ACA instance.           |
| AZURE_CLIENT_ID                             | No      | The client id to use for authentication for ACA.              |
| AZURE_CLIENT_SECRET                         | Yes     | The secret/password to use for ACA authentication.            |
| AZURE_TENANT_ID                             | No      | The tenant id that the configured ACA instance is located in. |
| LOKI_URL                                    | No      | The URL to Loki where logs should persisted.                  |

## App Settings

### Kafka Connection

| Name                                | Value       | Secret? |
|-------------------------------------|-------------|---------|
| spring.kafka.bootstrap_servers      |             | No      |
| spring.kafka.retry.max-attempts     | 3           | No      |
| spring.kafka.retry.retry-backoff-ms | 3000        | No      |
| spring.kafka.consumer.group-id      | measureeval | No      |
| spring.kafka.producer.group-id      | measureeval | No      |

### Measure Evaluation Config

| Name                         | Value                                                                 | Secret? |
|------------------------------|-----------------------------------------------------------------------|---------|
| link.reportability-predicate | com.lantanagroup.link.measureeval.reportability.IsInInitialPopulation | No      |
| link.cql_debug               | false                                                                 | No      |

### Database (Mongo)

| Name                             | Value       | Secret? |
|----------------------------------|-------------|---------|
| spring.data.mongodb.host         |             | No      |
| spring.data.mongodb.port         |             | No      |
| spring.data.mongodb.database     | measureeval | No      |
| TODO: Add authentication details |             | No      |


### Authentication & Secrets

| Name                            | Value                  | Secret? |
|---------------------------------|------------------------|---------|
| authentication.anonymous        | true                   | No      |
| authentication.authority        | https://localhost:7004 | No      |
| secret-management.key-vault-uri |                        | No      |

### Logging & Telemetry

| Name                        | Value                  | Secret? |
|-----------------------------|------------------------|---------|
| loki.enabled                | true                   | No      |
| loki.url                    |                        | No      |
| loki.app                    | link-dev               | No      |
| telemetry.exporter-endpoint | http://localhost:55690 | No      |

### Swagger

| Name                         | Value | Secret? |
|------------------------------|-------|---------|
| springdoc.api-docs.enabled   | false | No      |
| springdoc.swagger-ui.enabled | false | No      |


## Kafka Events/Topics

### Consumed Events

- **PatientDataNormalized**

### Produced Events

- **MeasureEvaluated**
- **NotificationRequested**

## API Operations

The **Measure Evaluation Service** provides REST endpoints to manage measure definitions and evaluate clinical data against those measures.

### Measure Definitions

- **GET /api/measure-definition/{id}**: Retrieves a specific measure definition by its ID.
- **PUT /api/measure-definition/{id}**: Creates or updates a measure definition with the specified ID.
- **GET /api/measure-definition**: Retrieves a list of all measure definitions.

### Measure Evaluation & Testing

- **POST /api/measure-definition/{id}/$evaluate**: Evaluates a measure against clinical data provided in the request body. May include a `debug` flag that indicates to create cql debug logs on the service during evaluation.
- **GET /api/measure-definition/{id}/{library-id}/$cql**: Retrieves the CQL for a specific measure definition and library. May include a `range` parameter that represents the range of CQL that is reported via debug logs.

### Health Check

- **GET /health**: Performs a health check to verify the service is operational.

These operations support the management of measure definitions and the evaluation of clinical data against defined measures.