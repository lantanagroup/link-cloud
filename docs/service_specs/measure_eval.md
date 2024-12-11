[← Back Home](../README.md)

## Measure Eval Overview

The Measure Eval service is a Java based application that is primarily responsible for evaluating bundles of acquired patient resources against the measures that Link Cloud tenants are configured to evaluate with. The service utilizes the [CQF framework](https://github.com/cqframework/cqf-ruler) to perform the measure evaluations.

- **Technology**: .NET Core 8
- **Image Name**: link-measureeval
- **Port**: 8080
- **Database**: Mongo

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## App Settings

### Kafka Connection

| Name                                     | Value                     | Secret? |
|------------------------------------------|---------------------------|---------|
| KafkaConnection__BootstrapServers__0     | `<KafkaBootstrapServer>`  | No      |
| KafkaConnection__GroupId                 | measure-events            | No      |

### Measure Evaluation Config

| Name                                       | Value                                           | Secret? |
|--------------------------------------------|-------------------------------------------------|---------|
| MeasureEvalConfig__TerminologyServiceUrl   | `https://cqf-ruler.nhsnlink.org/fhir`           | No      |
| MeasureEvalConfig__EvaluationServiceUrl    | `https://cqf-ruler.nhsnlink.org/fhir`           | No      |

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

### Measure Evaluation

- **POST /api/measure-definition/{id}/$evaluate**: Evaluates a measure against clinical data provided in the request body.

### Health Check

- **GET /health**: Performs a health check to verify the service is operational.

These operations support the management of measure definitions and the evaluation of clinical data against defined measures.