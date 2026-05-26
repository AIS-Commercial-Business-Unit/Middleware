package com.ais.middleware.common.events.billing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.List;

/**
 * Command sent by PolicyIssuanceAndLifecycleManagement in parallel with UpdateCustomerRecordCommand after PAS confirmation.
 * BillingAssociationCreated must be published within the 2-hour IssuanceSaga timeout (BR-BIL-005).
 */
public record AssociateBillingAccountCommand(
        String issuanceId,
        String accountServiceRequestNumber,
        List<String> policyNumbers,
        BillingChannel billingChannel,
        int policyTypeCode
) {
    public enum BillingChannel {
        DirectBill, AgencyBill, Digital
    }

    @JsonCreator
    public AssociateBillingAccountCommand(
            @JsonProperty("issuanceId") String issuanceId,
            @JsonProperty("accountServiceRequestNumber") String accountServiceRequestNumber,
            @JsonProperty("policyNumbers") List<String> policyNumbers,
            @JsonProperty("billingChannel") BillingChannel billingChannel,
            @JsonProperty("policyTypeCode") int policyTypeCode
    ) {
        this.issuanceId = issuanceId;
        this.accountServiceRequestNumber = accountServiceRequestNumber;
        this.policyNumbers = policyNumbers;
        this.billingChannel = billingChannel;
        this.policyTypeCode = policyTypeCode;
    }
}
