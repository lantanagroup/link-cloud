ARG SHARED_BOTW_FEED_TOKEN_ARG

# Stage 1: Build the Spring Boot application
FROM maven:3.8.7-openjdk-18-slim AS build
ARG SHARED_BOTW_FEED_TOKEN_ARG
WORKDIR /app
COPY . /app
ENV SHARED_BOTW_FEED_TOKEN=$SHARED_BOTW_FEED_TOKEN_ARG
COPY .m2/settings.xml /root/.m2/settings.xml
RUN mvn clean install -DskipTests
RUN mv target/measureeval-*.jar target/measureeval.jar

# Stage 2: Run the Spring Boot application
FROM openjdk:17-jdk-alpine
WORKDIR /app
COPY --from=build /app/target/measureeval.jar measureeval.jar
EXPOSE 8080
CMD ["java", "-jar", "measureeval.jar"]