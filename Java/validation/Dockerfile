FROM maven:3-eclipse-temurin-17 AS build

# Create a group and user
RUN groupadd maven
RUN useradd -m -s /bin/bash -g maven  mavenuser
USER mavenuser

WORKDIR /home/mavenuser

COPY pom.xml ./
COPY shared/pom.xml shared/
COPY measureeval/pom.xml measureeval/
COPY validation/pom.xml validation/
RUN mvn -pl validation -am dependency:go-offline

COPY --chown=mavenuser:maven ./ ./

RUN mvn -pl validation -am -Dmaven.test.skip=true package

FROM eclipse-temurin:17-alpine AS run

RUN adduser -D mavenuser
USER mavenuser

WORKDIR  /home/mavenuser

COPY --from=build /root/validation/target/validation-*.jar validation.jar
EXPOSE 31820
CMD ["java", "--add-opens", "java.base/java.util=ALL-UNNAMED", "-jar", "validation.jar"]
