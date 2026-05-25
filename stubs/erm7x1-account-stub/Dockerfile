FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/erm7x1-account-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9005
ENTRYPOINT ["java", "-jar", "app.jar"]
