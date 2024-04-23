ARG SHARED_BOTW_FEED_TOKEN_ARG

# Stage 1: Build the Spring Boot application
FROM maven:3.8.7-openjdk-18-slim AS build
WORKDIR /app

COPY Java/pom.xml ./
COPY Java/shared/pom.xml ./shared/
COPY Java/measureeval/pom.xml ./measureeval/
COPY Java/validation/pom.xml ./validation/
RUN mvn dependency:go-offline

COPY Java/ ./
RUN mvn clean install -pl measureeval -am
RUN mv measureeval/target/measureeval-*.jar measureeval/target/measureeval.jar

# Stage 2: Run the Spring Boot application
FROM openjdk:17-jdk-alpine
WORKDIR /app
COPY --from=build /app/measureeval/target/measureeval.jar measureeval.jar
EXPOSE 5135
CMD ["java", "-jar", "measureeval.jar", "--add-opens java.base/java.util=ALL-UNNAMED"]