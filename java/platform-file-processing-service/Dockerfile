FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
RUN mkdir -p /app/data/renewals/inbound /app/data/renewals/processed /app/data/renewals/error && \
    chown -R appuser:appgroup /app/data
COPY target/platform-file-processing-service-1.0.0-SNAPSHOT.jar app.jar
RUN chown appuser:appgroup app.jar
USER appuser
EXPOSE 8087
ENTRYPOINT ["java", "-jar", "app.jar"]
