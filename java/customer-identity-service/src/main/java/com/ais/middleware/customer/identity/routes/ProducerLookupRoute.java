package com.ais.middleware.customer.identity.routes;

import com.ais.middleware.common.events.prs.ProducerCrossReferenceRetrievedEvent;
import com.ais.middleware.common.events.prs.ProducerLookupRequestedEvent;
import com.ais.middleware.customer.identity.observability.EDAFlowProcessor;
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

/**
 * Handles producer cross-reference lookups for UC4 Appraisal workflow.
 *
 * Subscribes to: prs.events.producer-lookup-requested
 * Publishes:     prs.events.producer-crossref-retrieved
 *
 * ⚠️ DEMO GAP: Real CustomerDB schema needed — this stub uses an in-memory lookup table
 * with REPLACE_ME_ placeholder values. Actual stored procedure name and SQL schema unknown.
 *
 * Producer control code → UW routing:
 *   "UA-001"  → UA (Underwriting Associate) — 45-day suspense (ASSUMED)
 *   "UST-001" → UST (Underwriting Specialist Team) — 14-day suspense (ASSUMED)
 * These routing rules need PRS team confirmation.
 */
@Component
public class ProducerLookupRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(ProducerLookupRoute.class);

    private final ObjectMapper objectMapper;
    private final EDAFlowProcessor edaFlowProcessor;

    /**
     * ⚠️ DEMO GAP: REPLACE_ME_ prefixes mark all fabricated test data.
     * Real producer codes must be provided by PRS team from CustomerDB.
     */
    private static final Map<String, String[]> PRODUCER_LOOKUP = Map.of(
            "POL-12345", new String[]{"REPLACE_ME_PROD001", "UA-001"},
            "POL-12346", new String[]{"REPLACE_ME_PROD002", "UST-001"},
            "POL-12347", new String[]{"REPLACE_ME_PROD003", "UA-001"},
            "POL-12348", new String[]{"REPLACE_ME_PROD004", "UA-001"},
            "POL-12349", new String[]{"REPLACE_ME_PROD005", "UST-001"}
    );

    public ProducerLookupRoute(ObjectMapper objectMapper, EDAFlowProcessor edaFlowProcessor) {
        this.objectMapper = objectMapper;
        this.edaFlowProcessor = edaFlowProcessor;
    }

    @Override
    public void configure() throws Exception {

        // DLQ handler
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                log.error("Unhandled exception in producer-lookup route — routing to DLQ. error={}",
                        cause != null ? cause.getMessage() : "unknown", cause);
            })
            .to("kafka:customer.dlq.producer-lookup");

        from("kafka:prs.events.producer-lookup-requested?groupId=customer-identity-service-producer-lookup")
            .routeId("producer-lookup")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                ProducerLookupRequestedEvent evt = objectMapper.readValue(json, ProducerLookupRequestedEvent.class);

                MDC.put("correlationId", evt.correlationId());
                log.info("[EDA subscriber] CustomerIdentity received ProducerLookupRequestedEvent — correlationId={} policyNumber={}",
                        evt.correlationId(), evt.policyNumber());
                log.warn("⚠️ STUBBED: CustomerDBGateway called for producer lookup — policyNumber={}", evt.policyNumber());
                log.warn("⚠️ DEMO GAP: Real CustomerDB stored procedure name and schema unknown — using in-memory REPLACE_ME_ data");

                // In-memory lookup — ⚠️ DEMO GAP: replace with real CustomerDB call
                String[] producerData = PRODUCER_LOOKUP.get(evt.policyNumber());
                String producerCode;
                String producerControlCode;

                if (producerData != null) {
                    producerCode = producerData[0];
                    producerControlCode = producerData[1];
                    log.info("Producer lookup hit — policyNumber={} producerCode={} producerControlCode={}",
                            evt.policyNumber(), producerCode, producerControlCode);
                } else {
                    // ⚠️ DEMO GAP: Unknown policy — default to REPLACE_ME values
                    producerCode = "REPLACE_ME_UNKNOWN_PRODUCER";
                    producerControlCode = "UA-001";
                    log.warn("⚠️ DEMO GAP: policyNumber={} not in demo lookup table — defaulting to REPLACE_ME_UNKNOWN_PRODUCER / UA-001",
                            evt.policyNumber());
                }

                ProducerCrossReferenceRetrievedEvent response = new ProducerCrossReferenceRetrievedEvent(
                        evt.correlationId(),
                        evt.policyNumber(),
                        producerCode,
                        producerControlCode,
                        OffsetDateTime.now());

                exchange.getIn().setBody(objectMapper.writeValueAsString(response));
                exchange.getIn().setHeader("correlationId", evt.correlationId());
                exchange.getIn().setHeader(KafkaConstants.TOPIC, "prs.events.producer-crossref-retrieved");

                log.info("[EDA publish] CustomerIdentity publishing ProducerCrossReferenceRetrievedEvent — correlationId={} producerCode={} producerControlCode={}",
                        evt.correlationId(), producerCode, producerControlCode);
                MDC.clear();
            })
            .to("kafka:prs.events.producer-crossref-retrieved");
    }
}
