FROM maven:3-eclipse-temurin-17 AS build

WORKDIR /root

COPY pom.xml ./
COPY shared/pom.xml shared/
COPY measureeval/pom.xml measureeval/
COPY validation/pom.xml validation/
RUN mvn -pl measureeval -am dependency:go-offline

COPY ./ ./
RUN mvn -pl measureeval -am -Dmaven.test.skip=true package

FROM eclipse-temurin:17-alpine AS run

WORKDIR /root

COPY --from=build /root/measureeval/target/measureeval-*.jar measureeval.jar
EXPOSE 5135
CMD ["java", "--add-opens", "java.base/java.util=ALL-UNNAMED", "-jar", "measureeval.jar"]
