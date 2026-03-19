# It's probably best to use the same the Java version in the build and run stages
# I.e., when upgrading the HAPI image, consider whether the Maven image needs to be upgraded as well
# Later versions of HAPI:
#   - Are distroless by default (starting in 5.7.0), so that suffix would need to be removed from the image tag
#   - Already use the ENTRYPOINT specified below (starting in 6.6.0), so it could be removed here
#   - Support component scanning of custom packages found in /app/extra-classes (starting in 6.6.0)
#     So we could move our components from ca.uhn.fhir.jpa.starter to the more appropriate com.lantanagroup.link.hapi
#     Then set the HAPI_FHIR_CUSTOMBEANPACKAGES environment variable to com.lantanagroup.link.hapi
# See the upgrade notes in the POM as well

FROM maven:3-eclipse-temurin-21 AS build
WORKDIR /app
COPY pom.xml ./
COPY hapi/pom.xml hapi/
COPY measureeval/pom.xml measureeval/
COPY shared/pom.xml shared/
COPY validation/pom.xml validation/
RUN mvn -pl hapi -am -B -e -ntp dependency:go-offline
COPY hapi/ hapi/
RUN mvn -pl hapi -am -B -DskipTests -e -ntp package
RUN mvn -pl hapi dependency:copy-dependencies -DoutputDirectory=target/dependencies

FROM hapiproject/hapi:v7.6.1 AS run
WORKDIR /app
COPY --from=build /app/hapi/target/hapi-*.jar extra-classes/link.jar
COPY --from=build /app/hapi/target/dependencies/*.jar extra-classes/
# In newer HAPI versions, the ENTRYPOINT is usually already set, but we might want to customize it if needed.
# For now, let's keep the customization to ensure the extra-classes are picked up correctly.
# But check if HAPI 7.6.1 uses a different mechanism.
# Usually, we can use HAPI_FHIR_CUSTOMBEANPACKAGES.
ENV HAPI_FHIR_CUSTOMBEANPACKAGES=com.lantanagroup.link.hapi
# New HAPI versions might not need the custom ENTRYPOINT if we use the default and put jars in right place.
# However, to be safe and consistent with previous version's intent:
ENTRYPOINT [ \
    "java", \
    "--class-path", \
    "/app/main.war", \
    "-Dloader.path=main.war!/WEB-INF/classes/,main.war!/WEB-INF/,/app/extra-classes", \
    "org.springframework.boot.loader.launch.PropertiesLauncher" \
]
