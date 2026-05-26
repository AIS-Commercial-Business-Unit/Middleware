package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by policy-issuance-service when an AutomatedRenewal saga fails.
 * Consumed by platform-file-processing-service to update batch failure counters.
 */
public record RenewalRecordFailedEvent(
        String recordId,
        String batchId,
        String issuanceId,
        String failureReason,
        String failureCategory,
        OffsetDateTime failedAt
) {
    @JsonCreator
    public RenewalRecordFailedEvent(
            @JsonProperty("recordId") String recordId,
            @JsonProperty("batchId") String batchId,
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("failureReason") String failureReason,
            @JsonProperty("failureCategory") String failureCategory,
            @JsonProperty("failedAt") OffsetDateTime failedAt
    ) {
        this.recordId = recordId;
        this.batchId = batchId;
        this.issuanceId = issuanceId;
        this.failureReason = failureReason;
        this.failureCategory = failureCategory;
        this.failedAt = failedAt;
    }
}
