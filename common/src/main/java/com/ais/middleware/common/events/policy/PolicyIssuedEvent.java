package com.ais.middleware.common.events.policy;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Published by PolicyIssuanceAndLifecycleManagement when all saga branches complete after both join events are received.
 * This is the final success event of the IssuanceSaga (BR-PIL-003).
 */
public record PolicyIssuedEvent(
        String issuanceId,
        String accountServiceRequestNumber,
        List<String> policyNumbers,
        String targetPas,
        OffsetDateTime issuedAt
) {
    @JsonCreator
    public PolicyIssuedEvent(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountServiceRequestNumber") String accountServiceRequestNumber,
            @JsonProperty("policyNumbers") List<String> policyNumbers,
            @JsonProperty("targetPas") String targetPas,
            @JsonProperty("issuedAt") OffsetDateTime issuedAt
    ) {
        this.issuanceId = issuanceId;
        this.accountServiceRequestNumber = accountServiceRequestNumber;
        this.policyNumbers = policyNumbers;
        this.targetPas = targetPas;
        this.issuedAt = issuedAt;
    }
}
