package com.ais.middleware.common.events.policy;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by PolicyIssuanceAndLifecycleManagement on any unrecoverable failure path in the issuance flow.
 * This covers compliance blocks, PAS retry exhaustion after 3 attempts, and the 2-hour saga timeout (BR-PIL-004).
 */
public record IssuanceFailedEvent(
        String issuanceId,
        String failureReason,
        OffsetDateTime failedAt
) {
    @JsonCreator
    public IssuanceFailedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("failureReason") String failureReason,
            @JsonProperty("failedAt") OffsetDateTime failedAt
    ) {
        this.issuanceId = issuanceId;
        this.failureReason = failureReason;
        this.failedAt = failedAt;
    }
}
