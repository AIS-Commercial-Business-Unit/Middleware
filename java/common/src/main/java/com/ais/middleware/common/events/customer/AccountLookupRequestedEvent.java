package com.ais.middleware.common.events.customer;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by PolicyIssuanceAndLifecycleManagement after compliance clears.
 * CustomerIdentityAndRelationshipManagement subscribes and retrieves or creates the account service record.
 * EDA: PolicyLifecycle announces it needs the account record — it does not command CustomerIdentity.
 */
public record AccountLookupRequestedEvent(
        String issuanceId,
        String accountId,
        OffsetDateTime requestedAt
) {
    @JsonCreator
    public AccountLookupRequestedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountId") String accountId,
            @JsonProperty("requestedAt") OffsetDateTime requestedAt
    ) {
        this.issuanceId = issuanceId;
        this.accountId = accountId;
        this.requestedAt = requestedAt;
    }
}
