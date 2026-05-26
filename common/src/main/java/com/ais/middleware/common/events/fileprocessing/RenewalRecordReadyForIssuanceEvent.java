package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by platform-file-processing-service for each valid CSV row in a renewal batch file.
 * The correlationId becomes the IssuanceSaga correlation key (= issuanceId) in policy-issuance-service.
 */
public record RenewalRecordReadyForIssuanceEvent(
        String recordId,
        String batchId,
        String correlationId,
        int sequenceNumber,
        String rawContent,
        String fileType,
        String accountId,
        int policyTypeCode,
        int policyTypeSubCode,
        String policyNumber,
        OffsetDateTime publishedAt
) {
    @JsonCreator
    public RenewalRecordReadyForIssuanceEvent(
            @JsonProperty("recordId") String recordId,
            @JsonProperty("batchId") String batchId,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("sequenceNumber") int sequenceNumber,
            @JsonProperty("rawContent") String rawContent,
            @JsonProperty("fileType") String fileType,
            @JsonProperty("accountId") String accountId,
            @JsonProperty("policyTypeCode") int policyTypeCode,
            @JsonProperty("policyTypeSubCode") int policyTypeSubCode,
            @JsonProperty("policyNumber") String policyNumber,
            @JsonProperty("publishedAt") OffsetDateTime publishedAt
    ) {
        this.recordId = recordId;
        this.batchId = batchId;
        this.correlationId = correlationId;
        this.sequenceNumber = sequenceNumber;
        this.rawContent = rawContent;
        this.fileType = fileType;
        this.accountId = accountId;
        this.policyTypeCode = policyTypeCode;
        this.policyTypeSubCode = policyTypeSubCode;
        this.policyNumber = policyNumber;
        this.publishedAt = publishedAt;
    }
}
