package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by StatusCode6UWSaga to trigger parallel UW assignment determination.
 * Topic: prs.events.uw-determination-requested
 *
 * ⚠️ DEMO GAP: UW determination rules not confirmed — routing logic (UA vs UST)
 * is inferred from inspectionTypeCode (A→UA, B→UST, I/J→UA).
 * Real rule codes need to be provided by PRS team.
 */
public record UWDeterminationRequestedEvent(
        String correlationId,
        String appraisalId,
        String policyNumber,
        String inspectionTypeCode,
        String producerControlCode,
        OffsetDateTime requestedAt
) {
    @JsonCreator
    public UWDeterminationRequestedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("appraisalId") String appraisalId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("inspectionTypeCode") String inspectionTypeCode,
            @JsonProperty("producerControlCode") String producerControlCode,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.correlationId = correlationId;
        this.appraisalId = appraisalId;
        this.policyNumber = policyNumber;
        this.inspectionTypeCode = inspectionTypeCode;
        this.producerControlCode = producerControlCode;
        this.requestedAt = requestedAt;
    }
}
