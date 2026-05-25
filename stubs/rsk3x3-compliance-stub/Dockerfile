FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/rsk3x3-compliance-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9004
ENTRYPOINT ["java", "-jar", "app.jar"]
