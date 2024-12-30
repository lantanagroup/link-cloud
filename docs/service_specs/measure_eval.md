[← Back Home](../README.md)

## Measure Eval Overview

The Measure Eval service is a Java based application that is primarily responsible for evaluating bundles of acquired patient resources against the measures that Link Cloud tenants are configured to evaluate with. The service utilizes the [CQF framework](https://github.com/cqframework/cqf-ruler) to perform the measure evaluations.

- **Technology**: Java
- **Image Name**: link-measureeval
- **Port**: 8067
- **Database**: Mongo

See [Measure Eval Functionality](../functionality/measure_eval.md) for more information on the role of the Measure Eval service in the Link Cloud ecosystem.

## Common Configurations

* [Azure App Config](../config/java.md#azure-app-config)
* [Telemetry](../config/java.md#telemetry)
* [Swagger](../config/java.md#swagger)
* [Mongo DB](../config/java.md#mongo-db)
* [Kafka](../config/java.md#kafka)
* [Service Authentication](../config/java.md#service-authentication)

## Custom Configurations

| Property Name                | Description                                       | Type/Value                               | Secret? |
|------------------------------|---------------------------------------------------|------------------------------------------|---------|
| link.reportability-predicate | Predicate to determine if a patient is reportable | "...IsInInitialPopulation"<br/>(default) | No      |

### Reportability Predicates

The `link.reportability-predicate` property is used to determine if a patient is reportable. The default value is "com.lantanagroup.link.measureeval.reportability.IsInInitialPopulation", whichi s a class that implements `Predicate<MeasureReport>`. Other predicate implementations may be built over time in the same package and should be listed here.

Package `com.lantanagroup.link.measureeval.reportability`:

* `IsInInitialPopulation`: Determines if a patient is reportable if they are in the initial population (a count of 1 or more for the "InitialPopulation" population of the patient's MeasureReport).

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