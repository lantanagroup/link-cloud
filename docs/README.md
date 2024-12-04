
## Overview

This page and its references include documentation for Link's services and the functionality those services supports.

## Functionality

* [Admin UI](functionality/admin_ui.md)
* [Census Management](functionality/census_management.md)
* [Data Acquisition](functionality/data_acquisition.md)
* [Measure Evaluation](functionality/measure_eval.md)
* [Normalization](functionality/normalization.md)
* Submission
  * [Folder Structure](functionality/submission_folder.md)
* [Telemetry](functionality/telemetry.md)
* [Tenant/Facility Management](functionality/tenant_mgmt.md)

## Service Specifications

* [Account](service_specs/account.md)
* [Admin UI](service_specs/admin_ui.md)
* [Audit](service_specs/audit.md)
* [Backend For Frontend (BFF)](service_specs/bff.md)
* [Census](service_specs/census.md)
* [Data Acquisition](service_specs/data_acquisition.md)
* [Measure Evaluation](service_specs/measure_eval.md)
* [Normalization](service_specs/normalization.md)
* [Notification](service_specs/notification.md)
* [Query Dispatch](service_specs/query_dispatch.md)
* [Report](service_specs/report.md)
* [Submission](service_specs/submission.md)
* [Tenant](service_specs/tenant.md)

### Service Swagger Specifications

When deployed, each service provides a Swagger UI for exploring its API. The Swagger UI is available at the `/swagger` endpoint for most of the services. For example, the Swagger UI for the **Account** service is available at `https://link-account/swagger`. However, the endpoint for swagger specifications varies between .NET and Java.

* .NET Services
  * UI: `/swagger`
  * JSON: `/swagger/v1/swagger.json`
* Java Services
  * UI: `/swagger-ui.html`
  * JSON: `/v3/api-docs`