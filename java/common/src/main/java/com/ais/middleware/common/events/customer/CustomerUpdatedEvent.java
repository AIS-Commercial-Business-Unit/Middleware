package com.ais.middleware.common.events.customer;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Published by CustomerIdentityAndRelationshipManagement after updating CRM40X1.
 * This is one of the two events required to satisfy the IssuanceSaga join condition, alongside BillingAssociationCreatedEvent.
 */
public record CustomerUpdatedEvent(
        String correlationId,
        String externalAccountId,
        List<String> fieldsUpdated,
        OffsetDateTime updatedAt
) {
    @JsonCreator
    public CustomerUpdatedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("externalAccountId") String externalAccountId,
            @JsonProperty("fieldsUpdated") List<String> fieldsUpdated,
            @JsonProperty("updatedAt") OffsetDateTime updatedAt
    ) {
        this.correlationId = correlationId;
        this.externalAccountId = externalAccountId;
        this.fieldsUpdated = fieldsUpdated;
        this.updatedAt = updatedAt;
    }
}
