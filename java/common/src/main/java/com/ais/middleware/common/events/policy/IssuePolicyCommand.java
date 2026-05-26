package com.ais.middleware.common.events.policy;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Command: Agent or automated process requests a policy be issued.
 * <p>
 * This is the entry point for the IssuanceSaga. The handler must return
 * an acknowledgement immediately — before any saga step begins (BR-PIL-001).
 * The IssuanceId is the correlation key for every message in this flow (BR-PIL-002).
 */
public record IssuePolicyCommand(
        String issuanceId,
        String accountId,
        List<PolicyItem> policies,
        SubmittingChannel submittingChannel,
        OffsetDateTime requestedAt
) {
    public enum SubmittingChannel {
        LegacyQueue, DirectRequest, AutomatedRenewal
    }

    public record PolicyItem(
            int policyTypeCode,
            int policyTypeSubCode,
            Object policyData
    ) {}

    @JsonCreator
    public IssuePolicyCommand(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountId") String accountId,
            @JsonProperty("policies") List<PolicyItem> policies,
            @JsonProperty("submittingChannel") SubmittingChannel submittingChannel,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.issuanceId = issuanceId;
        this.accountId = accountId;
        this.policies = policies;
        this.submittingChannel = submittingChannel;
        this.requestedAt = requestedAt;
    }
}
