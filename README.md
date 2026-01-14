# Link Cloud

Org: Division of Healthcare Quality Promotion (DHQP)
Contact: ncezid_shareit@cdc.gov

Description: CDC’s National Healthcare Safety Network is the nation’s healthcare-associated infection tracking system and public health surveillance system for healthcare. NHSN provides facilities, states, regions, and the nation with data needed to identify problem areas, measure progress of prevention efforts, and ultimately eliminate healthcare-associated infections. Additionally, NHSN continues to support the nation’s COVID-19 response with COVID-19 Modules for reporting in Hospitals, Long Term Care facilities, including nursing homes, and Dialysis facilities and NHSN allows healthcare facilities to track blood safety errors and important healthcare process measures such as healthcare personnel influenza vaccine status and infection control adherence rates.

## Table of Contents
* [Introduction](#introduction)
* [Documentation](#documentation)
* [Developing and Contributing](#developing-and-contributing)

## Introduction

NHSNLink is an open-source reference implementation for CDC’s National Healthcare Safety Network (NHSN) reporting. It is an application that aggregates, transforms, evaluates, validates and submits patient-level clinical data for patients matching NHSN surveillance requirements. It is based on a event driven micro service architecture using C#, Java, Kafka and other technologies. NHSNLink is designed to handle large-scale data processing efficiently. It leverages streaming technologies that can be configured to continuously query and evaluate patient data throughout the reporting cycle, rather than waiting until the end to initiate this process.

## Documentation

Documentation on Link's implementation and the functionality it supports can be found [here](docs/README.md).

## Developing and Contributing

Developer documentation can be found [here](docs/development/README.md).