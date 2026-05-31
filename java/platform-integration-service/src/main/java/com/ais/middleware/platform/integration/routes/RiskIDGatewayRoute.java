package com.ais.middleware.platform.integration.routes;

import com.ais.middleware.common.events.prs.AppraisalReceivedEvent;
import com.ais.middleware.platform.integration.api.RiskIDStatusUpdateRequest;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;

/**
 * RiskIDGatewayRoute — Camel route bridging the RiskIDMQGateway HTTP stub to Kafka.
 *
 * Flow:
 *   direct:riskid-kafka-publish
 *     → transform RiskIDStatusUpdateRequest → AppraisalReceivedEvent
 *     → kafka:prs.events.appraisal-received
 *
 * Error path:
 *   onException → 2 retries with exponential backoff → kafka:prs.dlq.riskid-gateway
 *
 * ⚠️ DEMO GAP: Real RiskID MQ message format unknown - using mock structure.
 * Production: Replace direct:riskid-kafka-publish with IBM MQ JMS consumer endpoint.
 * The transformation logic below (field mappings, status codes) must be validated
 * against actual RiskID MQ wire format once schema is confirmed with integration team.
 */
@Component
public class RiskIDGatewayRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(RiskIDGatewayRoute.class);

    private final ObjectMapper objectMapper;

    public RiskIDGatewayRoute(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public void configure() {

        // DLQ pattern: 2 retries with exponential backoff; failed messages never dropped.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                String correlationId = exchange.getIn().getHeader("correlationId", String.class);
                log.error("[EDA_FLOW] RiskIDGatewayRoute publish failed — routing to DLQ. correlationId={} error={}",
                        correlationId, cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:prs.dlq.riskid-gateway");

        // Entry point: receives RiskIDStatusUpdateRequest from RiskIDMQGateway controller.
        //
        // ⚠️ DEMO GAP: Real RiskID MQ message format unknown - using mock structure.
        // Production swap: replace `from("direct:riskid-kafka-publish")` with
        //   `from("jms:queue:RISKID.STATUS.UPDATE?connectionFactory=#ibmMqConnectionFactory")`
        // and update the processor below to map the actual IBM MQ wire message.
        from("direct:riskid-kafka-publish")
            .routeId("riskid-gateway")
            .process(exchange -> {
                RiskIDStatusUpdateRequest request =
                        exchange.getIn().getBody(RiskIDStatusUpdateRequest.class);

                String correlationId = exchange.getIn().getHeader("correlationId", String.class);

                // ⚠️ DEMO GAP: Actual RiskID schema needed from integration team.
                // Mapping below is based on BizTalk orchestration analysis only.
                AppraisalReceivedEvent event = new AppraisalReceivedEvent(
                        correlationId,
                        request.policyNumber(),
                        request.appraisalId(),
                        request.statusCode(),
                        request.inspectionTypeCode(),
                        OffsetDateTime.now()
                );

                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("correlationId", correlationId);
                exchange.setProperty("correlationId", correlationId);

                log.info("[EDA_FLOW] RiskIDGatewayRoute publishing AppraisalReceivedEvent — correlationId={} policyNumber={} statusCode={}",
                        correlationId, request.policyNumber(), request.statusCode());
            })
            .log("[EDA_FLOW] Publishing to prs.events.appraisal-received — correlationId=${header.correlationId} statusCode=${header.statusCode}")
            .to("kafka:prs.events.appraisal-received");
    }
}
