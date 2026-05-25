package com.ais.middleware.customer.identity.routes;

import com.ais.middleware.common.events.customer.AccountServiceRecordRetrievedEvent;
import com.ais.middleware.common.events.customer.CustomerUpdatedEvent;
import com.ais.middleware.common.events.customer.GetOrCreateAccountServiceRecordCommand;
import com.ais.middleware.common.events.customer.UpdateCustomerRecordCommand;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.builder.RouteBuilder;
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

    private final ObjectMapper objectMapper;

    public AccountServiceRoute(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public void configure() throws Exception {

        // ── Get-or-create account service record ─────────────────────────────
        from("kafka:customer.commands.get-or-create-account-record?groupId=customer-identity-service")
            .routeId("account-lookup")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                GetOrCreateAccountServiceRecordCommand cmd = objectMapper.readValue(json, GetOrCreateAccountServiceRecordCommand.class);
                exchange.setProperty("correlationId", cmd.correlationId());
                exchange.setProperty("externalAccountId", cmd.externalAccountId());
                exchange.getIn().setBody(json);
            })
            .log("AccountServiceRecord request received for correlationId=${exchangeProperty.correlationId}")
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
            })
            .log("ERM7X1 response received — publishing AccountServiceRecordRetrieved for ${exchangeProperty.correlationId}")
            .to("kafka:customer.events.account-service-record-retrieved");

        // ── Update customer record after PAS confirmation ─────────────────────
        from("kafka:customer.commands.update-customer-record?groupId=customer-identity-service")
            .routeId("customer-update")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                UpdateCustomerRecordCommand cmd = objectMapper.readValue(json, UpdateCustomerRecordCommand.class);
                exchange.setProperty("correlationId", cmd.correlationId());
                exchange.setProperty("externalAccountId", cmd.externalAccountId());
                exchange.getIn().setBody(json);
            })
            .log("UpdateCustomerRecord request received for correlationId=${exchangeProperty.correlationId}")
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
            })
            .log("CRM40X1 updated — publishing CustomerUpdated for ${exchangeProperty.correlationId}")
            .to("kafka:customer.events.customer-updated");
    }
}

