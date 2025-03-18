# Functionality

## Table of Contents

1. [General](#general)
2. [Census Acquisition](#census-acquisition)
3. [Patient Data Acquisition](#patient-data-acquisition)
4. [Normalization](#normalization)~~~~
5. [Evaluation](#evaluation)
6. [Validation](#validation)
7. [Submission](#submission)
8. [User Interface](#user-interface)

## General

* Progressive query and evaluation for required vs. supplemental data
* MongoDB (or CosmosDB) for clinical data persistence
* SQL Server for service/tenant configuration
* User management
* Scheduling for routine automated operations
* Authentication Support for Census and Patient Data Acquisition
  * Basic
  * OAuth
  * Epic-flavored OAuth
  * Cerner-flavored OAuth
  * Planned: JWKS support for EHR certificate validation
* Open Telemetry for observability (visualization via Grafana)
* Planned: External integration to determine facility reportability

## Census Acquisition

* FHIR List
* Planned
  * ADT
  * CSV via SFTP
* Exploring: Bulk FHIR

## Patient Data Acquisition

* Acquisition Technologies
  * Native FHIR API
* Acquisition based on Query Plan
* Dynamic reporting period (aka "Lookback Period")
* Query dependencies (i.e. acquires conditions related to already-acquired encounters)
* Follows references to shared resources
* `POST /:resourceType/_search` to acquire shared resources in bulk
* Exploring: Automatically derive Query Plan from dQM CQL

## Normalization

* Core Transformations Supported
  * Concept Maps
  * Fixing Resource IDs
  * Conditional Transformations
  * Location Identifier Transformation
  * Date Period Precision Correction
* Changes to data tracked in FHIR extensions for traceability
* Persists normalized resources 
* Planned
  * Defining order of operations
  * Tracking changes in an audit log separate from data (reducing data volumes)
 
## Evaluation

* Load IG packages' Measure, Library, ValueSet and CodeSystem resources 
* Utilization of cqframework library for CQL evaluation
* Evaluates individual patients against configured dQMs
* Evaluate multiple dQMs per reporting period

## Validation

* Load IG packages' StructureDefinition, ValueSet and CodeSystem resources
* Validate bundled Patient data post-evaluation
  * Core FHIR R4 specification
  * Profiles asserted on resources resulting from evaluation
* Persists validation results
* Categorizes validation results with flexible configuration for categories
* Planned: Halt submission based on negative validation outcomes

## Submission

* Expects data that should be submitted to be returned by CQL evaluation (i.e. MeasureReport.contained)
* Produces internal folder structure of patient data (supports debugging)
* When complete, submits data to separate folder (presumably mounted to cloud storage)

## User Interface

* Internal Admin UI
  * User management
  * Configuration of services/components
  * Tenant configuration
  * Planned:
    * Census review and manipulation
    * View report meta-data
    * View validation results and categorization
* Planned: External/Facility UI