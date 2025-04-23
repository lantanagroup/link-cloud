# End-to-End (E2E) Test Project

## Overview

This project is designed to execute end-to-end (E2E) tests for validating the functionality and behavior of the system.
It ensures that all the services work together seamlessly within the system by routing requests through the **Admin BFF
** service.

## Key Features

- **Testing Framework:** This project uses **XUnit** as the testing framework.
- **Admin BFF Service:** All tests communicate with the **Admin BFF service**, which acts as a proxy. The tests never
  communicate directly with individual microservices but rely on the Admin BFF for all interactions.
- **Docker Compatibility:** Tests can run in complete isolation of an external Docker Compose infrastructure to ensure
  repeatability and deterministic results.
- **Self-Contained Test Data:** All test data required during execution is embedded within the project. No external
  internet dependency is needed to fetch the test data.
- **Environment Cleanup:** Any data created during the tests is thoroughly cleaned up after execution, ensuring the
  environment is restored to its initial state.
- **Environment Variables Support:** Tests can be configured via environment variables when necessary, allowing for
  customization of the test environment and behavior without modifying the codebase.

## Prerequisites

- .NET 8.0 SDK must be installed.
- Docker (optional, only required if you wish to run the tests in complete isolation).

## Running the Tests

To execute the tests locally, follow these steps:

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run the tests:
   ```bash
   dotnet test
   ```

If you want to run the tests using the Docker Compose infrastructure, ensure the services are up and running:

   ```bash
   docker-compose up
   ```

## Test Data

- Test data resides within the **E2ETests** project.
- The tests are pre-configured to load and utilize this data during execution.

## Cleanup

Post execution, all test-created data in the environment is cleaned up. This ensures that the tests leave no residual
data that might interfere with subsequent test runs.

## End-to-End Tests

### Smoke Test

This smoke test ensures that the core functionality of the system's adhoc and historical reporting capabilities are
working as expected. It validates the interaction between services via the **Admin BFF** service. The test performs the
following steps:

1. **Load Data on a FHIR Server**: Populate the FHIR server with the necessary test data, ensuring the data is structured correctly for testing purposes.

2. **Load a Measure into the MeasureEval and Validation Services**:
    - Store a predefined measure into the **MeasureEval** service, which is responsible for evaluating measures.
    - The measure's validationa rtifacts are also loaded into the **Validation** service to verify its compliance with the expected standards.

3. **Configure a Tenant for the FHIR Server**: Set up a dedicated tenant configuration associated with the FHIR server. This is a minimal configuration that indicates how to query and normalize.

4. **Generate an Adhoc Report**:
    - Trigger the generation of an adhoc report using the data and measures loaded into the system. This tests the report generation workflow from input processing to report creation.
    - Polls the Admin BFF service on an interval to check when the report is submitted, and proceeds when it has been submitted

5. **Download the Report Data**: Retrieve the generated report data.

6. **Validate the Report Data**: Verify the downloaded report data against expected results to ensure accuracy and completeness.