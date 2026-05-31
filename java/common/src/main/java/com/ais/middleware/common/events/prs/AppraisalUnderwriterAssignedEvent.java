package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published when the AppraisalReceivedSaga completes StatusCode=6 (UW Determination) flow.
 * Signals that an underwriter has been assigned and downstream systems have been updated.
 *
 * Topic: prs.events.appraisal-uw-assigned
 */
public record AppraisalUnderwriterAssignedEvent(
        String correlationId,
        String appraisalId,
        String policyNumber,
        String uwAssignment,
        int suspenseDays,
        String pluwReferenceId,
        OffsetDateTime assignedAt
) {
    @JsonCreator
    public AppraisalUnderwriterAssignedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("appraisalId") String appraisalId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("uwAssignment") String uwAssignment,
            @JsonProperty("suspenseDays") int suspenseDays,
            @JsonProperty("pluwReferenceId") String pluwReferenceId,
            @JsonProperty("assignedAt") OffsetDateTime assignedAt
    ) {
        this.correlationId = correlationId;
        this.appraisalId = appraisalId;
        this.policyNumber = policyNumber;
        this.uwAssignment = uwAssignment;
        this.suspenseDays = suspenseDays;
        this.pluwReferenceId = pluwReferenceId;
        this.assignedAt = assignedAt;
    }
}
