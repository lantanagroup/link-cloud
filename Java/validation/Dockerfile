FROM maven:3-eclipse-temurin-17 AS build

WORKDIR /app

COPY --chown=mvn:mvn pom.xml ./
COPY --chown=mvn:mvn shared/pom.xml shared/
COPY --chown=mvn:mvn measureeval/pom.xml measureeval/
COPY --chown=mvn:mvn validation/pom.xml validation/
RUN mvn -pl validation -am dependency:go-offline

COPY --chown=mvn:mvn ./ ./
#RUN chmod +x ./mvn
RUN mvn -pl validation -am -Dmaven.test.skip=true package

FROM eclipse-temurin:17-alpine AS run
WORKDIR /app
COPY --from=build /app/validation/target/validation-*.jar validation.jar
RUN chmod -R 755 /app \
  && chown -R 1001 /app

USER 1001
EXPOSE 5135
CMD ["java", "--add-opens", "java.base/java.util=ALL-UNNAMED", "-jar", "validation.jar"]
