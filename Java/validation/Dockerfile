FROM maven:3-eclipse-temurin-17 AS build

WORKDIR /root

COPY pom.xml ./
COPY shared/pom.xml shared/
COPY measureeval/pom.xml measureeval/
COPY validation/pom.xml validation/
RUN mvn -pl validation -am dependency:go-offline

COPY ./ ./
RUN mvn -pl validation -am -Dmaven.test.skip=true package

FROM eclipse-temurin:17-alpine AS run

WORKDIR /root

COPY --from=build /root/validation/target/validation-*.jar validation.jar
EXPOSE 31820
CMD ["java", "--add-opens", "java.base/java.util=ALL-UNNAMED", "-jar", "validation.jar"]
