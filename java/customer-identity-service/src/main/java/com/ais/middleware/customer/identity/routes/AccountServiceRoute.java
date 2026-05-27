package com.ais.middleware.customer.identity.routes;

import com.ais.middleware.common.events.customer.AccountLookupRequestedEvent;
import com.ais.middleware.common.events.customer.AccountServiceRecordRetrievedEvent;
import com.ais.middleware.common.events.customer.CustomerUpdatedEvent;
import com.ais.middleware.common.events.integration.PolicyAdminSystemResponseReceivedEvent;
import com.ais.middleware.customer.identity.observability.EDAFlowProcessor;
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
import java.util.List;

/**
 * Subscribes to AccountLookupRequestedEvent to retrieve an AccountServiceRequestNumber via ERM7X1.
 * Also subscribes directly to PolicyAdminSystemResponseReceivedEvent to update CRM40X1 after PAS confirms policy.
 */
@Component
public class AccountServiceRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(AccountServiceRoute.class);

    private final ObjectMapper objectMapper;
    private final EDAFlowProcessor edaFlowProcessor;

    public AccountServiceRoute(ObjectMapper objectMapper, EDAFlowProcessor edaFlowProcessor) {
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
                log.error("Unhandled exception in account-service route — routing to DLQ. routeId={} error={}",
                        exchange.getFromRouteId(),
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:customer.dlq.account-service");

        // ── Get-or-create account service record ─────────────────────────────
        from("kafka:customer.events.account-lookup-requested?groupId=customer-identity-service")
            .routeId("account-lookup")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                AccountLookupRequestedEvent evt = objectMapper.readValue(json, AccountLookupRequestedEvent.class);
                MDC.put("issuanceId", evt.issuanceId());
                log.info("[EDA subscriber] CustomerIdentityAndRelationshipManagement received AccountLookupRequestedEvent — issuanceId={}", evt.issuanceId());
                log.info("AccountLookup triggered by AccountLookupRequested [EDA subscriber] — issuanceId={}", evt.issuanceId());
                exchange.setProperty("correlationId", evt.issuanceId());
                exchange.setProperty("externalAccountId", evt.accountId());
                exchange.getIn().setBody(json);
            })
            .to("http://{{erm7x1.url}}/account-service?bridgeEndpoint=true&httpMethod=GET")
            .process(exchange -> {
                String responseBody = exchange.getIn().getBody(String.class);
                JsonNode resp = objectMapper.readTree(responseBody);
                String correlationId = exchange.getProperty("correlationId", String.class);
                String externalAccountId = exchange.getProperty("externalAccountId", String.class);

                AccountServiceRecordRetrievedEvent event = new AccountServiceRecordRetrievedEvent(
                        correlationId,
                        externalAccountId,
                        resp.path("accountServiceRequestNumber").asText("ERM-UNKNOWN"),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", correlationId);
                log.info("[EDA publish] CustomerIdentityAndRelationshipManagement publishing AccountServiceRecordRetrievedEvent — issuanceId={}", correlationId);
                MDC.clear();
            })
            .to("kafka:customer.events.account-service-record-retrieved");

        // ── Update customer record after PAS confirmation ─────────────────────
        from("kafka:integration.events.policy-admin-system-response-received?groupId=customer-identity-service-customer-update")
            .routeId("customer-update")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyAdminSystemResponseReceivedEvent evt = objectMapper.readValue(json, PolicyAdminSystemResponseReceivedEvent.class);
                String targetPolicyNumber = evt.policyNumbers() != null && !evt.policyNumbers().isEmpty()
                        ? evt.policyNumbers().get(0) : "";
                MDC.put("issuanceId", evt.issuanceId());
                log.info("[EDA subscriber] CustomerIdentityAndRelationshipManagement received PolicyAdminSystemResponseReceivedEvent — issuanceId={}", evt.issuanceId());
                log.info("CustomerUpdate triggered by PolicyAdminSystemResponseReceived [EDA fan-out subscriber] — issuanceId={}", evt.issuanceId());
                exchange.setProperty("correlationId", evt.issuanceId());
                exchange.setProperty("externalAccountId", targetPolicyNumber);
                exchange.getIn().setBody(objectMapper.writeValueAsString(java.util.Map.of(
                        "issuanceId", evt.issuanceId(),
                        "policyNumber", targetPolicyNumber,
                        "policyNumbers", evt.policyNumbers() != null ? evt.policyNumbers() : List.of(),
                        "targetPas", evt.targetPas() != null ? evt.targetPas() : "",
                        "accountServiceRequestNumber", evt.accountServiceRequestNumber() != null ? evt.accountServiceRequestNumber() : ""
                )));
            })
            .to("http://{{crm40x1.url}}/customer/update?bridgeEndpoint=true")
            .process(exchange -> {
                String correlationId = exchange.getProperty("correlationId", String.class);
                String externalAccountId = exchange.getProperty("externalAccountId", String.class);

                CustomerUpdatedEvent event = new CustomerUpdatedEvent(
                        correlationId,
                        externalAccountId,
                        List.of("policyNumbers"),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", correlationId);
                log.info("[EDA publish] CustomerIdentityAndRelationshipManagement publishing CustomerUpdatedEvent — issuanceId={}", correlationId);
                MDC.clear();
            })
            .to("kafka:customer.events.customer-updated");
    }
}

