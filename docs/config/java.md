[← Back Home](../README.md)

# Common Configurations for Java Services

Any of the properties for serivce configuration can be provided either via environment variables, through a custom `application.yml` file, or via properties set in java using `-D<propertyName>=<value>` passed as an argument to the JVM during startup.

## Azure App Config

| Property Name                                                                                                 | Description                                                                                                                                                                                          | Type/Value    |
|---------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------|
| spring.cloud.azure.appconfiguration.enabled                                                                   | Enable Azure App Configuration                                                                                                                                                                       | true or false |
| spring.cloud.azure.appconfiguration.stores[0].connection-string<br/>OR<br/>AZURE_APP_CONFIG_CONNECTION_STRING | Connection string to Azure App Config instance. The `AZURE_APP_CONFIG_CONNECTION_STRING` variable is setup in the default bootstrap-dev.yml configuration that's used by Azure App Config libraries. | \<string>     |
| spring.cloud.azure.appconfiguration.stores[0].selects[0].label-filter                                         | Label to use for configuration                                                                                                                                                                       | ",Validation" |
| spring.cloud.azure.appconfiguration.stores[0].selects[0].key-filter                                           | Key to use for configuration                                                                                                                                                                         | "/"           |

## Telemetry

| Property Name              | Description                                                   | Type/Value               |
|----------------------------|---------------------------------------------------------------|--------------------------|
| telemetry.exporterEndpoint | Endpoint that can be connected to by scrapers for metric data | "http://localhost:55690" |
| loki.enabled               | Enable Loki for logging                                       | true or false            |
| loki.url                   | URL for Loki                                                  | "http://localhost:3100"  |
| loki.app                   | Application name for Loki                                     | "link-dev"               |

## Swagger

| Property Name                | Description                              | Type/Value                                                                   |
|------------------------------|------------------------------------------|------------------------------------------------------------------------------|
| springdoc                    | Configuration for Swagger and Swagger UI | See [Springdoc documentation](https://springdoc.org/#properties) for details |
| springdoc.api-docs.enabled   | Enable Swagger specification generation  | true or false (default)                                                      |
| springdoc.swagger-ui.enabled | Enable Swagger UI                        | true or false (default)                                                      |

## Mongo DB

| Property Name                | Description                          | Type/Value    | Secret? |
|------------------------------|--------------------------------------|---------------|---------|
| spring.data.mongodb.host     | Host address for the Mongo database  | "localhost"   | No      |
| spring.data.mongodb.port     | Port for the Mongo database          | 27017         | No      |
| spring.data.mongodb.database | Database name for the Mongo database | "measureeval" | No      |
| spring.data.mongodb.username | Username for the Mongo database      | \<string>     | No      |
| spring.data.mongodb.password | Password for the Mongo database      | \<string>     | Yes     |

## SQL Server

| Property Name                  | Description                          | Type/Value                                         | Secret? |
|--------------------------------|--------------------------------------|----------------------------------------------------|---------|
| spring.datasource.url          | URL for the SQL Server database      | \<string> prefixed with "jdbc:sqlserver://"        | No      |
| spring.datasource.username     | Username for the SQL Server database | \<string>                                          | No      |
| spring.datasource.password     | Password for the SQL Server database | \<string>                                          | Yes     |
| spring.jpa.hibernate.ddl-auto  | DDL auto setting for JPA/Hibernate   | "none" (default) or "update"                       | No      |
| spring.jpa.properties.show_sql | Show SQL statements in logs          | true (default) or false                            | No      |
| spring.jpa.properties.dialect  | SQL dialect for the database         | "org.hibernate.dialect.SQLServerDialect" (default) | No      |

## Kafka

| Property Name                       | Description                                                       | Type/Value       | Secret? |
|-------------------------------------|-------------------------------------------------------------------|------------------|---------|
| spring.kafka.bootstrap-servers      | Kafka bootstrap servers                                           | "localhost:9092" | No      |
| spring.kafka.consumer.group-id      | Kafka consumer group ID                                           | "measureeval"    | No      |
| spring.kafka.producer.client-id     | Kafka producer client ID                                          | "measureeval"    | No      |
| spring.kafka.retry.maxAttempts      | Maximum number of times consumption of an event should be retried | 3                | No      |
| spring.kafka.retry.retry-backoff-ms | Time in milliseconds to wait before retrying a failed event       | 3000             | No      |

## Service Authentication

| Property Name                   | Description                                                                                                                | Type/Value              | Secret? |
|---------------------------------|----------------------------------------------------------------------------------------------------------------------------|-------------------------|---------|
| secret-management.key-vault-uri | URI for the Azure Key Vault                                                                                                | \<string>               | Yes     |
| authentication.anonymous        | Whether the service should allow anonmyous users access to the services. This should onyl be enabled for DEV environments. | true or false (default) | No      |
| authentication.authority        | Authority for the service to authenticate against.                                                                         | "http://localhost:7004" | No      |