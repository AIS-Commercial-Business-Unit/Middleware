package com.ais.middleware.platform.integration.observability;

import org.apache.camel.Exchange;
import org.apache.camel.Processor;
import org.apache.camel.component.kafka.KafkaConstants;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.util.Map;

@Component
public class EDAFlowProcessor implements Processor {

    private static final Logger log = LoggerFactory.getLogger(EDAFlowProcessor.class);

    @Value("${spring.application.name:platform-integration-service}")
    private String serviceName;

    private static final Map<String, String> TOPIC_TO_PUBLISHER = Map.ofEntries(
            Map.entry("policy.commands.issue-policy", "API"),
            Map.entry("policy.events.policy-issuance-initiated", "PolicyIssuance"),
            Map.entry("policy.events.issue-policy-requested", "PolicyIssuance"),
            Map.entry("policy.events.policy-issued", "PolicyIssuance"),
            Map.entry("policy.events.issuance-failed", "PolicyIssuance"),
            Map.entry("compliance.events.compliance-cleared", "Compliance"),
            Map.entry("compliance.events.compliance-blocked", "Compliance"),
            Map.entry("customer.events.account-lookup-requested", "PolicyIssuance"),
            Map.entry("customer.events.account-service-record-retrieved", "CustomerIdentity"),
            Map.entry("customer.events.customer-updated", "CustomerIdentity"),
            Map.entry("integration.events.policy-admin-system-response-received", "Integration"),
            Map.entry("integration.events.policy-admin-system-call-failed", "Integration"),
            Map.entry("billing.events.billing-association-created", "Billing")
    );

    private static final Map<String, String> TOPIC_TO_CONSUMER = Map.ofEntries(
            Map.entry("policy.commands.issue-policy", "PolicyIssuance"),
            Map.entry("policy.events.policy-issuance-initiated", "Compliance"),
            Map.entry("policy.events.issue-policy-requested", "Integration"),
            Map.entry("policy.events.policy-issued", "PolicyIssuance"),
            Map.entry("policy.events.issuance-failed", "PolicyIssuance"),
            Map.entry("compliance.events.compliance-cleared", "PolicyIssuance"),
            Map.entry("compliance.events.compliance-blocked", "PolicyIssuance"),
            Map.entry("customer.events.account-lookup-requested", "CustomerIdentity"),
            Map.entry("customer.events.account-service-record-retrieved", "PolicyIssuance"),
            Map.entry("customer.events.customer-updated", "PolicyIssuance"),
            Map.entry("integration.events.policy-admin-system-response-received", "PolicyIssuance"),
            Map.entry("integration.events.policy-admin-system-call-failed", "PolicyIssuance"),
            Map.entry("billing.events.billing-association-created", "PolicyIssuance")
    );

    private static final Map<String, String> TOPIC_TO_MESSAGE_TYPE = Map.ofEntries(
            Map.entry("policy.commands.issue-policy", "IssuePolicyCommand"),
            Map.entry("policy.events.policy-issuance-initiated", "PolicyIssuanceInitiatedEvent"),
            Map.entry("policy.events.issue-policy-requested", "IssuePolicyRequestedEvent"),
            Map.entry("policy.events.policy-issued", "PolicyIssuedEvent"),
            Map.entry("policy.events.issuance-failed", "IssuanceFailedEvent"),
            Map.entry("compliance.events.compliance-cleared", "ComplianceClearedEvent"),
            Map.entry("compliance.events.compliance-blocked", "ComplianceBlockedEvent"),
            Map.entry("customer.events.account-lookup-requested", "AccountLookupRequestedEvent"),
            Map.entry("customer.events.account-service-record-retrieved", "AccountServiceRecordRetrievedEvent"),
            Map.entry("customer.events.customer-updated", "CustomerUpdatedEvent"),
            Map.entry("integration.events.policy-admin-system-response-received", "PolicyAdminSystemResponseReceivedEvent"),
            Map.entry("integration.events.policy-admin-system-call-failed", "PolicyAdminSystemCallFailedEvent"),
            Map.entry("billing.events.billing-association-created", "BillingAssociationCreatedEvent")
    );

    private static final Map<String, String> SERVICE_TO_PARTICIPANT = Map.of(
            "policy-issuance-service", "PolicyIssuance",
            "platform-compliance-service", "Compliance",
            "customer-identity-service", "CustomerIdentity",
            "platform-integration-service", "Integration",
            "billing-finance-service", "Billing",
            "platform-notification-service", "Notification"
    );

    @Override
    public void process(Exchange exchange) {
        String topic = exchange.getIn().getHeader(KafkaConstants.TOPIC, String.class);
        String direction = exchange.getProperty("EDA_FLOW_DIRECTION", String.class);
        if (topic == null || direction == null) {
            return;
        }

        if ("published".equals(direction) && exchange.getFromRouteId() == null) {
            return;
        }

        String issuanceId = resolveIssuanceId(exchange);
        if (issuanceId == null || issuanceId.isBlank()) {
            return;
        }

        String messageType = resolveMessageType(exchange, topic);
        String currentParticipant = SERVICE_TO_PARTICIPANT.getOrDefault(serviceName, serviceName);

        String from;
        String to;
        if ("published".equals(direction)) {
            from = currentParticipant;
            to = TOPIC_TO_CONSUMER.getOrDefault(topic, TOPIC_TO_PUBLISHER.getOrDefault(topic, "?"));
        } else {
            from = TOPIC_TO_PUBLISHER.getOrDefault(topic, "?");
            to = currentParticipant;
        }

        MDC.put("EDA_Event", "EDA_FLOW");
        MDC.put("EDA_IssuanceId", issuanceId);
        MDC.put("EDA_MessageType", messageType);
        MDC.put("EDA_From", from);
        MDC.put("EDA_To", to);
        MDC.put("EDA_Topic", topic);
        MDC.put("EDA_Direction", direction);
        MDC.put("EDA_Stack", "java");
        try {
            log.info("EDA_FLOW {} {} -> {}", messageType, from, to);
        } finally {
            MDC.remove("EDA_Event");
            MDC.remove("EDA_IssuanceId");
            MDC.remove("EDA_MessageType");
            MDC.remove("EDA_From");
            MDC.remove("EDA_To");
            MDC.remove("EDA_Topic");
            MDC.remove("EDA_Direction");
            MDC.remove("EDA_Stack");
        }
    }

    private String resolveIssuanceId(Exchange exchange) {
        String id = exchange.getIn().getHeader("issuanceId", String.class);
        if (id != null) {
            return id;
        }

        id = exchange.getProperty("issuanceId", String.class);
        if (id != null) {
            return id;
        }

        id = exchange.getProperty("correlationId", String.class);
        if (id != null) {
            return id;
        }

        id = MDC.get("issuanceId");
        if (id != null) {
            return id;
        }

        Object body = exchange.getIn().getBody();
        if (body != null) {
            try {
                var method = body.getClass().getMethod("getIssuanceId");
                Object value = method.invoke(body);
                if (value != null) {
                    return value.toString();
                }
            } catch (Exception ignored) {
            }
        }
        return null;
    }

    private String resolveMessageType(Exchange exchange, String topic) {
        String typeId = exchange.getIn().getHeader("__TypeId__", String.class);
        if (typeId != null) {
            return typeId.contains(".") ? typeId.substring(typeId.lastIndexOf('.') + 1) : typeId;
        }

        String mappedType = TOPIC_TO_MESSAGE_TYPE.get(topic);
        if (mappedType != null) {
            return mappedType;
        }

        Object body = exchange.getIn().getBody();
        return body != null ? body.getClass().getSimpleName() : "UnknownMessage";
    }
}
