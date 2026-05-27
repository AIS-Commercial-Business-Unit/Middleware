package com.ais.middleware.billing.finance.routes;

import com.ais.middleware.common.events.billing.BillingAssociationCreatedEvent;
import com.ais.middleware.common.events.integration.PolicyAdminSystemResponseReceivedEvent;
import com.ais.middleware.billing.finance.observability.EDAFlowProcessor;
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

/**
 * Subscribes directly to PolicyAdminSystemResponseReceivedEvent for billing fan-out after PAS confirmation.
 * Calls CRM19X1 for DirectBill and publishes BillingAssociationCreatedEvent.
 */
@Component
public class BillingAssociationRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(BillingAssociationRoute.class);

    private final ObjectMapper objectMapper;
    private final EDAFlowProcessor edaFlowProcessor;

    public BillingAssociationRoute(ObjectMapper objectMapper, EDAFlowProcessor edaFlowProcessor) {
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
                log.error("Unhandled exception in billing-association route — routing to DLQ. issuanceId={} error={}",
                        issuanceId, cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:billing.dlq.billing-association");

        from("kafka:integration.events.policy-admin-system-response-received?groupId=billing-finance-service")
            .routeId("billing-association")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyAdminSystemResponseReceivedEvent evt = objectMapper.readValue(json, PolicyAdminSystemResponseReceivedEvent.class);
                MDC.put("issuanceId", evt.issuanceId());
                log.info("[EDA subscriber] BillingAndFinanceManagement received PolicyAdminSystemResponseReceivedEvent — issuanceId={}", evt.issuanceId());
                log.info("BillingAssociation triggered by PolicyAdminSystemResponseReceived [EDA fan-out subscriber] — issuanceId={}", evt.issuanceId());
                exchange.setProperty("issuanceId", evt.issuanceId());
                exchange.setProperty("accountServiceRequestNumber", evt.accountServiceRequestNumber());
                exchange.setProperty("primaryPolicyNumber", evt.policyNumbers() != null && !evt.policyNumbers().isEmpty()
                        ? evt.policyNumbers().get(0) : "BILL-UNKNOWN");
                exchange.getIn().setBody(objectMapper.writeValueAsString(java.util.Map.of(
                        "issuanceId", evt.issuanceId(),
                        "accountServiceRequestNumber", evt.accountServiceRequestNumber() != null ? evt.accountServiceRequestNumber() : "",
                        "policyNumbers", evt.policyNumbers() != null ? evt.policyNumbers() : java.util.List.of(),
                        "billingChannel", "DirectBill"
                )));
            })
            .to("http://{{crm19x1.url}}/billing/associate?bridgeEndpoint=true")
            .process(exchange -> {
                String responseBody = exchange.getIn().getBody(String.class);
                JsonNode resp = objectMapper.readTree(responseBody);
                String issuanceId = exchange.getProperty("issuanceId", String.class);
                String accountServiceRequestNumber = exchange.getProperty("accountServiceRequestNumber", String.class);
                String primaryPolicyNumber = exchange.getProperty("primaryPolicyNumber", String.class);

                BillingAssociationCreatedEvent event = new BillingAssociationCreatedEvent(
                        resp.path("billingAccountId").asText(primaryPolicyNumber),
                        issuanceId,
                        accountServiceRequestNumber,
                        BillingAssociationCreatedEvent.BillingChannel.DirectBill,
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                log.info("[EDA publish] BillingAndFinanceManagement publishing BillingAssociationCreatedEvent — issuanceId={}", issuanceId);
                MDC.clear();
            })
            .to("kafka:billing.events.billing-association-created");
    }
}

