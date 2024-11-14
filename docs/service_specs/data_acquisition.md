[← Back Home](../README.md)

## Data Acquisition

### Overview

- **Technology**: .NET Core
- **Image Name**: link-dataacquisition
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

### Kafka Connection

| Name                                    | Value                    | Secret? |
|-----------------------------------------|--------------------------|---------|
| KafkaConnection:BootstrapServers:0       | `<KafkaBootstrapServer>` | No      |
| KafkaConnection:GroupId                  | data-acquisition-events  | No      |

### Consumed Events

- **PatientEvent**
- **PatientBulkAcquisitionScheduled**

### Produced Events

- **PatientIdsAcquired**
- **PatientAcquired**
- **NotificationRequested**