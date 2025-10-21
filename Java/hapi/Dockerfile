# syntax=docker/dockerfile:1
# It's probably best to use the same the Java version in the build and run stages
# I.e., when upgrading the HAPI image, consider whether the Maven image needs to be upgraded as well
# Later versions of HAPI:
#   - Are distroless by default (starting in 5.7.0), so that suffix would need to be removed from the image tag
#   - Already use the ENTRYPOINT specified below (starting in 6.6.0), so it could be removed here
#   - Support component scanning of custom packages found in /app/extra-classes (starting in 6.6.0)
#     So we could move our components from ca.uhn.fhir.jpa.starter to the more appropriate com.lantanagroup.link.hapi
#     Then set the HAPI_FHIR_CUSTOMBEANPACKAGES environment variable to com.lantanagroup.link.hapi
# See the upgrade notes in the POM as well

FROM maven:3-eclipse-temurin-11 AS build
WORKDIR /app
COPY pom.xml ./
COPY hapi/pom.xml hapi/
COPY measureeval/pom.xml measureeval/
COPY shared/pom.xml shared/
COPY validation/pom.xml validation/
RUN --mount=type=cache,id=m2,sharing=locked,target=/root/.m2 \
    mvn -pl hapi -am -B -e -ntp dependency:go-offline
COPY hapi/src/main hapi/src/main
RUN --mount=type=cache,id=m2,sharing=locked,target=/root/.m2 \
    mvn -pl hapi -am -B -DskipTests -e -ntp package \
 && mvn -pl hapi -B -e -ntp dependency:copy-dependencies -DoutputDirectory=target/dependencies

FROM hapiproject/hapi:v5.6.0-distroless AS run
WORKDIR /app
COPY --from=build /app/hapi/target/hapi-*.jar extra-classes/link.jar
COPY --from=build /app/hapi/target/dependencies/*.jar extra-classes/
ENTRYPOINT [ \
    "java", \
    "--class-path", \
    "/app/main.war", \
    "-Dloader.path=main.war!/WEB-INF/classes/,main.war!/WEB-INF/,/app/extra-classes", \
    "org.springframework.boot.loader.PropertiesLauncher" \
]
