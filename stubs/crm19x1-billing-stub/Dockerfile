FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/crm19x1-billing-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9007
ENTRYPOINT ["java", "-jar", "app.jar"]
