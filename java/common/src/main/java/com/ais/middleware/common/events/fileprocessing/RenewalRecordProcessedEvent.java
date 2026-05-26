package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Published by policy-issuance-service when an AutomatedRenewal saga completes successfully.
 * Consumed by platform-file-processing-service to update batch progress counters.
 */
public record RenewalRecordProcessedEvent(
        String recordId,
        String batchId,
        String issuanceId,
        List<String> policyNumbers,
        OffsetDateTime processedAt
) {
    @JsonCreator
    public RenewalRecordProcessedEvent(
            @JsonProperty("recordId") String recordId,
            @JsonProperty("batchId") String batchId,
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("policyNumbers") List<String> policyNumbers,
            @JsonProperty("processedAt") OffsetDateTime processedAt
    ) {
        this.recordId = recordId;
        this.batchId = batchId;
        this.issuanceId = issuanceId;
        this.policyNumbers = policyNumbers;
        this.processedAt = processedAt;
    }
}
