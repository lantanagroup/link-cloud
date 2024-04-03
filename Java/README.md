# Overview

Each folder is a separate module. The modules are independently built (i.e. CD to module directory and
run `mvn clean install`).
The shared module is a dependency for the other modules, but is published to a private maven repository so that it is
available to the other modules during the build process.
To authenticate against the private maven repo, each service has a .m2/settings.xml file that contains parameterized (
from env variables) credentials for the private maven repo.
In automated builds, this environment variable is set by the CI/CD pipeline.