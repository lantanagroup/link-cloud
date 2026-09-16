# Link Cloud

## Table of Contents
* [Introduction](#introduction)
* [Getting Started](#getting-started)
* [Documentation and Contributing](#documentation-and-contributing)

## Introduction

Link is an open-source, real-world scalable platform for public health reporting and clinical data processing with FHIR (Fast Healthcare Interoperable Resources). It aggregates, transforms, evaluates, validates, and submits patient-level clinical data based on configurable surveillance and reporting requirements. Link is built on an event-driven microservices architecture leveraging C#, Java, Kafka, Redis, Microsoft SQL Server, MongoDB, Azure Blob Storage, and observability tooling with OpenTelemetry, Prometheus, and Loki. By leveraging streaming technologies, Link enables continuous querying and evaluation of patient data throughout the reporting lifecycle, allowing proactive data analysis rather than waiting until the end of a reporting period.

## Getting Started

* **[DEVELOPMENT.md](DEVELOPMENT.md)** — bringing up the local docker-compose stack, building and testing the .NET solution, the Backend E2E suites, the Java modules, the Admin UI, and EF Core migrations.
* **[ARCHITECTURE.md](ARCHITECTURE.md)** — how the services fit together: the .NET service layout, the shared contract layer, Kafka conventions, and the report-generation pipeline end to end.

## Documentation and Contributing

Documentation for Link implementation, development, and contribution can be found at [https://lantanagroup.github.io/link-cloud](https://lantanagroup.github.io/link-cloud).