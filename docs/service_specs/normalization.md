[← Back Home](../README.md)

## Normalization Overview

FHIR resources queried from EHR endpoints can vary from location to location. There will be occasions where data for specific resources may need to be adjusted to ensure that Link Cloud properly evaluates a patient against dQM’s. The Normalization service is a component in Link Cloud to help make those adjustments in an automated way. The service operates in between the resource acquisition and evaluation steps to ensure that the tenant data is in a readied state for measure evaluation.

- **Technology**: .NET Core
- **Image Name**: link-normalization
- **Port**: 8080
- **Database**: MSSQL
- **Scale**: 0-3

See [Normalization Functionality](../functionality/normalization.md) for more information on the role of the Normalization service in the Link Cloud ecosystem.

## Common Configurations

* [Swagger](../config/csharp.md#swagger)
* [Azure App Configuration](../config/csharp.md#azure-app-config-environment-variables)
* [Kafka Configuration](../config/csharp.md#kafka)
* [Kafka Consumer Retry Configuration](../config/csharp.md#kafka-consumer-settings)
* [Service Registry Configuration](../config/csharp.md#service-registry)
* [CORS Configuration](../config/csharp.md#cors)
* [Token Service Configuration](../config/csharp.md#token-service-settings)
* [Service Authentication](../config/csharp.md#service-authentication)
* [SQL Server Database Configuration](../config/csharp.md#sql-server-database)

## Kafka Events/Topics

### Consumed Events

- **PatientDataAcquired**

### Produced Events

- **PatientNormalized**
- **NotificationRequested**

## API Operations

The **Normalization** service provides REST endpoints for managing normalization configurations for each tenant. These configurations dictate how FHIR resources are normalized upon acquisition.

- **POST /api/Normalization**: Create a new normalization configuration for a tenant.
- **GET /api/Normalization/{facilityId}**: Retrieve the normalization configuration for a specific tenant by `facilityId`.
- **PUT /api/Normalization/{facilityId}**: Update the normalization configuration for a specific tenant by `facilityId`.
- **DELETE /api/Normalization/{facilityId}**: Delete the normalization configuration for a specific tenant by `facilityId`.

Each operation enables tenants to customize the normalization process to meet their specific requirements, ensuring data consistency and compliance across workflows.
## Additional Notes

### Vendor and Facility Operations

- Normalization operations can be defined at the vendor level or for a specific facility.
- Vendor operations leave the `FacilityId` property empty, while facility operations include a `FacilityId`.
- Vendor operations should not be converted into facility operations. Create a new facility operation if different logic is needed.
- There is no limit on how many vendor or facility operations may share the same configuration data.
- There is no separate operation type identifier; a null `FacilityId` means the operation is vendor-specific.
- Facilities are not automatically linked to vendors. A facility can include vendor operations directly in its sequence configuration.

### Operation Sequences

- Operations only run when they are referenced in a sequence.
- Sequence numbers determine the order in which operations are applied.
- Operations without a sequence entry are ignored during normalization.
- Sequences are loaded when a `ResourceAcquired` event is processed. The sequence number itself does not appear in the `PatientNormalized` event; examine the resulting resource to confirm the applied transformations.

### Supported Resources

- Operations can target **any** FHIR R4 resource type, not just Encounter or Location.

### Resources API

- The `/api/resources` endpoints are used to initialize and view the resource definitions known to the service.
- Call the **Initialize** endpoint once per environment. Multiple calls are safe and do not duplicate data.

### Using FHIRPath

- Some operations require `SourceFhirPath` and `TargetFhirPath` values.
- These are FHIRPath expressions indicating where a value is copied from and to, for example `identifier.value` to `type[0].coding`.
