package com.ais.middleware.common.events.integration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Published once by Platform.Integration after a successful PAS call.
 * It is a pub/sub fan-out event consumed simultaneously by PolicyIssuanceAndLifecycleManagement, BillingAndFinanceManagement, and CustomerIdentityAndRelationshipManagement, and no domain re-publishes or forwards it under the Single Publisher Rule.
 */
public record PolicyAdminSystemResponseReceivedEvent(
        String issuanceId,
        String targetPas,
        String accountServiceRequestNumber,
        List<String> policyNumbers,
        OffsetDateTime receivedAt
) {
    @JsonCreator
    public PolicyAdminSystemResponseReceivedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("targetPas") String targetPas,
            @JsonProperty("accountServiceRequestNumber") String accountServiceRequestNumber,
            @JsonProperty("policyNumbers") List<String> policyNumbers,
            @JsonProperty("receivedAt") OffsetDateTime receivedAt
    ) {
        this.issuanceId = issuanceId;
        this.targetPas = targetPas;
        this.accountServiceRequestNumber = accountServiceRequestNumber;
        this.policyNumbers = policyNumbers;
        this.receivedAt = receivedAt;
    }
}
