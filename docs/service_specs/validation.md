[← Back Home](../README.md)

## Validation Overview

The Validation service is a Java based application that is responsible for validating FHIR resources against the FHIR specification and any additional constraints defined by the Link Cloud tenant. The service utilizes the [HAPI FHIR library](https://hapifhir.io/) to perform the validation.

- **Technology**: Java
- **Image Name**: link-validation
- **Port**: 8075
- **Database**: SQL Server

## Common Configurations

* [Azure App Config](../config/java.md#azure-app-config)
* [Telemetry](../config/java.md#telemetry)
* [Swagger](../config/java.md#swagger)
* [SQL Server](../config/java.md#sql-server)
* [Kafka](../config/java.md#kafka)
* [Service Authentication](../config/java.md#service-authentication)

## Custom Configurations

| Property Name | Description                                                                       | Type/Value              | Secret? |
|---------------|-----------------------------------------------------------------------------------|-------------------------|---------|
| artifact.init | Whether or not to initialize the artifacts in the database with default artifacts | true (default) or false | No      |

## Kafka Events/Topics

### Consumed Events

- **PatientEvaluated**

### Produced Events

None

## API Operations

The **Validation Service** provides REST endpoints to validate FHIR resources against the FHIR specification and any additional constraints defined by the Link Cloud tenant.

### Artifact Management

The ArtifactController is a Spring Boot REST controller that manages artifacts that are loaded into the validation engine (including both _individual resources_ and _resource packages_).

* `PUT /api/artifact/{type}`: Create or update an artifact 
* `DELETE /api/artifact/{type}/{name}`: Delete an artifact
* `GET /api/artifact`: List all artifacts

### Category Management

Categories are used to classify validation results (since there tend to be many) in ways that can be understood by the user. Each category has rules for how the category is matched against a validation result.

- `POST /api/category`: Create or update a category
- `GET /api/category`: Get all categories
- `GET /api/category/{categoryId}/rules`: Get the latest version of rules for a category by ID
- `POST /api/category/{categoryId}/rules`: Create or update rule sets for a category by ID
- `GET /api/category/{categoryId}/rules/history`: Get the history of rule sets for a category by ID
- `POST /api/category/bulk`: Bulk save categories and their rule sets
- `DELETE /api/category/{categoryId}`: Delete a category by ID

### Validation

- `POST /api/validation/reload`: Reload validation artifacts
- `POST /api/validation/validate`: Validate a resource (which can also be a Bundle of resources)

### Health Check

- `GET /health`: Performs a health check to verify the service is operational.

## Future Considerations

* Operation to bulk _retrieve_ categories and their rules that can be updated in an text editor and then provided back to the _bulk save_ operation.
* Operation to validate _and_ categorize a resource (or Bundle) and return a composite response of the validation results and associated categories.
* Operation to re-validate and re-categorize a given report, to update the persisted set of results and categories for the report. 