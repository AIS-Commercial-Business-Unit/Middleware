package com.ais.middleware.customer.identity.routes;

import com.ais.middleware.common.events.customer.AccountServiceRecordRetrievedEvent;
import com.ais.middleware.common.events.customer.CustomerUpdatedEvent;
import com.ais.middleware.common.events.customer.GetOrCreateAccountServiceRecordCommand;
import com.ais.middleware.common.events.customer.UpdateCustomerRecordCommand;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Handles account service record requests.
 * Looks up or creates AccountServiceRequestNumber via ERM7X1 and publishes the result.
 * Also handles CRM40X1 customer updates after PAS confirms policy.
 */
@Component
public class AccountServiceRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(AccountServiceRoute.class);

    private final ObjectMapper objectMapper;

    public AccountServiceRoute(ObjectMapper objectMapper) {
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
                log.error("Unhandled exception in account-service route — routing to DLQ. routeId={} error={}",
                        exchange.getFromRouteId(),
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:customer.dlq.account-service");

        // ── Get-or-create account service record ─────────────────────────────
        from("kafka:customer.commands.get-or-create-account-record?groupId=customer-identity-service")
            .routeId("account-lookup")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                GetOrCreateAccountServiceRecordCommand cmd = objectMapper.readValue(json, GetOrCreateAccountServiceRecordCommand.class);
                MDC.put("issuanceId", cmd.correlationId());
                log.info("GetOrCreateAccountServiceRecord received — correlationId={}", cmd.correlationId());
                exchange.setProperty("correlationId", cmd.correlationId());
                exchange.setProperty("externalAccountId", cmd.externalAccountId());
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
                log.info("ERM7X1 response received — publishing AccountServiceRecordRetrieved correlationId={}", correlationId);
                MDC.clear();
            })
            .to("kafka:customer.events.account-service-record-retrieved");

        // ── Update customer record after PAS confirmation ─────────────────────
        from("kafka:customer.commands.update-customer-record?groupId=customer-identity-service")
            .routeId("customer-update")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                UpdateCustomerRecordCommand cmd = objectMapper.readValue(json, UpdateCustomerRecordCommand.class);
                MDC.put("issuanceId", cmd.correlationId());
                log.info("UpdateCustomerRecord received — correlationId={}", cmd.correlationId());
                exchange.setProperty("correlationId", cmd.correlationId());
                exchange.setProperty("externalAccountId", cmd.externalAccountId());
                exchange.getIn().setBody(json);
            })
            .to("http://{{crm40x1.url}}/customer/update?bridgeEndpoint=true")
            .process(exchange -> {
                String correlationId = exchange.getProperty("correlationId", String.class);
                String externalAccountId = exchange.getProperty("externalAccountId", String.class);

                CustomerUpdatedEvent event = new CustomerUpdatedEvent(
                        correlationId,
                        externalAccountId,
                        List.of("billingAddress", "policyNumbers"),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", correlationId);
                log.info("CRM40X1 updated — publishing CustomerUpdated correlationId={}", correlationId);
                MDC.clear();
            })
            .to("kafka:customer.events.customer-updated");
    }
}

