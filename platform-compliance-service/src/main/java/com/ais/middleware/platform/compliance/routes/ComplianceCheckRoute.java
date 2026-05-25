package com.ais.middleware.platform.compliance.routes;

import com.ais.middleware.common.events.compliance.ComplianceClearedEvent;
import com.ais.middleware.common.events.compliance.RequestComplianceCheckCommand;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.builder.RouteBuilder;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;

/**
 * Handles compliance check requests from PolicyIssuanceAndLifecycleManagement.
 * Calls RSK3X3 external sanctions screening service and publishes a proper ComplianceClearedEvent.
 */
@Component
public class ComplianceCheckRoute extends RouteBuilder {

    private final ObjectMapper objectMapper;

    public ComplianceCheckRoute(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public void configure() throws Exception {
        from("kafka:compliance.commands.request-compliance-check?groupId=platform-compliance-service")
            .routeId("compliance-check")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                RequestComplianceCheckCommand cmd = objectMapper.readValue(json, RequestComplianceCheckCommand.class);
                exchange.setProperty("correlationId", cmd.correlationId());
                exchange.setProperty("checkId", cmd.checkId());
                // Forward the original body as HTTP POST payload
                exchange.getIn().setBody(json);
            })
            .log("ComplianceCheck received for correlationId=${exchangeProperty.correlationId}")
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
            })
            .log("RSK3X3 response received — publishing ComplianceCleared for ${exchangeProperty.correlationId}")
            .to("kafka:compliance.events.compliance-cleared");
    }
}

