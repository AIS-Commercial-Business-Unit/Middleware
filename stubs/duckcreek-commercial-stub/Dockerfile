FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/duckcreek-commercial-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9001
ENTRYPOINT ["java", "-jar", "app.jar"]
