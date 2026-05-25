FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/forefront-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9003
ENTRYPOINT ["java", "-jar", "app.jar"]
