# Tenant/Facility Management

In Link Cloud, the terms "tenant" and "facility" are used interchangeably to refer to healthcare organizations configured within the system. Each tenant/facility is identified by a unique `facilityId` that must be consistent across all services to ensure proper functionality.

## Tenant Service
The Tenant service acts as the primary entry point for configuring organizations within Link Cloud. It maintains core facility configurations and generates events for scheduled measure reporting periods.

### Core Tenant Configuration Elements
- Facility ID (unique identifier used across all services)
- Facility Name
- Creation and Modification dates
- Scheduled Tasks
- Monthly Reporting Plans
    - Report Types
    - Reporting Periods
    - Submission Schedules

## Cross-Service Tenant Configuration

A fully functional tenant requires configuration across multiple services. Each service maintains its own tenant-specific configurations using the `facilityId` as the linking identifier.

### Service-Specific Configurations

#### Tenant Service

- Basic facility information
- Reporting schedules
- Monthly reporting plans
- Multi-measure reporting configurations

#### Data Acquisition Service

- Data source configurations
- Query parameters
- Resource type settings
- Data collection schedules

#### Measure Evaluation Service

- Measure specifications
- Evaluation schedules
- Performance settings
- Output configurations

#### Report Generation Service

- Report templates
- Output format preferences
- Delivery settings
- Generation schedules

#### Notification Service

- Email notification settings
- Recipient lists
- Alert preferences
- Communication channels

#### Census Services

- Facility identification
  - Facility ID linking
  - Tenant API endpoint configuration
  - Base service URLs
- Patient Census Management
  - FHIR List query frequency settings
  - Admission tracking parameters
  - Discharge monitoring configurations
  - Patient information retention policies

#### Query Dispatch Service

- Facility Configuration
  - Facility ID mapping
  - FHIR endpoint connections
  - Resource query parameters
- Query Timing Management
  - Discharge lag period settings
  - Resource query scheduling
  - Data settlement wait times
  - Query retry parameters
- Resource Query Settings
  - FHIR resource type configurations
  - Query batch sizes
  - Performance optimization parameters
  - Resource filtering rules

## Important Notes

1. Creating a tenant configuration in the Tenant service alone does not create a fully functional tenant. Additional configuration in other services is required.
2. The `facilityId` must be consistent across all services to ensure proper system integration and functionality.
3. Each service captures and manages different aspects of a tenant's configuration, working together to provide complete functionality.

## Best Practices

### Configuration Management
- Ensure all required services are properly configured for each tenant
- Maintain consistent `facilityId` usage across services