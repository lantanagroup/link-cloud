# Stage 1: Build the Spring Boot application
FROM maven:3.8.7-openjdk-18-slim AS build
WORKDIR /app
COPY Java/ ./
RUN mvn clean install -pl validation -am
RUN mv validation/target/validation-*.jar validation/target/validation.jar

# Stage 2: Run the Spring Boot application
FROM openjdk:17-jdk-alpine
WORKDIR /app
COPY --from=build /app/target/validation.jar validation.jar
EXPOSE 31820
CMD ["java", "-jar", "validation.jar", "--add-opens java.base/java.util=ALL-UNNAMED"]