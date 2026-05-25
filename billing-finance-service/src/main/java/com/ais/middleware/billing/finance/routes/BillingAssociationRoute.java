package com.ais.middleware.billing.finance.routes;

import com.ais.middleware.common.events.billing.AssociateBillingAccountCommand;
import com.ais.middleware.common.events.billing.BillingAssociationCreatedEvent;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.builder.RouteBuilder;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;

/**
 * Handles billing account association requests.
 * Subscribes to AssociateBillingAccount commands (parallel branch of IssuanceSaga).
 * Calls CRM19X1 for DirectBill and publishes BillingAssociationCreated.
 */
@Component
public class BillingAssociationRoute extends RouteBuilder {

    private final ObjectMapper objectMapper;

    public BillingAssociationRoute(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public void configure() throws Exception {
        from("kafka:billing.commands.associate-billing-account?groupId=billing-finance-service")
            .routeId("billing-association")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                AssociateBillingAccountCommand cmd = objectMapper.readValue(json, AssociateBillingAccountCommand.class);
                exchange.setProperty("issuanceId", cmd.issuanceId());
                exchange.setProperty("accountServiceRequestNumber", cmd.accountServiceRequestNumber());
                exchange.setProperty("billingChannel", cmd.billingChannel() != null
                        ? cmd.billingChannel().name() : "DirectBill");
                exchange.getIn().setBody(json);
            })
            .log("AssociateBillingAccount received for issuanceId=${exchangeProperty.issuanceId}")
            .to("http://{{crm19x1.url}}/billing/associate?bridgeEndpoint=true")
            .process(exchange -> {
                String responseBody = exchange.getIn().getBody(String.class);
                JsonNode resp = objectMapper.readTree(responseBody);
                String issuanceId = exchange.getProperty("issuanceId", String.class);
                String accountServiceRequestNumber = exchange.getProperty("accountServiceRequestNumber", String.class);
                String channelStr = exchange.getProperty("billingChannel", String.class);

                BillingAssociationCreatedEvent.BillingChannel channel;
                try {
                    channel = BillingAssociationCreatedEvent.BillingChannel.valueOf(channelStr);
                } catch (Exception e) {
                    channel = BillingAssociationCreatedEvent.BillingChannel.DirectBill;
                }

                BillingAssociationCreatedEvent event = new BillingAssociationCreatedEvent(
                        resp.path("billingAccountId").asText("BILL-UNKNOWN"),
                        issuanceId,
                        accountServiceRequestNumber,
                        channel,
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", issuanceId);
            })
            .log("CRM19X1 billing association complete — publishing BillingAssociationCreated for ${exchangeProperty.issuanceId}")
            .to("kafka:billing.events.billing-association-created");
    }
}

