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

#### AppSettings
**NOTE** You may need to create an appsettings.Local.json file in your cloned copy or update appsettings.development.json with updated kafka settings or postgres connection information.
```
"KafkaConnection": {
  "BootstrapServers": [ "localhost:9092/" ],
  "ClientId": "",
  "GroupId": "default"
},
"Postgres": {
  "ConnectionString": "Host=localhost;Database=link;Username=user;Password=admin"
},
```

#### Endpoints

##### Account Operations
Provided via the AccountService class.  Currently supports basic CRUD and adding/removing accounts from groups and roles.  Generally an AccountMessage maps to AccountModel. Additionally, the Account Service will contain functionality for determining user authorization.

###### Account Service gRPC and REST Operations
- Get all accounts
  - gRPC: GetAllAccounts(GetAllAccountsMessage) returns (stream AccountMessage)
  - REST: GET /api/account
- Get one account
  - gRPC: GetAccount(GetAccountMessage) returns (AccountMessage)
  - REST: GET /api/account/{id}
- Create account
  - gRPC: CreateAccount(AccountMessage) returns (AccountMessage)
  - REST: POST /api/account
- Update account
  - gRPC: UpdateAccount(AccountMessage) returns (AccountMessage)
  - REST: PUT /api/account/{id}
- Delete account
  - gRPC: DeleteAccount(DeleteAccountMessage) returns (AccountDeletedMessage)
  - REST: DELETE /api/account/{id}
- Restore a deleted account
  - gRPC: RestoreAccount(RestoreAccountMessage) returns (AccountMessage)
  - REST: POST /api/account/restore/{id}
- Add an account to a group
  - gRPC: AddAccountToGroup(AddAccountToGroupMessage) returns (AccountMessage)
  - REST: POST /api/account/{accountId}/group/{groupId}
- Remove an account from a group
  - gRPC: RemoveAccountFromGroup(RemoveAccountFromGroupMessage) returns (AccountRemovedFromGroupMessage)
  - REST: DELETE /api/account/{accountId}/group/{groupId}
- Assign a role to an account
  - gRPC: AddRoleToAccount(AddRoleToAccountMessage) returns (AccountMessage)
  - REST: POST /api/account/{accountId}/role/{roleId}
- Remove a role from an account
  - gRPC: RemoveRoleFromAccount(RemoveRoleFromAccountMessage) returns (RoleRemovedFromAccountMessage)
  - REST: DELETE /api/account/{accountId}/role/{roleId}
- User Has Access
  - gRPC: UserHasAccess(string email, string facilityId string role, string group) returns bool  

##### Group Operations
Provided via the GroupService class.  Currently supports basic CRUD.  Generally a GroupMessage maps to GroupModel.

###### Group Service gRPC and REST Operations
- Get all groups
  - gRPC: GetAllGroups(GetAllGroupsMessage) returns (stream GroupMessage)
  - REST: GET /api/group
- Get one group
  - gRPC: GetGroup(GetGroupMessage) returns (GroupMessage)
  - REST: GET /api/group/{id}
- Create group
  - gRPC: CreateGroup(GroupMessage) returns (GroupMessage)
  - REST: POST /api/group
- Update group
  - gRPC: UpdateGroup(GroupMessage) returns (GroupMessage)
  - REST: PUT /api/group/{id}
- Delete group
  - gRPC: DeleteGroup(DeleteGroupMessage) returns (GroupDeletedMessage)
  - REST: DELETE /api/group/{id}
- Restore a deleted group
  - gRPC: RestoreGroup(RestoreGroupMessage) returns (GroupMessage)
  - REST: POST /api/group/restore/{id}

##### Role Operations
Provided via the RoleService class.  Currently supports basic CRUD.  Generally a RoleMessage maps to RoleModel.

###### Role Service gRPC and REST Operations
- Get all roles
  - gRPC: GetAllRoles(GetAllRolesMessage) returns (stream RoleMessage)
  - REST: GET /api/role
- Get one role
  - gRPC: GetRole(GetRoleMessage) returns (RoleMessage)
  - REST: GET /api/role/{id}
- Create role
  - gRPC: CreateRole(RoleMessage) returns (RoleMessage)
  - REST: POST /api/role
- Update role
  - gRPC: UpdateRole(RoleMessage) returns (RoleMessage)
  - REST: PUT /api/role/{id}
- Delete role
  - gRPC: DeleteRole(DeleteRoleMessage) returns (RoleDeletedMessage)
  - REST: DELETE /api/role/{id}
- Restore a deleted role
  - gRPC: RestoreRole(RestoreRoleMessage) returns (RoleMessage)
  - REST: POST /api/role/restore/{id}

### Audit Service

The Audit service creates and persists audit events triggered from actions that take place throughout Link. As well as:

Capture and store audit events that take place in Link
 - Create a new audit event
 - List all audit events on record
 - List all audit events on record for a specified facility
 - Provide a single audit event based on Id

#### Rest Operations

- Get All Audit Events - **GET api/audit**
  - searchText: text to use in full text search
  - filterFacilityBy: return only events where facility id equals
  - filterServiceBy: return only events where service name equals
  - filterActionBy: return only events where action equals
  - filterUserBy: return only events where user id equals
  - sortBy: property to sort by, Options: FacilityId, Action, ServiceName, Resource, CreatedOn, defaults to CreatedOn
  - sortOrder: Ascending = 0, Descending = 1, defaults to Ascending
  - pageSize: results returned per page, max is 20
  - pageNumber: the page number to return
- Get one Audit Event - **GET api/audit/{*auditId*}**
- Get audit events for a facility - **GET api/audit/facility/*{facilityId}***

### Census Service

#### Responsibilities:
- The census service will be responsible for maintaining and internal schedule in which it will produce events requesting a list of patients that are currently admitted.
- The census service will persist this list of patients.
  - Retention of census to be configuration option 
- The census service (for Epic STU3 facilities) will determine if a patient has been discharged and is ready for additional queries.

####

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


