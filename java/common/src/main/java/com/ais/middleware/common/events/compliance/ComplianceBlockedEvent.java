package com.ais.middleware.common.events.compliance;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by Platform.Compliance when sanctions screening returns Blocked or the ComplianceClearanceSaga times out at 72 hours (BR-CMP-003).
 * A blocked result is final, so no PAS call is made and the IssuanceSaga terminates by publishing IssuanceFailedEvent.
 */
public record ComplianceBlockedEvent(
        String checkId,
        String correlationId,
        String sourceDomain,
        String blockReason,
        OffsetDateTime blockedAt
) {
    @JsonCreator
    public ComplianceBlockedEvent(
            @JsonProperty("checkId") String checkId,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("sourceDomain") String sourceDomain,
            @JsonProperty("blockReason") String blockReason,
            @JsonProperty("blockedAt") OffsetDateTime blockedAt
    ) {
        this.checkId = checkId;
        this.correlationId = correlationId;
        this.sourceDomain = sourceDomain;
        this.blockReason = blockReason;
        this.blockedAt = blockedAt;
    }
}
