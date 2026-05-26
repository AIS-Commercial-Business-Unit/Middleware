package com.ais.middleware.common.events.customer;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * Command sent by PolicyIssuanceAndLifecycleManagement after compliance clears.
 * It instructs CustomerIdentityAndRelationshipManagement to look up or create the AccountServiceRequestNumber from ERM7X1, which is assigned exactly once per commercial account (BR-CUS-001).
 */
public record GetOrCreateAccountServiceRecordCommand(
        String correlationId,
        String externalAccountId,
        String accountName
) {
    @JsonCreator
    public GetOrCreateAccountServiceRecordCommand(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("externalAccountId") String externalAccountId,
            @JsonProperty("accountName") String accountName
    ) {
        this.correlationId = correlationId;
        this.externalAccountId = externalAccountId;
        this.accountName = accountName;
    }
}
