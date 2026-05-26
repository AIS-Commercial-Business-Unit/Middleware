package com.ais.middleware.common.events.policy;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Published by PolicyIssuanceAndLifecycleManagement after compliance is cleared and the account record is retrieved.
 * Platform.Integration subscribes and routes to the correct PAS via subscription filter on policyTypeCode (BR-PIL-005).
 */
public record IssuePolicyRequestedEvent(
        String issuanceId,
        String accountId,
        String accountServiceRequestNumber,
        List<PolicyItem> policies,
        OffsetDateTime requestedAt
) {
    public record PolicyItem(
            int policyTypeCode,
            int policyTypeSubCode,
            Object policyData
    ) {
        @JsonCreator
        public PolicyItem(
                @JsonProperty("policyTypeCode") int policyTypeCode,
                @JsonProperty("policyTypeSubCode") int policyTypeSubCode,
                @JsonProperty("policyData") Object policyData
        ) {
            this.policyTypeCode = policyTypeCode;
            this.policyTypeSubCode = policyTypeSubCode;
            this.policyData = policyData;
        }
    }

    @JsonCreator
    public IssuePolicyRequestedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountId") String accountId,
            @JsonProperty("accountServiceRequestNumber") String accountServiceRequestNumber,
            @JsonProperty("policies") List<PolicyItem> policies,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.issuanceId = issuanceId;
        this.accountId = accountId;
        this.accountServiceRequestNumber = accountServiceRequestNumber;
        this.policies = policies;
        this.requestedAt = requestedAt;
    }
}
