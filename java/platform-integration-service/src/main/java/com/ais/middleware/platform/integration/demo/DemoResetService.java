package com.ais.middleware.platform.integration.demo;

import com.mongodb.client.MongoClient;
import com.mongodb.client.MongoCollection;
import com.mongodb.client.MongoDatabase;
import org.bson.Document;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestTemplate;

import java.time.OffsetDateTime;
import java.util.*;

/**
 * DemoResetService — orchestrates full demo prep for UC4 appraisal scenarios.
 *
 * Health check URLs use Docker-internal service names when running in Compose;
 * override via environment variables for other environments.
 */
@Service
public class DemoResetService {

    private static final Logger log = LoggerFactory.getLogger(DemoResetService.class);

    private final MongoClient mongoClient;
    private final RestTemplate restTemplate;

    // Java services (Spring Boot Actuator health)
    @Value("${demo.health.policy-issuance:http://policy-issuance-service:8081}")
    private String policyIssuanceUrl;

    @Value("${demo.health.platform-compliance:http://platform-compliance-service:8082}")
    private String platformComplianceUrl;

    @Value("${demo.health.customer-identity:http://customer-identity-service:8083}")
    private String customerIdentityUrl;

    @Value("${demo.health.platform-integration:http://platform-integration-service:8084}")
    private String platformIntegrationUrl;

    @Value("${demo.health.billing-finance:http://billing-finance-service:8085}")
    private String billingFinanceUrl;

    @Value("${demo.health.platform-notification:http://platform-notification-service:8086}")
    private String platformNotificationUrl;

    @Value("${demo.health.platform-file-processing:http://platform-file-processing-service:8087}")
    private String platformFileProcessingUrl;

    @Value("${demo.health.prs-appraisal:http://prs-appraisal-service:8090}")
    private String prsAppraisalUrl;

    // .NET services (ASP.NET health endpoint)
    @Value("${demo.health.dotnet-policy-issuance:http://dotnet-policy-issuance:8181}")
    private String dotnetPolicyIssuanceUrl;

    @Value("${demo.health.dotnet-platform-compliance:http://dotnet-platform-compliance:8182}")
    private String dotnetPlatformComplianceUrl;

    @Value("${demo.health.dotnet-customer-identity:http://dotnet-customer-identity:8183}")
    private String dotnetCustomerIdentityUrl;

    @Value("${demo.health.dotnet-platform-integration:http://dotnet-platform-integration:8184}")
    private String dotnetPlatformIntegrationUrl;

    @Value("${demo.health.dotnet-billing-finance:http://dotnet-billing-finance:8185}")
    private String dotnetBillingFinanceUrl;

    @Value("${demo.health.dotnet-platform-notification:http://dotnet-platform-notification:8186}")
    private String dotnetPlatformNotificationUrl;

    @Value("${demo.health.dotnet-file-processing:http://dotnet-file-processing:8187}")
    private String dotnetFileProcessingUrl;

    @Value("${demo.health.dotnet-kafka-bridge:http://dotnet-kafka-bridge:8188}")
    private String dotnetKafkaBridgeUrl;

    @Value("${demo.health.dotnet-prs-appraisal:http://dotnet-prs-appraisal:8189}")
    private String dotnetPrsAppraisalUrl;

    public DemoResetService(MongoClient mongoClient) {
        this.mongoClient = mongoClient;
        this.restTemplate = new RestTemplate();
    }

    // ── Health ────────────────────────────────────────────────────────────────

    public Map<String, Object> checkHealth() {
        List<Map<String, Object>> services = new ArrayList<>();

        // Java stack — Spring Boot Actuator
        services.add(probe("policy-issuance-service (Java)",    policyIssuanceUrl    + "/actuator/health", "java"));
        services.add(probe("platform-compliance-service (Java)",platformComplianceUrl + "/actuator/health", "java"));
        services.add(probe("customer-identity-service (Java)",  customerIdentityUrl   + "/actuator/health", "java"));
        services.add(probe("platform-integration-service (Java)",platformIntegrationUrl + "/actuator/health", "java"));
        services.add(probe("billing-finance-service (Java)",    billingFinanceUrl     + "/actuator/health", "java"));
        services.add(probe("platform-notification-service (Java)",platformNotificationUrl + "/actuator/health", "java"));
        services.add(probe("platform-file-processing-service (Java)",platformFileProcessingUrl + "/actuator/health", "java"));
        services.add(probe("prs-appraisal-service (Java)",      prsAppraisalUrl       + "/actuator/health", "java"));

        // .NET stack — ASP.NET health checks
        services.add(probe("dotnet-policy-issuance (.NET)",    dotnetPolicyIssuanceUrl    + "/health", "dotnet"));
        services.add(probe("dotnet-platform-compliance (.NET)",dotnetPlatformComplianceUrl + "/health", "dotnet"));
        services.add(probe("dotnet-customer-identity (.NET)",  dotnetCustomerIdentityUrl   + "/health", "dotnet"));
        services.add(probe("dotnet-platform-integration (.NET)",dotnetPlatformIntegrationUrl + "/health", "dotnet"));
        services.add(probe("dotnet-billing-finance (.NET)",    dotnetBillingFinanceUrl     + "/health", "dotnet"));
        services.add(probe("dotnet-platform-notification (.NET)",dotnetPlatformNotificationUrl + "/health", "dotnet"));
        services.add(probe("dotnet-file-processing (.NET)",    dotnetFileProcessingUrl     + "/health", "dotnet"));
        services.add(probe("dotnet-kafka-bridge (.NET)",       dotnetKafkaBridgeUrl        + "/health", "dotnet"));
        services.add(probe("dotnet-prs-appraisal (.NET)",      dotnetPrsAppraisalUrl       + "/health", "dotnet"));

        long healthyCount = services.stream()
                .filter(s -> "UP".equals(s.get("status")))
                .count();

        Map<String, Object> result = new LinkedHashMap<>();
        result.put("checkedAt", OffsetDateTime.now().toString());
        result.put("totalServices", services.size());
        result.put("healthyServices", healthyCount);
        result.put("allHealthy", healthyCount == services.size());
        result.put("services", services);
        return result;
    }

    private Map<String, Object> probe(String name, String url, String stack) {
        long start = System.currentTimeMillis();
        try {
            restTemplate.getForEntity(url, String.class);
            long latency = System.currentTimeMillis() - start;
            log.info("[Demo] Health OK — service={} latencyMs={}", name, latency);
            return serviceEntry(name, url, stack, "UP", latency, null);
        } catch (Exception ex) {
            long latency = System.currentTimeMillis() - start;
            log.warn("[Demo] Health FAIL — service={} error={}", name, ex.getMessage());
            return serviceEntry(name, url, stack, "DOWN", latency, ex.getMessage());
        }
    }

    private Map<String, Object> serviceEntry(String name, String url, String stack,
                                              String status, long latencyMs, String error) {
        Map<String, Object> entry = new LinkedHashMap<>();
        entry.put("name", name);
        entry.put("url", url);
        entry.put("stack", stack);
        entry.put("status", status);
        entry.put("latencyMs", latencyMs);
        if (error != null) entry.put("error", error);
        return entry;
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    public Map<String, Object> clearData() {
        List<Map<String, Object>> cleared = new ArrayList<>();

        // Java stack — prs_appraisal_db
        cleared.add(dropCollection("prs_appraisal_db", "appraisal_received_sagas"));

        // .NET stack — dotnet_prs_appraisal_db
        cleared.add(dropCollection("dotnet_prs_appraisal_db", "plapr_staging"));
        cleared.add(dropCollection("dotnet_prs_appraisal_db", "appraisal_saga_records"));

        long clearedCount = cleared.stream()
                .filter(c -> Boolean.TRUE.equals(c.get("success")))
                .count();

        Map<String, Object> result = new LinkedHashMap<>();
        result.put("clearedAt", OffsetDateTime.now().toString());
        result.put("collectionsCleared", clearedCount);
        result.put("details", cleared);
        result.put("success", clearedCount == cleared.size());
        return result;
    }

    private Map<String, Object> dropCollection(String dbName, String collectionName) {
        try {
            MongoDatabase db = mongoClient.getDatabase(dbName);
            MongoCollection<Document> collection = db.getCollection(collectionName);
            long count = collection.countDocuments();
            collection.deleteMany(new Document());
            log.info("[Demo] Cleared collection — db={} collection={} deletedCount={}", dbName, collectionName, count);
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("db", dbName);
            entry.put("collection", collectionName);
            entry.put("deletedCount", count);
            entry.put("success", true);
            return entry;
        } catch (Exception ex) {
            log.error("[Demo] Failed to clear collection — db={} collection={} error={}", dbName, collectionName, ex.getMessage());
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("db", dbName);
            entry.put("collection", collectionName);
            entry.put("deletedCount", 0);
            entry.put("success", false);
            entry.put("error", ex.getMessage());
            return entry;
        }
    }

    // ── Seed ──────────────────────────────────────────────────────────────────

    public Map<String, Object> seedData() {
        List<Map<String, Object>> seeded = new ArrayList<>();
        MongoDatabase db = mongoClient.getDatabase("prs_appraisal_db");
        MongoCollection<Document> sagas = db.getCollection("appraisal_received_sagas");

        seeded.add(insertSaga(sagas, "demo-INS-001", "POL-12345", "INS-001", 6,  "A", "Completed",   "UA",  45, "PLUW-DEMO-001"));
        seeded.add(insertSaga(sagas, "demo-INS-002", "POL-12346", "INS-002", 6,  "B", "Completed",   "UST", 14, "PLUW-DEMO-002"));
        seeded.add(insertSaga(sagas, "demo-INS-003", "POL-12347", "INS-003", 6,  "I", "Completed",   "UA",  45, "PLUW-DEMO-003"));
        seeded.add(insertSaga(sagas, "demo-INS-004", "POL-12348", "INS-004", 6,  "A", "TimedOut",    null,   0, null));
        seeded.add(insertSaga(sagas, "demo-INS-005", "POL-12349", "INS-005", 15, "A", "Completed",   "UA",  45, null));

        long insertedCount = seeded.stream()
                .filter(s -> Boolean.TRUE.equals(s.get("success")))
                .count();

        Map<String, Object> result = new LinkedHashMap<>();
        result.put("seededAt", OffsetDateTime.now().toString());
        result.put("scenariosInserted", insertedCount);
        result.put("details", seeded);
        result.put("success", insertedCount == seeded.size());
        return result;
    }

    private Map<String, Object> insertSaga(MongoCollection<Document> collection,
                                            String correlationId, String policyNumber,
                                            String appraisalId, int statusCode,
                                            String inspectionTypeCode, String status,
                                            String uwAssignment, int suspenseDays,
                                            String pluwReferenceId) {
        try {
            Document doc = new Document()
                    .append("_id", correlationId)
                    .append("correlationId", correlationId)
                    .append("appraisalId", appraisalId)
                    .append("policyNumber", policyNumber)
                    .append("statusCode", statusCode)
                    .append("inspectionTypeCode", inspectionTypeCode)
                    .append("status", status)
                    .append("currentStep", status.equals("Completed") ? "Done" : status)
                    .append("producerCode", "REPLACE_ME_PROD001")
                    .append("producerControlCode", uwAssignment != null ? uwAssignment : "UA")
                    .append("uwAssignment", uwAssignment)
                    .append("suspenseDays", suspenseDays)
                    .append("pluwReferenceId", pluwReferenceId)
                    .append("producerLookupComplete", true)
                    .append("pluwCreateComplete", !status.equals("TimedOut"))
                    .append("uwDeterminationComplete", !status.equals("TimedOut"))
                    .append("receivedAt", OffsetDateTime.now().minusMinutes(10).toString())
                    .append("completedAt", status.equals("Completed") ? OffsetDateTime.now().minusMinutes(1).toString() : null)
                    .append("timeoutAt", status.equals("TimedOut") ? OffsetDateTime.now().minusMinutes(5).toString() : null)
                    .append("isSeedData", true);

            collection.insertOne(doc);
            log.info("[Demo] Seeded saga — correlationId={} policyNumber={} status={}", correlationId, policyNumber, status);

            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("correlationId", correlationId);
            entry.put("policyNumber", policyNumber);
            entry.put("appraisalId", appraisalId);
            entry.put("statusCode", statusCode);
            entry.put("status", status);
            entry.put("success", true);
            return entry;
        } catch (Exception ex) {
            log.error("[Demo] Failed to seed saga — correlationId={} error={}", correlationId, ex.getMessage());
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("correlationId", correlationId);
            entry.put("success", false);
            entry.put("error", ex.getMessage());
            return entry;
        }
    }

    // ── Full Reset ────────────────────────────────────────────────────────────

    public Map<String, Object> fullReset() {
        log.info("[Demo] Starting full demo reset — health check, clear, seed");

        Map<String, Object> healthResult = checkHealth();
        Map<String, Object> clearResult  = clearData();
        Map<String, Object> seedResult   = seedData();

        boolean allHealthy  = Boolean.TRUE.equals(healthResult.get("allHealthy"));
        boolean clearOk     = Boolean.TRUE.equals(clearResult.get("success"));
        boolean seedOk      = Boolean.TRUE.equals(seedResult.get("success"));
        boolean overallOk   = clearOk && seedOk;

        Map<String, Object> result = new LinkedHashMap<>();
        result.put("resetAt", OffsetDateTime.now().toString());
        result.put("success", overallOk);
        result.put("readyForDemo", overallOk);
        result.put("summary", buildSummary(allHealthy, clearOk, seedOk));
        result.put("health", healthResult);
        result.put("clear", clearResult);
        result.put("seed", seedResult);

        if (overallOk) {
            log.info("[Demo] Full reset complete — demo is ready. allServicesHealthy={}", allHealthy);
        } else {
            log.warn("[Demo] Full reset finished with issues — clearOk={} seedOk={}", clearOk, seedOk);
        }
        return result;
    }

    private String buildSummary(boolean allHealthy, boolean clearOk, boolean seedOk) {
        if (clearOk && seedOk && allHealthy) {
            return "✅ Demo ready — all services healthy, data reset, 5 scenarios seeded";
        }
        StringBuilder sb = new StringBuilder();
        sb.append(allHealthy ? "✅ Services: all healthy" : "⚠️ Services: some DOWN (check health tab)");
        sb.append(" | ");
        sb.append(clearOk ? "✅ Data: cleared" : "❌ Data: clear failed");
        sb.append(" | ");
        sb.append(seedOk ? "✅ Seed: 5 scenarios ready" : "❌ Seed: failed");
        return sb.toString();
    }
}
