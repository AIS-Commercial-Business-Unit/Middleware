package com.ais.middleware.prs.appraisal.observability;

import org.apache.camel.Exchange;
import org.apache.camel.Processor;
import org.apache.camel.component.kafka.KafkaConstants;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.util.Map;

/**
 * Emits structured EDA_FLOW log entries at every Kafka consume/publish boundary.
 * Wired via interceptFrom / interceptSendToEndpoint in route builders.
 */
@Component
public class EDAFlowProcessor implements Processor {

    private static final Logger log = LoggerFactory.getLogger(EDAFlowProcessor.class);

    @Value("${spring.application.name:prs-appraisal-service}")
    private String serviceName;

    private static final Map<String, String> TOPIC_TO_PUBLISHER = Map.of(
            "prs.events.appraisal-list-retrieved", "AppraisalService",
            "prs.events.document-retrieved", "AppraisalService"
    );

    private static final Map<String, String> TOPIC_TO_CONSUMER = Map.of(
            "prs.events.appraisal-list-retrieved", "Audit",
            "prs.events.document-retrieved", "Audit"
    );

    private static final Map<String, String> TOPIC_TO_MESSAGE_TYPE = Map.of(
            "prs.events.appraisal-list-retrieved", "AppraisalListRetrievedEvent",
            "prs.events.document-retrieved", "DocumentRetrievedEvent"
    );

    private static final Map<String, String> SERVICE_TO_PARTICIPANT = Map.of(
            "prs-appraisal-service", "AppraisalService",
            "platform-integration-service", "Integration"
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

        String correlationId = resolveCorrelationId(exchange);
        if (correlationId == null || correlationId.isBlank()) {
            return;
        }

        String messageType = resolveMessageType(exchange, topic);
        String currentParticipant = SERVICE_TO_PARTICIPANT.getOrDefault(serviceName, serviceName);

        String from;
        String to;
        if ("published".equals(direction)) {
            from = currentParticipant;
            to = TOPIC_TO_CONSUMER.getOrDefault(topic, "?");
        } else {
            from = TOPIC_TO_PUBLISHER.getOrDefault(topic, "?");
            to = currentParticipant;
        }

        MDC.put("EDA_Event", "EDA_FLOW");
        MDC.put("EDA_IssuanceId", correlationId);
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

    private String resolveCorrelationId(Exchange exchange) {
        String id = exchange.getIn().getHeader("correlationId", String.class);
        if (id != null) return id;
        id = MDC.get("correlationId");
        if (id != null) return id;
        id = exchange.getIn().getHeader("CorrelationId", String.class);
        if (id != null) return id;
        return MDC.get("CorrelationId");
    }

    private String resolveMessageType(Exchange exchange, String topic) {
        String typeId = exchange.getIn().getHeader("__TypeId__", String.class);
        if (typeId != null) {
            return typeId.contains(".") ? typeId.substring(typeId.lastIndexOf('.') + 1) : typeId;
        }
        String mappedType = TOPIC_TO_MESSAGE_TYPE.get(topic);
        if (mappedType != null) return mappedType;
        Object body = exchange.getIn().getBody();
        return body != null ? body.getClass().getSimpleName() : "UnknownMessage";
    }
}
