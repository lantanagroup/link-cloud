# Overview

## Key Functionality

* Validates patient data, persists validation results
* Categorizes validation results
* Consumes Kafka events to determine when to validate patient data
* Produces Kafka events when validation is complete
* Provides a REST API to query validation results

## Building

### Manually

Ensure the local Maven repository has been specified in Maven settings.

```bash
mvn clean install
```

### Docker

```bash
docker build -t link-validation .
```

## Utilities

- **Validation cost audit** — internal tool for auditing IG validation cost,
  ranking profiles / resource types / validation messages by contribution.
  See [COST-AUDIT.md](COST-AUDIT.md).