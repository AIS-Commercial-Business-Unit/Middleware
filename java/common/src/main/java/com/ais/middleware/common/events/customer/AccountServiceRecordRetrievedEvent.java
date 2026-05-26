package com.ais.middleware.common.events.customer;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by CustomerIdentityAndRelationshipManagement after ERM7X1 lookup or creation.
 * The IssuanceSaga stores the accountServiceRequestNumber and then publishes IssuePolicyRequestedEvent.
 */
public record AccountServiceRecordRetrievedEvent(
        String correlationId,
        String externalAccountId,
        String accountServiceRequestNumber,
        OffsetDateTime retrievedAt
) {
    @JsonCreator
    public AccountServiceRecordRetrievedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("externalAccountId") String externalAccountId,
            @JsonProperty("accountServiceRequestNumber") String accountServiceRequestNumber,
            @JsonProperty("retrievedAt") OffsetDateTime retrievedAt
    ) {
        this.correlationId = correlationId;
        this.externalAccountId = externalAccountId;
        this.accountServiceRequestNumber = accountServiceRequestNumber;
        this.retrievedAt = retrievedAt;
    }
}
