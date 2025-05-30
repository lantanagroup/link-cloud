[← Back Home](../README.md)

## Account Overview

The Account service is responsible for maintaining roles and permissions for Link Cloud users.

- **Technology**: .NET Core
- **Image Name**: link-account
- **Port**: 8080
- **Database**: MSSQL

## Common Configurations

* [Swagger](../config/csharp.md#swagger)
* [Azure App Configuration](../config/csharp.md#azure-app-config-environment-variables)
* [Kafka Configuration](../config/csharp.md#kafka)
* [Service Registry Configuration](../config/csharp.md#service-registry)
* [CORS Configuration](../config/csharp.md#cors)
* [Token Service Configuration](../config/csharp.md#token-service-settings)
* [Service Authentication](../config/csharp.md#service-authentication)
* [SQL Server Database Configuration](../config/csharp.md#sql-server-database)

## Account-Specific Configurations

| Name                                          | Description                      | Required | Default Value | Secret? |
|-----------------------------------------------|----------------------------------|----------|---------------|---------|
| UserManagement__EnableAutomaticUserActivation | Enable automatic user activation | No       | true          | No      |

## Kafka Events/Topics

### Consumed Events

- **NONE**

### Produced Events

- **AuditableEventOccurred**

## API Operations

The **Account** service provides REST endpoints for managing users, roles, and claims, enabling user lifecycle management and role-based access control.

- **GET /api/account/info**: Retrieve basic information about the Account service.
- **POST /api/account/user**: Create a new user.
- **GET /api/account/user/{id}**: Retrieve information about a specific user by `id`.
- **PUT /api/account/user/{id}**: Update an existing user by `id`.
- **DELETE /api/account/user/{id}**: Delete a user by `id`.
- **POST /api/account/user/{id}/activate**: Activate a user, making them active.
- **POST /api/account/user/{id}/deactivate**: Deactivate a user, making them inactive.
- **POST /api/account/user/{id}/recover**: Recover a deleted user and make them active.
- **GET /api/account/users**: Search for users with various filters.
- **GET /api/account/user/email/{email}**: Retrieve a user by their email address.
- **GET /api/account/user/facility/{id}**: Retrieve users associated with a specific facility by `id`.
- **GET /api/account/user/role/{id}**: Retrieve users assigned to a specific role by `id`.
- **PUT /api/account/user/{id}/claims**: Update claims assigned to a specific user.

### Role Management
- **POST /api/account/role**: Create a new role.
- **GET /api/account/role**: Retrieve all roles.
- **GET /api/account/role/{id}**: Retrieve a specific role by `id`.
- **PUT /api/account/role/{id}**: Update an existing role by `id`.
- **DELETE /api/account/role/{id}**: Delete a role by `id`.
- **GET /api/account/role/name/{name}**: Retrieve a specific role by its `name`.
- **PUT /api/account/role/{id}/claims**: Update claims assigned to a specific role.

### Claims Management
- **GET /api/account/claims**: Retrieve a list of all assignable claims.
