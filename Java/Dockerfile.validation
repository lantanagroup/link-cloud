FROM maven:3.8.7-openjdk-18-slim AS deps
WORKDIR /app
COPY pom.xml /app
COPY shared/pom.xml /app/shared/pom.xml
COPY validation/pom.xml /app/validation/pom.xml
RUN mvn dependency:go-offline

# Stage 1: Build the Spring Boot application
FROM maven:3.8.7-openjdk-18-slim AS build
WORKDIR /app
COPY --from=deps /root/.m2 /root/.m2
COPY . /app
RUN mvn clean install -DskipTests
RUN mv validation/target/validation-*.jar validation/target/validation.jar

# Stage 2: Run the Spring Boot application
FROM openjdk:17-jdk-alpine
WORKDIR /app
# Extract the version from the JAR filename using shell scripting
COPY --from=build /app/validation/target/validation.jar validation.jar
EXPOSE 8080
CMD ["java", "-jar", "validation.jar"]