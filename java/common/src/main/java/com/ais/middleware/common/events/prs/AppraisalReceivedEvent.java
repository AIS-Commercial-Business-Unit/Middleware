package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by Platform.Integration when a RiskID status update is received.
 *
 * Consumed by: Appraisal domain service (AppraisalReceivedSaga).
 *
 * ⚠️ DEMO GAP: Actual RiskID schema needed from integration team.
 * Fields below are inferred from BizTalk orchestration analysis — real MQ message
 * structure may differ (field names, data types, additional fields).
 *
 * Topic: prs.events.appraisal-received
 */
public record AppraisalReceivedEvent(
        String correlationId,
        String policyNumber,
        // ⚠️ DEMO GAP: Actual RiskID schema needed from integration team
        String appraisalId,
        int statusCode,
        String inspectionTypeCode,
        OffsetDateTime receivedAt
) {
    @JsonCreator
    public AppraisalReceivedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("appraisalId") String appraisalId,
            @JsonProperty("statusCode") int statusCode,
            @JsonProperty("inspectionTypeCode") String inspectionTypeCode,
            @JsonProperty("receivedAt") OffsetDateTime receivedAt
    ) {
        this.correlationId = correlationId;
        this.policyNumber = policyNumber;
        this.appraisalId = appraisalId;
        this.statusCode = statusCode;
        this.inspectionTypeCode = inspectionTypeCode;
        this.receivedAt = receivedAt;
    }
}
