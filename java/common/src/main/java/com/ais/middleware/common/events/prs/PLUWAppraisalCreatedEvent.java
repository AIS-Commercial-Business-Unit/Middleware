package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by PLUWGateway stub after successfully creating the appraisal/inspection record.
 * Topic: prs.events.pluw-appraisal-created
 *
 * ⚠️ DEMO GAP: Real PLUW response fields not confirmed — pluwReferenceId is fabricated.
 */
public record PLUWAppraisalCreatedEvent(
        String correlationId,
        // ⚠️ DEMO GAP: Actual PLUW reference ID format unknown
        String pluwReferenceId,
        boolean success,
        OffsetDateTime createdAt
) {
    @JsonCreator
    public PLUWAppraisalCreatedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("pluwReferenceId") String pluwReferenceId,
            @JsonProperty("success") boolean success,
            @JsonProperty("createdAt") OffsetDateTime createdAt
    ) {
        this.correlationId = correlationId;
        this.pluwReferenceId = pluwReferenceId;
        this.success = success;
        this.createdAt = createdAt;
    }
}
