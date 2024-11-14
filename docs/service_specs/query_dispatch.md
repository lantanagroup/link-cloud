[← Back Home](../README.md)

## Query Dispatch Overview

The Query Dispatch service is primarily responsible for applying a lag period prior to making FHIR resource query requests against a facility endpoint. The current implementation of the Query Dispatch service handles how long Link Cloud should wait before querying for a patient’s FHIR resources after being discharged. To ensure that the encounter related data for the patient has been settled (Medications have been closed, Labs have had their results finalized, etc), tenants are able to customize how long they would like the lag from discharge to querying to be.

- **Technology**: .NET Core
- **Image**: link-querydispatch
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Scale**: 0-3

## Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |

## Kafka Connection

| Name                                    | Value                    | Secret? |
|-----------------------------------------|--------------------------|---------|
| KafkaConnection:BootstrapServers:0       | `<KafkaBootstrapServer>` | No      |
| KafkaConnection:GroupId                  | query-dispatch-events    | No      |
| KafkaConnection:ClientId                 | query-dispatch-events    | No      |

## Database Settings (MSSQL)

| Name                   | Value                | Secret? |
|------------------------|----------------------|---------|
| MongoDB:ConnectionString | `<ConnectionString>` | Yes   |
| MongoDB:DatabaseName     | `<DatabaseName>`     | No    |
| MongoDB:CollectionName   | `<CollectionName>`   | No    |

## Additional Settings

| Name         | Value                          | Secret? |
|--------------|--------------------------------|---------|
| EnableSwagger | true (DEV and TEST)           | No      |

## Consumed Events

- **ReportScheduled**
- **PatientEvent**

## Produced Events

- **DataAcquisitionRequested**
