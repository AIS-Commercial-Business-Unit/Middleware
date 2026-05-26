package com.ais.middleware.common.events.integration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by Platform.Integration when a PAS call fails or times out.
 * The IssuanceSaga retries IssuePolicyRequestedEvent up to 3 times before publishing IssuanceFailedEvent (BR-INT-003).
 */
public record PolicyAdminSystemCallFailedEvent(
        String issuanceId,
        String targetPas,
        String failureReason,
        OffsetDateTime failedAt
) {
    @JsonCreator
    public PolicyAdminSystemCallFailedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("targetPas") String targetPas,
            @JsonProperty("failureReason") String failureReason,
            @JsonProperty("failedAt") OffsetDateTime failedAt
    ) {
        this.issuanceId = issuanceId;
        this.targetPas = targetPas;
        this.failureReason = failureReason;
        this.failedAt = failedAt;
    }
}
