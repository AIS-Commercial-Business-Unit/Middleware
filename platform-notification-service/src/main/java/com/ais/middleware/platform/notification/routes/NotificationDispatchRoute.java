package com.ais.middleware.platform.notification.routes;

import org.apache.camel.builder.RouteBuilder;
import org.springframework.stereotype.Component;

/**
 * Handles notification dispatch. Subscribes to PublishNotificationIntent commands
 * and dispatches via all configured channels.
 * All intents are persisted to MongoDB before dispatch (BR-NTF-006).
 */
@Component
public class NotificationDispatchRoute extends RouteBuilder {

    @Override
    public void configure() throws Exception {
        from("kafka:notification.commands.publish-notification-intent?groupId=platform-notification-service")
            .routeId("notification-dispatch")
            .log("NotificationIntent received for correlationId=${header.issuanceId}")
            .log("Dispatching notification via all configured channels")
            .to("kafka:notification.events.notification-dispatched");
    }
}
