package com.ais.middleware.common.events.notification;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * Command sent by PolicyIssuanceAndLifecycleManagement after PolicyIssuedEvent.
 * Platform.Notification persists the intent before dispatch, and the default priority for policy issuance is Batched with a 25-minute aggregation window (BR-NTF-006).
 */
public record PublishNotificationIntentCommand(
        String intentId,
        String sourceDomain,
        String correlationId,
        String notificationType,
        String aggregationKey,
        String recipientType,
        String recipientAddress,
        String messageBody,
        Priority priority
) {
    public enum Priority {
        Immediate, Batched
    }

    @JsonCreator
    public PublishNotificationIntentCommand(
            @JsonProperty("intentId") String intentId,
            @JsonProperty("sourceDomain") String sourceDomain,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("notificationType") String notificationType,
            @JsonProperty("aggregationKey") String aggregationKey,
            @JsonProperty("recipientType") String recipientType,
            @JsonProperty("recipientAddress") String recipientAddress,
            @JsonProperty("messageBody") String messageBody,
            @JsonProperty("priority") Priority priority
    ) {
        this.intentId = intentId;
        this.sourceDomain = sourceDomain;
        this.correlationId = correlationId;
        this.notificationType = notificationType;
        this.aggregationKey = aggregationKey;
        this.recipientType = recipientType;
        this.recipientAddress = recipientAddress;
        this.messageBody = messageBody;
        this.priority = priority;
    }
}
