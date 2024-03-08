# Link Cloud

## Introduction

Link is an open source reference implementation for CDC’s National Healthcare Safety Network (NHSN) reporting. It is an application that aggregates, calculates, and shares line-level clinical data for patients matching NHSN surveillance requirements. It is based on C#/.NET Core, Firely libraries, using Micro Services, Kafka, and other technologies. It is intended for large loads. It uses streaming technologies so that as soon as a patient can be queried, it does, rather than waiting until the end of the reporting cycle to initiate query/evaluate. So, it works through-out the entire month, distributing the workload through the entire month, and then aggregates it at the end of the reporting cycle for a submission.

## Project Set Up

Clone the repository to your local machine!

### Kafka

Kafka is a pre-requisite to running any of the services in Link.

Launch Kafka and supporting components in docker using `docker-compose-kafka-and-ui.yml` docker compose file: `docker compose -f docker-compose-kafka-and-ui.yml up -d`

This compose file sets up the following services for Kafka:

* Zookeeper (port 2181)
* Broker (port 29092)
* UI (port 8091)
* REST API (port 38082)

### Mongo DB

Using the links below, install MongoDB and MongoDB Atlas. Once completed, create the following databases in MongoDB Atlas:

 - audit
 - census
 - data
 - measureEval
 - normalization
 - notification
 - query
 - report
 - tenant

Downloads:<br/>
[MongoDB Atlas](https://www.mongodb.com/try/download/compass)<br/>
[MongoDB](https://www.mongodb.com/docs/v3.0/tutorial/install-mongodb-on-windows/)<br/>

### SQL Server

TBD

### Dotnet Application

Open solution in Visual Studio and build the solution (ctrl + shift + b)

## Service Definitions

### Account Service

The Account Service is responsible for the following:
 - Maintain a list of accounts with access to the Link application
 - Add/Remove Groups associated with Accounts
 - Add/Remove permission Roles associated with Accounts and Groups

### Audit Service

The Audit service creates and persists audit events triggered from actions that take place throughout Link. As well as:

Capture and store audit events that take place in Link
 - Create a new audit event
 - List all audit events on record
 - List all audit events on record for a specified facility
 - Provide a single audit event based on Id

### Census Service

- The census service will be responsible for maintaining and internal schedule in which it will produce events requesting a list of patients that are currently admitted.
- The census service will persist this list of patients.
  - Retention of census to be configuration option 
- The census service (for Epic STU3 facilities) will determine if a patient has been discharged and is ready for additional queries.

### Data Acquisition Service

The Data Acquisition Service does:
 - Manage configuration properties needed to establish a connection with a Fhir endpoint.
 - Manage configuration properties needed to perform query requests.
 - Acquire patient resource data required by the measure definition. 
 - Bundle gathered patient resources into a patient bundle.

### Measure Evaluation Service



### Normalization Service

The normalization service is responsible for ensuring that data for each tenant is transformed into the format necessary for a measure to be evaluated. 

### Notification Service

The notification service is responsible for emailing configured users when there is a failure that occurred during the processing of patient data that needs to be addressed by the facility. It will immediately send the notification emails once as they are consumed by the service. Potential enhancements to this service are:

### Patients To Query Service

The Patient To Query Service does: 
 - Consume each patient that is produced to the PatientIDsAcquired topic.
 - Consume each patient that is produced to the DataAcquisitionRequested topic.
 - Transform the consumed records from both Kafka topics into a unified model.
 - Produce a list of patients that are currently admitted in a facility that have not had their resources queried.

### Query Dispatch Service

The responsibilities of the Query Dispatch Service are:
 - Consume, persist, and maintain ReportScheduled events produced from the Tenant service.
 - Consume patient events that occur within Link. For each patient event that is consumed, a schedule will be created to produce a DataAcquisitionRequested event once the scheduled threshold has been met.

### Report Service

The responsibility of the Report Service are:
 - Responds to requests to generate a report by acknowledging either a valid report generation request that will be generated or respond with a failure notice that the request
 - Assembles a report through production and consumption of other events from other services depending what is necessary for the report type
 - Stores finished reports in database to allow future retrieval and submission
 - Produces completion events to handle a completed report or a report generation that failed.

### Submission Service

The responsibilities of the Submission Service are:
 - To manage the configurations needed to authenticate and establish a connection to submission endpoints.
 - Submit reports to the configured endpoint.
 - To support multiple submission destinations for each report type.
 - To acquire a report bundle by performing a GET rest operation to the Report service

### Tenant Service

The Tenant Service does the following:
 - To manage the scheduling in which the following initiating events of the system are produced:
   - Report Scheduled
   - Retention Check
 - Connect with the external Monthly Reporting Plan (MRP) API to receive the measure submission schedules per tenant.
 - Manage each tenants configuration for:
   - Monthly Reporting Plan (Report submission frequency and measure type)
   - FacilityID (aka, NHSN_Org_ID)


