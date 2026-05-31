package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by AppraisalReceivedSaga when it needs the producer cross-reference for a policy.
 * Consumed by: customer-identity-service (ProducerLookupRoute).
 *
 * Topic: prs.events.producer-lookup-requested
 */
public record ProducerLookupRequestedEvent(
        String correlationId,
        String policyNumber,
        OffsetDateTime requestedAt
) {
    @JsonCreator
    public ProducerLookupRequestedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.correlationId = correlationId;
        this.policyNumber = policyNumber;
        this.requestedAt = requestedAt;
    }
}
