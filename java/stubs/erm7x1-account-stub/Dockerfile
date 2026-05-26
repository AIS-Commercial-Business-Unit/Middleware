FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
COPY target/erm7x1-account-stub-1.0.0-SNAPSHOT.jar app.jar
RUN chown appuser:appgroup app.jar
USER appuser
EXPOSE 9005
ENTRYPOINT ["java", "-jar", "app.jar"]