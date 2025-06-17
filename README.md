# Link Cloud

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
