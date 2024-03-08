# Link Cloud

## Introduction

Link is an open source reference implementation for CDC’s National Healthcare Safety Network (NHSN) reporting. It is an application that aggregates, calculates, and shares line-level clinical data for patients matching NHSN surveillance requirements.

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

## Service Definitions

### Account Service

#### Introduction

#### Setup

### Audit Service

#### Introduction

#### Setup

### Census Service

#### Introduction

#### Setup

### Data Acquisition Service

#### Introduction

#### Setup

### Measure Evaluation Service

#### Introduction

#### Setup

### Normalization Service

#### Introduction

#### Setup

### Notification Service

#### Introduction

#### Setup

### Patients To Query Service

#### Introduction

#### Setup

### Query Dispatch Service

#### Introduction

#### Setup

### Report Service

#### Introduction

#### Setup

### Submission Service

#### Introduction

#### Setup

### Tenant Service

#### Introduction

#### Setup

## Running End to End
