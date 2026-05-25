FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/duckcreek-personal-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9002
ENTRYPOINT ["java", "-jar", "app.jar"]
