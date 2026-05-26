package com.ais.middleware.common.events.notification;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * Published by Platform.Notification after dispatching across all configured channels.
 * Failure in one channel does not block dispatch to the remaining channels.
 */
public record NotificationDispatchedEvent(
        String intentId,
        String correlationId,
        List<String> channelsDispatched,
        OffsetDateTime dispatchedAt
) {
    @JsonCreator
    public NotificationDispatchedEvent(
            @JsonProperty("intentId") String intentId,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("channelsDispatched") List<String> channelsDispatched,
            @JsonProperty("dispatchedAt") OffsetDateTime dispatchedAt
    ) {
        this.intentId = intentId;
        this.correlationId = correlationId;
        this.channelsDispatched = channelsDispatched;
        this.dispatchedAt = dispatchedAt;
    }
}
