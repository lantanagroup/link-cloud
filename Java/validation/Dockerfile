# syntax=docker/dockerfile:1
FROM maven:3-eclipse-temurin-17 AS build

WORKDIR /app

COPY --chown=mvn:mvn pom.xml ./
COPY --chown=mvn:mvn shared/pom.xml shared/
COPY --chown=mvn:mvn hapi/pom.xml hapi/
COPY --chown=mvn:mvn measureeval/pom.xml measureeval/
COPY --chown=mvn:mvn validation/pom.xml validation/
RUN --mount=type=cache,id=m2,sharing=locked,target=/root/.m2 \
    mvn -pl validation -am -B -e -ntp dependency:go-offline

COPY --chown=mvn:mvn shared/src/main shared/src/main
COPY --chown=mvn:mvn validation/src/main validation/src/main
RUN --mount=type=cache,id=m2,sharing=locked,target=/root/.m2 \
    mvn -pl validation -am -B -e -ntp -Dmaven.test.skip=true package

FROM eclipse-temurin:17-jre-alpine AS run
RUN apk add --no-cache curl
WORKDIR /app
COPY --from=build /app/validation/target/validation-*.jar validation.jar
RUN chmod -R 755 /app \
  && chown -R 1001 /app

USER 1001
EXPOSE 5135
# Heap tracks the container/cgroup (or host) memory. Override JAVA_TOOL_OPTIONS
# only if you need a different percentage; do not replace this with a fixed -Xmx.
ENV JAVA_TOOL_OPTIONS="-XX:MaxRAMPercentage=75.0"
CMD ["java", "--add-opens", "java.base/java.util=ALL-UNNAMED", "-jar", "validation.jar"]
