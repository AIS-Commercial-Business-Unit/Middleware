package com.ais.middleware.platform.compliance.routes;

import com.ais.middleware.common.events.compliance.ComplianceClearedEvent;
import com.ais.middleware.common.events.compliance.RequestComplianceCheckCommand;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;

/**
 * Handles compliance check requests from PolicyIssuanceAndLifecycleManagement.
 * Calls RSK3X3 external sanctions screening service and publishes a proper ComplianceClearedEvent.
 */
@Component
public class ComplianceCheckRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(ComplianceCheckRoute.class);

    private final ObjectMapper objectMapper;

    public ComplianceCheckRoute(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public void configure() throws Exception {

        // Global DLQ handler: 2 retries with exponential backoff, then dead-letter.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                String correlationId = exchange.getProperty("correlationId", String.class);
                log.error("Unhandled exception in compliance-check route — routing to DLQ. correlationId={} error={}",
                        correlationId, cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:compliance.dlq.compliance-check");

        from("kafka:compliance.commands.request-compliance-check?groupId=platform-compliance-service")
            .routeId("compliance-check")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                RequestComplianceCheckCommand cmd = objectMapper.readValue(json, RequestComplianceCheckCommand.class);
                MDC.put("issuanceId", cmd.correlationId());
                log.info("ComplianceCheck received — correlationId={} checkType={}", cmd.correlationId(), cmd.checkType());
                exchange.setProperty("correlationId", cmd.correlationId());
                exchange.setProperty("checkId", cmd.checkId());
                // Forward the original body as HTTP POST payload
                exchange.getIn().setBody(json);
            })
            .to("http://{{rsk3x3.url}}/screen?bridgeEndpoint=true")
            .process(exchange -> {
                String responseBody = exchange.getIn().getBody(String.class);
                JsonNode resp = objectMapper.readTree(responseBody);
                String correlationId = exchange.getProperty("correlationId", String.class);
                String checkId = exchange.getProperty("checkId", String.class);

                // Build a proper ComplianceClearedEvent from the RSK3X3 response
                ComplianceClearedEvent event = new ComplianceClearedEvent(
                        checkId,
                        correlationId,
                        "PolicyIssuance",
                        "EconomicSanctions",
                        resp.path("referenceId").asText("unknown"),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", correlationId);
                log.info("RSK3X3 response received — publishing ComplianceCleared correlationId={}", correlationId);
                MDC.clear();
            })
            .to("kafka:compliance.events.compliance-cleared");
    }
}

