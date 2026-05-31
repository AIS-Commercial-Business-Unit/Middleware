package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published when the AppraisalReceivedSaga fails (gateway timeout, retry exhaustion, etc.).
 * Message is routed to DLQ after this event.
 *
 * Topic: prs.events.appraisal-status-update-failed
 */
public record AppraisalStatusUpdateFailedEvent(
        String correlationId,
        String appraisalId,
        String policyNumber,
        String failureReason,
        String failedStep,
        OffsetDateTime failedAt
) {
    @JsonCreator
    public AppraisalStatusUpdateFailedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("appraisalId") String appraisalId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("failureReason") String failureReason,
            @JsonProperty("failedStep") String failedStep,
            @JsonProperty("failedAt") OffsetDateTime failedAt
    ) {
        this.correlationId = correlationId;
        this.appraisalId = appraisalId;
        this.policyNumber = policyNumber;
        this.failureReason = failureReason;
        this.failedStep = failedStep;
        this.failedAt = failedAt;
    }
}
