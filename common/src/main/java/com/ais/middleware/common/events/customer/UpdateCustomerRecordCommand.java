package com.ais.middleware.common.events.customer;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * Command sent by PolicyIssuanceAndLifecycleManagement in parallel with AssociateBillingAccountCommand after PAS confirmation.
 * Only changed fields are populated so null fields are not sent to CRM40X1, and CustomerUpdated is still required for the saga join (BR-CUS-003).
 */
public record UpdateCustomerRecordCommand(
        String correlationId,
        String externalAccountId,
        String dunsNumber,
        String billingAddress
) {
    @JsonCreator
    public UpdateCustomerRecordCommand(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("externalAccountId") String externalAccountId,
            @JsonProperty(value = "dunsNumber", required = false) String dunsNumber,
            @JsonProperty(value = "billingAddress", required = false) String billingAddress
    ) {
        this.correlationId = correlationId;
        this.externalAccountId = externalAccountId;
        this.dunsNumber = dunsNumber;
        this.billingAddress = billingAddress;
    }
}
