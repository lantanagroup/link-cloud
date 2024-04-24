# Stage 1: Build the Spring Boot application
FROM maven:3.8.7-openjdk-18-slim AS build
WORKDIR /tmp
COPY Java/pom.xml ./
COPY Java/shared/pom.xml ./shared/
COPY Java/measureeval/pom.xml ./measureeval/
COPY Java/validation/pom.xml ./validation/
RUN mvn dependency:go-offline

WORKDIR /app
COPY Java/ ./
RUN mvn package -pl validation -am
RUN mv validation/target/validation-*-jar-with-dependencies.jar validation/target/validation.jar

# Stage 2: Run the Spring Boot application
FROM openjdk:17-jdk-alpine
WORKDIR /app
COPY --from=build /app/validation/target/validation.jar validation.jar
EXPOSE 31820
CMD ["java", "-jar", "validation.jar", "--add-opens java.base/java.util=ALL-UNNAMED"]