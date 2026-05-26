package com.ais.middleware.common.events.billing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by BillingAndFinanceManagement after creating the BillingAccount record and associating it with the external billing system.
 * This is one of the two events required for the IssuanceSaga join condition (BR-PIL-003).
 */
public record BillingAssociationCreatedEvent(
        String accountId,
        String issuanceId,
        String accountServiceRequestNumber,
        BillingChannel billingChannel,
        OffsetDateTime createdAt
) {
    public enum BillingChannel {
        DirectBill, AgencyBill, Digital
    }

    @JsonCreator
    public BillingAssociationCreatedEvent(
            @JsonProperty("accountId") String accountId,
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountServiceRequestNumber") String accountServiceRequestNumber,
            @JsonProperty("billingChannel") BillingChannel billingChannel,
            @JsonProperty("createdAt") OffsetDateTime createdAt
    ) {
        this.accountId = accountId;
        this.issuanceId = issuanceId;
        this.accountServiceRequestNumber = accountServiceRequestNumber;
        this.billingChannel = billingChannel;
        this.createdAt = createdAt;
    }
}
