FROM maven:3-eclipse-temurin-17 AS build

WORKDIR /root

COPY pom.xml ./
RUN mvn dependency:go-offline

COPY ./ ./
RUN mvn package

FROM eclipse-temurin:17-jre-alpine AS run

WORKDIR /root

COPY --from=build /root/target/measureeval-*.jar measureeval.jar
EXPOSE 5135
CMD ["java", "-jar", "measureeval.jar"]
