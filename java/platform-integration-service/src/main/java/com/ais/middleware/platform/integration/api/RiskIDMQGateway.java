package com.ais.middleware.platform.integration.api;

import org.apache.camel.ProducerTemplate;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ThreadLocalRandom;

/**
 * RiskIDMQGateway — HTTP stub simulating IBM MQ inbound from RiskID.
 *
 * ⚠️ DEMO GAP: Real RiskID MQ message format unknown - using mock structure.
 * Production implementation: IBM MQ JMS consumer on the MQ queue; this HTTP
 * endpoint exists only to drive demo scenarios from Platform.UI.
 *
 * POST /api/riskid/status-update  →  direct:riskid-kafka-publish  →  prs.events.appraisal-received
 */
@RestController
@RequestMapping("/api/riskid")
public class RiskIDMQGateway {

    private static final Logger log = LoggerFactory.getLogger(RiskIDMQGateway.class);

    private final ProducerTemplate producerTemplate;

    public RiskIDMQGateway(ProducerTemplate producerTemplate) {
        this.producerTemplate = producerTemplate;
    }

    /**
     * Accepts a mock RiskID status update and publishes it to Kafka.
     *
     * ⚠️ DEMO GAP: Real RiskID MQ message format unknown - using mock structure.
     * The request body mirrors what BizTalk received over IBM MQ (MQSC adapter),
     * but the exact wire format has not been confirmed with the PRS integration team.
     */
    @PostMapping("/status-update")
    public ResponseEntity<Map<String, String>> receiveStatusUpdate(
            @RequestBody RiskIDStatusUpdateRequest request) {

        String correlationId = (request.appraisalId() != null && !request.appraisalId().isBlank())
                ? request.appraisalId()
                : "APR-" + UUID.randomUUID();

        MDC.put("correlationId", correlationId);
        MDC.put("policyNumber", request.policyNumber());
        try {
            log.info("[EDA_FLOW] RiskIDMQGateway received status update — correlationId={} policyNumber={} statusCode={} inspectionTypeCode={}",
                    correlationId, request.policyNumber(), request.statusCode(), request.inspectionTypeCode());

            // Simulate realistic IBM MQ dequeue + parse latency (500–1200ms)
            long delayMs = ThreadLocalRandom.current().nextLong(500, 1201);
            log.info("⚠️ STUBBED: RiskIDMQGateway simulating IBM MQ receive latency={}ms (real MQ dequeue would take this long) — correlationId={}",
                    delayMs, correlationId);
            try { Thread.sleep(delayMs); } catch (InterruptedException e) { Thread.currentThread().interrupt(); }

            // ⚠️ DEMO GAP: Real RiskID MQ message format unknown - using mock structure.
            // Route body (the original JSON request) + correlation props through Camel to Kafka.
            producerTemplate.sendBodyAndHeaders(
                    "direct:riskid-kafka-publish",
                    request,
                    Map.of(
                            "correlationId", correlationId,
                            "policyNumber", request.policyNumber() != null ? request.policyNumber() : "",
                            "statusCode", String.valueOf(request.statusCode())
                    )
            );

            return ResponseEntity.accepted().body(Map.of(
                    "correlationId", correlationId,
                    "status", "accepted",
                    "message", "RiskID status update accepted and published to prs.events.appraisal-received"
            ));
        } finally {
            MDC.remove("correlationId");
            MDC.remove("policyNumber");
        }
    }
}
