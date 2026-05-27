package com.ais.middleware.platform.compliance.routes;

import com.ais.middleware.common.events.compliance.ComplianceBlockedEvent;
import com.ais.middleware.common.events.compliance.ComplianceClearedEvent;
import com.ais.middleware.common.events.policy.PolicyIssuanceInitiatedEvent;
import com.ais.middleware.platform.compliance.observability.EDAFlowProcessor;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.apache.camel.component.kafka.KafkaConstants;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.UUID;

/**
 * Subscribes to PolicyIssuanceInitiatedEvent from PolicyIssuanceAndLifecycleManagement.
 * Calls RSK3X3 external sanctions screening service and publishes ComplianceClearedEvent or ComplianceBlockedEvent.
 */
@Component
public class ComplianceCheckRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(ComplianceCheckRoute.class);

    private final ObjectMapper objectMapper;
    private final EDAFlowProcessor edaFlowProcessor;

    public ComplianceCheckRoute(ObjectMapper objectMapper, EDAFlowProcessor edaFlowProcessor) {
        this.objectMapper = objectMapper;
        this.edaFlowProcessor = edaFlowProcessor;
    }

    @Override
    public void configure() throws Exception {

        interceptFrom("kafka:*")
            .process(exchange -> exchange.setProperty("EDA_FLOW_DIRECTION", "consumed"))
            .process(edaFlowProcessor);

        interceptSendToEndpoint("kafka:*")
            .process(exchange -> {
                String uri = exchange.getProperty(Exchange.INTERCEPTED_ENDPOINT, String.class);
                if (uri == null) {
                    uri = exchange.getProperty(Exchange.TO_ENDPOINT, String.class);
                }
                if (uri == null) {
                    uri = exchange.getIn().getHeader(Exchange.TO_ENDPOINT, String.class);
                }
                if (uri == null || !uri.startsWith("kafka:")) {
                    return;
                }

                String topic = uri.replaceFirst("^kafka:(//)?", "");
                int optionsSeparator = topic.indexOf('?');
                if (optionsSeparator >= 0) {
                    topic = topic.substring(0, optionsSeparator);
                }

                exchange.getIn().setHeader(KafkaConstants.TOPIC, topic);
                exchange.setProperty("EDA_FLOW_DIRECTION", "published");
            })
            .process(edaFlowProcessor);

        // Global DLQ handler: 2 retries with exponential backoff, then dead-letter.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                String issuanceId = exchange.getProperty("issuanceId", String.class);
                log.error("Unhandled exception in compliance-check route — routing to DLQ. issuanceId={} error={}",
                        issuanceId, cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:compliance.dlq.compliance-check");

        from("kafka:policy.events.policy-issuance-initiated?groupId=platform-compliance-service")
            .routeId("compliance-check")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyIssuanceInitiatedEvent evt = objectMapper.readValue(json, PolicyIssuanceInitiatedEvent.class);
                MDC.put("issuanceId", evt.issuanceId());
                log.info("[EDA subscriber] PlatformCompliance received PolicyIssuanceInitiatedEvent — issuanceId={}", evt.issuanceId());
                log.info("ComplianceCheck triggered by PolicyIssuanceInitiated [EDA subscriber] — issuanceId={}", evt.issuanceId());
                exchange.setProperty("issuanceId", evt.issuanceId());
                exchange.setProperty("checkId", UUID.randomUUID().toString());
                exchange.getIn().setBody(objectMapper.writeValueAsString(Map.of(
                        "issuanceId", evt.issuanceId(),
                        "accountId", evt.accountId(),
                        "policyTypeCode", evt.policyTypeCode()
                )));
            })
            .to("http://{{rsk3x3.url}}/screen?bridgeEndpoint=true&httpMethod=POST")
            .process(exchange -> {
                String responseBody = exchange.getIn().getBody(String.class);
                JsonNode resp = objectMapper.readTree(responseBody);
                String issuanceId = exchange.getProperty("issuanceId", String.class);
                String checkId = exchange.getProperty("checkId", String.class);
                String status = resp.path("status").asText("Clear");

                if ("Clear".equalsIgnoreCase(status)) {
                    ComplianceClearedEvent event = new ComplianceClearedEvent(
                            checkId,
                            issuanceId,
                            "PolicyIssuance",
                            "EconomicSanctions",
                            resp.path("referenceId").asText("unknown"),
                            OffsetDateTime.now()
                    );
                    exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                    exchange.setProperty("complianceTopic", "kafka:compliance.events.compliance-cleared");
                    log.info("[EDA publish] PlatformCompliance publishing ComplianceClearedEvent — issuanceId={}", issuanceId);
                } else {
                    ComplianceBlockedEvent event = new ComplianceBlockedEvent(
                            checkId,
                            issuanceId,
                            "PolicyIssuance",
                            resp.path("reason").asText(status),
                            OffsetDateTime.now()
                    );
                    exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                    exchange.setProperty("complianceTopic", "kafka:compliance.events.compliance-blocked");
                    log.info("[EDA publish] PlatformCompliance publishing ComplianceBlockedEvent — issuanceId={}", issuanceId);
                }
                exchange.getIn().setHeader("issuanceId", issuanceId);
                MDC.clear();
            })
            .toD("${exchangeProperty.complianceTopic}");
    }
}

