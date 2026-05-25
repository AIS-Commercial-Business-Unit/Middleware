FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY target/crm40x1-customer-stub-1.0.0-SNAPSHOT.jar app.jar
EXPOSE 9006
ENTRYPOINT ["java", "-jar", "app.jar"]
