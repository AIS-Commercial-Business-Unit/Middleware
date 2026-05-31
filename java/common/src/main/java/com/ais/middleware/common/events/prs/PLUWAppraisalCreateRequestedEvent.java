package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by StatusCode6UWSaga to trigger parallel PLUW appraisal/inspection creation.
 * Topic: prs.events.pluw-appraisal-create-requested
 *
 * ⚠️ DEMO GAP: Actual PLUW WCF-WSHTTP request schema not yet provided by PRS team.
 */
public record PLUWAppraisalCreateRequestedEvent(
        String correlationId,
        String appraisalId,
        String policyNumber,
        String inspectionTypeCode,
        // ⚠️ DEMO GAP: PLUW request fields — names and types need validation from PRS team
        String producerCode,
        OffsetDateTime requestedAt
) {
    @JsonCreator
    public PLUWAppraisalCreateRequestedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("appraisalId") String appraisalId,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("inspectionTypeCode") String inspectionTypeCode,
            @JsonProperty("producerCode") String producerCode,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.correlationId = correlationId;
        this.appraisalId = appraisalId;
        this.policyNumber = policyNumber;
        this.inspectionTypeCode = inspectionTypeCode;
        this.producerCode = producerCode;
        this.requestedAt = requestedAt;
    }
}
