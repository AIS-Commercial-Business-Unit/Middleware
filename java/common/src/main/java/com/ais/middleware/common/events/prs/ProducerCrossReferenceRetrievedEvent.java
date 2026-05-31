package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by customer-identity-service after retrieving the producer cross-reference.
 * Consumed by: AppraisalReceivedSaga (prs-appraisal-service).
 *
 * ⚠️ DEMO GAP: ProducerControlCode routing logic unknown — UA vs UST rules need PRS team input.
 * Currently using inspectionTypeCode (A/B/I/J) as a proxy for routing decision.
 *
 * Topic: prs.events.producer-crossref-retrieved
 */
public record ProducerCrossReferenceRetrievedEvent(
        String correlationId,
        String policyNumber,
        // ⚠️ DEMO GAP: Real field names from CustomerDB stored procedure unknown
        String producerCode,
        String producerControlCode,
        OffsetDateTime retrievedAt
) {
    @JsonCreator
    public ProducerCrossReferenceRetrievedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("producerCode") String producerCode,
            @JsonProperty("producerControlCode") String producerControlCode,
            @JsonProperty("retrievedAt") OffsetDateTime retrievedAt
    ) {
        this.correlationId = correlationId;
        this.policyNumber = policyNumber;
        this.producerCode = producerCode;
        this.producerControlCode = producerControlCode;
        this.retrievedAt = retrievedAt;
    }
}
