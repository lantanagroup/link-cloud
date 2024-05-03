# Stage 1: Build the Spring Boot application
FROM maven:3.8.7-openjdk-18-slim AS build
WORKDIR /tmp
COPY pom.xml ./
COPY shared/pom.xml ./shared/
COPY measureeval/pom.xml ./measureeval/
COPY validation/pom.xml ./validation/
RUN mvn dependency:go-offline

WORKDIR /app
COPY . .
RUN mvn package -pl validation -am
RUN mv validation/target/validation-*-jar-with-dependencies.jar validation/target/validation.jar

# Stage 2: Run the Spring Boot application
FROM openjdk:17-jdk-alpine
WORKDIR /app
COPY --from=build /app/validation/target/validation.jar validation.jar
EXPOSE 31820
CMD ["java", "-jar", "validation.jar", "--add-opens java.base/java.util=ALL-UNNAMED"]