package com.ais.middleware.common.events.policy;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by PolicyIssuanceAndLifecycleManagement immediately when the IssuanceSaga is created.
 * Downstream systems should not react to this event because it exists only as an audit marker.
 */
public record IssuanceSagaStartedEvent(
        String issuanceId,
        String accountId,
        String submittingChannel,
        OffsetDateTime startedAt
) {
    @JsonCreator
    public IssuanceSagaStartedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountId") String accountId,
            @JsonProperty("submittingChannel") String submittingChannel,
            @JsonProperty("startedAt") OffsetDateTime startedAt
    ) {
        this.issuanceId = issuanceId;
        this.accountId = accountId;
        this.submittingChannel = submittingChannel;
        this.startedAt = startedAt;
    }
}
