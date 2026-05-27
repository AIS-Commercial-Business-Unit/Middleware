package com.ais.middleware.platform.integration.routes;

import com.ais.middleware.common.events.integration.PolicyAdminSystemResponseReceivedEvent;
import com.ais.middleware.common.events.policy.IssuePolicyRequestedEvent;
import com.ais.middleware.platform.integration.observability.EDAFlowProcessor;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.apache.camel.component.kafka.KafkaConstants;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Content-Based Router: routes IssuePolicyRequested to the correct PAS gateway
 * based on policyTypeCode. This is the subscription filter self-selection pattern —
 * no routing logic lives in PolicyIssuanceAndLifecycleManagement (BR-PIL-005).
 *
 * WHAT IS A CONTENT-BASED ROUTER? (see .docs/getting-started.md)
 * A Content-Based Router examines each incoming message and decides which
 * processing path to take based on the message contents. Here, policyTypeCode
 * in the message header determines which PAS receives the request.
 *
 * DuckCreek Commercial Lines: PolicyTypeCodes 1, 2, 3, 4, 42, 44, 45, 46, 47
 * DuckCreek Personal Lines:   PolicyTypeCodes 5, 6, 7, 8, 9
 * ForeFront:                  PolicyTypeCodes 10, 12, 14, 17, 18
 */
@Component
public class PasGatewayRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(PasGatewayRoute.class);

    private final ObjectMapper objectMapper;
    private final EDAFlowProcessor edaFlowProcessor;

    public PasGatewayRoute(ObjectMapper objectMapper, EDAFlowProcessor edaFlowProcessor) {
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
                log.error("Unhandled exception in pas-gateway route — routing to DLQ. issuanceId={} routeId={} error={}",
                        issuanceId, exchange.getFromRouteId(),
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:integration.dlq.pas-gateway");

        // Parse the incoming IssuePolicyRequestedEvent and store issuanceId + policyTypeCode before routing
        from("kafka:policy.events.issue-policy-requested?groupId=platform-integration-service")
            .routeId("pas-gateway-router")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                IssuePolicyRequestedEvent evt = objectMapper.readValue(json, IssuePolicyRequestedEvent.class);
                exchange.setProperty("issuanceId", evt.issuanceId());
                exchange.setProperty("accountServiceRequestNumber", evt.accountServiceRequestNumber());
                int policyTypeCode = (evt.policies() != null && !evt.policies().isEmpty())
                        ? evt.policies().get(0).policyTypeCode() : 0;
                exchange.setProperty("policyTypeCode", policyTypeCode);
                exchange.getIn().setBody(json);
                exchange.getIn().setHeader("policyTypeCode", String.valueOf(policyTypeCode));
                log.info("[EDA subscriber] PlatformIntegration received IssuePolicyRequestedEvent — issuanceId={}", evt.issuanceId());
            })
            .log("IssuePolicyRequested received — routing by policyTypeCode=${exchangeProperty.policyTypeCode}")
            .choice()
                // DuckCreek Commercial Lines — codes 1-4, 42, 44-47
                .when(header("policyTypeCode").in("1","2","3","4","42","44","45","46","47"))
                    .log("Routing to DuckCreek Commercial Lines")
                    .to("direct:duckcreek-commercial")
                // DuckCreek Personal Lines — codes 5-9
                .when(header("policyTypeCode").in("5","6","7","8","9"))
                    .log("Routing to DuckCreek Personal Lines")
                    .to("direct:duckcreek-personal")
                // ForeFront — codes 10, 12, 14, 17, 18
                .when(header("policyTypeCode").in("10","12","14","17","18"))
                    .log("Routing to ForeFront")
                    .to("direct:forefront")
                .otherwise()
                    .log("WARNING: No PAS gateway registered for policyTypeCode=${exchangeProperty.policyTypeCode}")
                    .to("kafka:integration.events.policy-admin-system-call-failed")
            .end();

        // DuckCreek Commercial: call stub, publish PolicyAdminSystemResponseReceived
        from("direct:duckcreek-commercial")
            .routeId("duckcreek-commercial-adapter")
            .to("http://{{duckcreek.commercial.url}}/policy/issue?bridgeEndpoint=true")
            .process(exchange -> buildPasResponseEvent(exchange, "DuckCreek-Commercial"))
            .log("[EDA publish] PlatformIntegration publishing PolicyAdminSystemResponseReceivedEvent — issuanceId=${exchangeProperty.issuanceId} — PAS response received with accountServiceRequestNumber (fan-out: billing-finance + customer-identity + policy-issuance subscribed)")
            .to("kafka:integration.events.policy-admin-system-response-received");

        // DuckCreek Personal: call stub, publish PolicyAdminSystemResponseReceived
        from("direct:duckcreek-personal")
            .routeId("duckcreek-personal-adapter")
            .to("http://{{duckcreek.personal.url}}/policy/issue?bridgeEndpoint=true")
            .process(exchange -> buildPasResponseEvent(exchange, "DuckCreek-Personal"))
            .log("[EDA publish] PlatformIntegration publishing PolicyAdminSystemResponseReceivedEvent — issuanceId=${exchangeProperty.issuanceId} — PAS response received with accountServiceRequestNumber (fan-out: billing-finance + customer-identity + policy-issuance subscribed)")
            .to("kafka:integration.events.policy-admin-system-response-received");

        // ForeFront: call stub, publish PolicyAdminSystemResponseReceived
        from("direct:forefront")
            .routeId("forefront-adapter")
            .to("http://{{forefront.url}}/policy/issue?bridgeEndpoint=true")
            .process(exchange -> buildPasResponseEvent(exchange, "ForeFront"))
            .log("[EDA publish] PlatformIntegration publishing PolicyAdminSystemResponseReceivedEvent — issuanceId=${exchangeProperty.issuanceId} — PAS response received with accountServiceRequestNumber (fan-out: billing-finance + customer-identity + policy-issuance subscribed)")
            .to("kafka:integration.events.policy-admin-system-response-received");
    }

    private void buildPasResponseEvent(org.apache.camel.Exchange exchange, String targetPas) throws Exception {
        String responseBody = exchange.getIn().getBody(String.class);
        JsonNode resp = objectMapper.readTree(responseBody);
        String issuanceId = exchange.getProperty("issuanceId", String.class);
        String accountServiceRequestNumber = exchange.getProperty("accountServiceRequestNumber", String.class);

        String policyNumber = resp.path("policyNumber").asText(null);
        List<String> policyNumbers = (policyNumber != null) ? List.of(policyNumber) : List.of();

        PolicyAdminSystemResponseReceivedEvent event = new PolicyAdminSystemResponseReceivedEvent(
                issuanceId,
                targetPas,
                accountServiceRequestNumber,
                policyNumbers,
                OffsetDateTime.now()
        );
        exchange.getIn().setBody(objectMapper.writeValueAsString(event));
        exchange.getIn().setHeader("issuanceId", issuanceId);
    }
}

