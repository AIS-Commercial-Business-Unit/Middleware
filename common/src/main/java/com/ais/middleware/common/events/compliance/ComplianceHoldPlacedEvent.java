package com.ais.middleware.common.events.compliance;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by Platform.Compliance when sanctions screening returns PendingReview.
 * A ComplianceClearanceSaga is started to wait for manual clearance, and the IssuanceSaga is suspended without timing out during that period.
 */
public record ComplianceHoldPlacedEvent(
        String checkId,
        String correlationId,
        String externalReferenceId,
        OffsetDateTime holdPlacedAt,
        OffsetDateTime maxWaitUntil
) {
    @JsonCreator
    public ComplianceHoldPlacedEvent(
            @JsonProperty("checkId") String checkId,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("externalReferenceId") String externalReferenceId,
            @JsonProperty("holdPlacedAt") OffsetDateTime holdPlacedAt,
            @JsonProperty("maxWaitUntil") OffsetDateTime maxWaitUntil
    ) {
        this.checkId = checkId;
        this.correlationId = correlationId;
        this.externalReferenceId = externalReferenceId;
        this.holdPlacedAt = holdPlacedAt;
        this.maxWaitUntil = maxWaitUntil;
    }
}
