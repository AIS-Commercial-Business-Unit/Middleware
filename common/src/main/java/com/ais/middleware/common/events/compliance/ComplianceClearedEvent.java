package com.ais.middleware.common.events.compliance;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by Platform.Compliance when sanctions screening returns Clear or when a ComplianceClearanceSaga is manually cleared.
 * The correlationId equals the originating IssuanceId and is required for saga resumption.
 */
public record ComplianceClearedEvent(
        String checkId,
        String correlationId,
        String sourceDomain,
        String checkType,
        String subjectId,
        OffsetDateTime clearedAt
) {
    @JsonCreator
    public ComplianceClearedEvent(
            @JsonProperty("checkId") String checkId,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("sourceDomain") String sourceDomain,
            @JsonProperty("checkType") String checkType,
            @JsonProperty("subjectId") String subjectId,
            @JsonProperty("clearedAt") OffsetDateTime clearedAt
    ) {
        this.checkId = checkId;
        this.correlationId = correlationId;
        this.sourceDomain = sourceDomain;
        this.checkType = checkType;
        this.subjectId = subjectId;
        this.clearedAt = clearedAt;
    }
}
