# Stage 1: Build — compiles Java source inside Docker (no local JDK/Maven needed)
FROM maven:3.9-eclipse-temurin-21 AS builder
WORKDIR /workspace

# Copy ALL module POMs first so Maven can resolve the reactor (cached until any POM changes)
COPY pom.xml .
COPY common/pom.xml common/
COPY policy-issuance-service/pom.xml policy-issuance-service/
COPY platform-compliance-service/pom.xml platform-compliance-service/
COPY customer-identity-service/pom.xml customer-identity-service/
COPY platform-integration-service/pom.xml platform-integration-service/
COPY billing-finance-service/pom.xml billing-finance-service/
COPY platform-notification-service/pom.xml platform-notification-service/
COPY platform-file-processing-service/pom.xml platform-file-processing-service/
COPY prs-appraisal-service/pom.xml prs-appraisal-service/
COPY stubs/duckcreek-commercial-stub/pom.xml stubs/duckcreek-commercial-stub/
COPY stubs/duckcreek-personal-stub/pom.xml stubs/duckcreek-personal-stub/
COPY stubs/forefront-stub/pom.xml stubs/forefront-stub/
COPY stubs/rsk3x3-compliance-stub/pom.xml stubs/rsk3x3-compliance-stub/
COPY stubs/erm7x1-account-stub/pom.xml stubs/erm7x1-account-stub/
COPY stubs/crm40x1-customer-stub/pom.xml stubs/crm40x1-customer-stub/
COPY stubs/crm19x1-billing-stub/pom.xml stubs/crm19x1-billing-stub/
COPY stubs/deipde07-mq-simulator/pom.xml stubs/deipde07-mq-simulator/
# Pre-download dependencies (cached layer — only re-runs when POMs change)
RUN mvn dependency:go-offline -pl stubs/erm7x1-account-stub -q --no-transfer-progress

# Copy only the stub source
COPY stubs/erm7x1-account-stub/src stubs/erm7x1-account-stub/src

# Compile and package
RUN mvn clean package -DskipTests -pl stubs/erm7x1-account-stub -q --no-transfer-progress

# Stage 2: Runtime — minimal JRE image
FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
COPY --from=builder /workspace/stubs/erm7x1-account-stub/target/erm7x1-account-stub-1.0.0-SNAPSHOT.jar app.jar
RUN chown appuser:appgroup app.jar
USER appuser
EXPOSE 9005
ENTRYPOINT ["java", "-jar", "app.jar"]
