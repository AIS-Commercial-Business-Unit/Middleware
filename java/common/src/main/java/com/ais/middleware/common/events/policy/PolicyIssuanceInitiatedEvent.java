package com.ais.middleware.common.events.policy;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by PolicyIssuanceAndLifecycleManagement when an IssuePolicy command is received.
 * Platform.Compliance subscribes and runs economic sanctions screening.
 * EDA: PolicyLifecycle announces that issuance has begun — it does not command Compliance to check.
 */
public record PolicyIssuanceInitiatedEvent(
        String issuanceId,
        String accountId,
        int policyTypeCode,
        OffsetDateTime requestedAt
) {
    @JsonCreator
    public PolicyIssuanceInitiatedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountId") String accountId,
            @JsonProperty("policyTypeCode") int policyTypeCode,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.issuanceId = issuanceId;
        this.accountId = accountId;
        this.policyTypeCode = policyTypeCode;
        this.requestedAt = requestedAt;
    }
}
