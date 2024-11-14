[← Back Home](../README.md)

## Submission

### Overview

- **Technology**: .NET Core
- **Image Name**: link-submission
- **Port**: 8080
- **Database**: MSSQL (previously Mongo)
- **Volumes**: Azure Storage Account File Share mounted at `/Link/Submission`

### Environment Variables

| Name                                        | Value                         | Secret? |
|---------------------------------------------|-------------------------------|---------|
| Link__Audit__ExternalConfigurationSource    | AzureAppConfiguration         | No      |
| ConnectionStrings__AzureAppConfiguration    | `<AzureAppConfigEndpoint>`    | Yes     |
| KafkaConnection:BootstrapServers:0          | `<KafkaBootstrapServer>`      | No      |
| KafkaConnection:GroupId                     | submission-events             | No      |
| KafkaConnection:ClientId                    | submission-events             | No      |
| MongoDB:ConnectionString                    | `<ConnectionString>`          | Yes     |
| MongoDb:DatabaseName                        | `<DatabaseName>`              | No      |
| SubmissionServiceConfig:ReportServiceUrl    | `<ReportServiceUrl>/api/Report/GetSubmissionBundle` | No      |
| FileSystemConfig:FilePath                   | `/data/Submission`            | No      |
| EnableSwagger                               | true (DEV and TEST)           | No      |

### Consumed Events

- **SubmitReport**

### Produced Events

- **ReportSubmitted**
