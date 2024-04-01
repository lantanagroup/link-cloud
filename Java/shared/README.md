# Overview

The shared module contains shared functionality across all java services
It is published to a separate private maven repository so that builds of service modules can find the shared module in
the private repository

# Testing Changes

To test changes to the shared library before deploying them to the private maven repo, run `mvn clean install` on the
shared
library, first. Then re-run `mvn clean install` on the dependent modules, and they will pick up the changes from the
local
.m2 repository where the shared library was installed.