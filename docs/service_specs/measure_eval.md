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

## Consumed Events

- **PatientDataNormalized**

## Produced Events

- **MeasureEvaluated**
- **NotificationRequested**

> **Note**: This service is being re-designed as a Java application to use CQFramework libraries directly rather than relying on a separate CQF-Ruler installation.