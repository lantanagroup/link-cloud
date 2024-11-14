[← Back Home](../README.md)

## Measure

### Overview

- **Technology**: .NET Core 8
- **Image Name**: link-measureeval
- **Port**: 8080
- **Database**: Mongo

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

### Kafka Connection

| Name                                    | Value                    | Secret? |
|-----------------------------------------|--------------------------|---------|
| KafkaConnection:BootstrapServers:0       | `<KafkaBootstrapServer>` | No      |
| KafkaConnection:GroupId                  | measure-events           | No      |

### Measure Evaluation Config

| Name                                      | Value                                           | Secret? |
|-------------------------------------------|-------------------------------------------------|---------|
| MeasureEvalConfig:TerminologyServiceUrl   | `https://cqf-ruler.nhsnlink.org/fhir`           | No      |
| MeasureEvalConfig:EvaluationServiceUrl    | `https://cqf-ruler.nhsnlink.org/fhir`           | No      |

### Consumed Events

- **PatientDataNormalized**

### Produced Events

- **MeasureEvaluated**
- **NotificationRequested**

**Note**: This service is being re-designed as a Java application to use CQFramework libraries directly rather than relying on a separate CQF-Ruler installation.