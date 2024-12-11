[← Back Home](../README.md)

## Overview

Link includes a **Census Management** module designed to acquire, evaluate, and maintain a real-time census of patients actively or recently treated within a hospital system. This module supports the submission of required patient data to governing bodies (such as NHSN) per established reporting criteria.

The system routinely gathers census data by leveraging multiple methods compatible with hospital EHRs. This document provides an overview of each acquisition method, the data elements tracked, and future considerations for expanding data persistence.

## Census Data Acquisition Methods

### 1. FHIR Standard - List Resource

The **FHIR List Resource** method is one of the primary approaches for acquiring patient lists from hospital EHR systems. In systems like Epic, patient lists are generated through proprietary queries within the EHR, associated with the FHIR List resource for access through FHIR integrations.

- **Endpoint**: `GET /List/<listId>`
- **Functionality**: Queries the EHR for patient lists identified by a `listId`.
- **Tenant Configurability**: The `listId` is configurable per tenant within Link, allowing each institution to define the patient population that constitutes their census.
- **Applicability**: This method supports census management for any EHR that utilizes FHIR Lists representing relevant patient groups.

### 2. FHIR Standard - Bulk FHIR Implementation Guide

The **Bulk FHIR** method allows Link to acquire patient data via batch processing, useful for large patient groups in systems that support flexible querying.

- **Endpoint**: Bulk FHIR `$export` request with `groupId`
- **Process**:
    - Link initiates a `$export` request for patient data by `groupId`.
    - The export process is monitored and polled routinely until completion.
    - Upon completion, patient resources are retrieved, and the FHIR ID of each patient is extracted and stored.
- **Tenant Configurability**: Each tenant configures a unique `groupId` corresponding to their desired patient group.

### 3. ADT Feeds (Under Exploration)

Link is evaluating the feasibility of using **ADT feeds**—specifically for admission, discharge, and cancellation events—to dynamically manage census data. This approach would offer real-time census updates and potentially enhance the accuracy and responsiveness of the census.

## Scheduling and Frequency

Both the FHIR List and Bulk FHIR methods utilize a configurable scheduling system, allowing each tenant to define query intervals. The scheduling system is based on CRON patterns, ensuring Link can automatically query the EHR at specified times to maintain an updated census.

## Data Persistence and Tracking

Currently, Link persists only the **FHIR ID** of each patient. This identifier allows Link to accurately track patients across census updates without storing additional demographic information.

### Future Considerations

In the interest of enhancing the user interface, Link is considering storing additional data elements, such as the **patient name** associated with each FHIR ID. This would provide users with meaningful patient identifiers, facilitating easier navigation and record management within the Link UI.---

---

For more details on the Census service, refer to the [Census Service Specification](../service_specs/census.md).