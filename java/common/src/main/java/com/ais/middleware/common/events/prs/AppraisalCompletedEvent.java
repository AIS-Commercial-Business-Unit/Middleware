package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published when StatusCode=15 (Completed) flow finishes.
 * Masterpiece Transaction 90 has been called and appraisal closed.
 *
 * Topic: prs.events.appraisal-completed
 */
public record AppraisalCompletedEvent(
        String correlationId,
        String appraisalId,
        String policyNumber,
        OffsetDateTime completedAt
) {
    @JsonCreator
    public AppraisalCompletedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("appraisalId") String appraisalId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("completedAt") OffsetDateTime completedAt
    ) {
        this.correlationId = correlationId;
        this.appraisalId = appraisalId;
        this.policyNumber = policyNumber;
        this.completedAt = completedAt;
    }
}
