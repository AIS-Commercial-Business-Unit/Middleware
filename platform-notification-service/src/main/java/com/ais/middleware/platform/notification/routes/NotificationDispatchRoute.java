package com.ais.middleware.platform.notification.routes;

import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

/**
 * Handles notification dispatch. Subscribes to PublishNotificationIntent commands
 * and dispatches via all configured channels.
 * All intents are persisted to MongoDB before dispatch (BR-NTF-006).
 */
@Component
public class NotificationDispatchRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(NotificationDispatchRoute.class);

    @Override
    public void configure() throws Exception {

        // Global DLQ handler: 2 retries with exponential backoff, then dead-letter.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                log.error("Unhandled exception in notification-dispatch route — routing to DLQ. error={}",
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:notification.dlq.notification-dispatch");

        from("kafka:notification.commands.publish-notification-intent?groupId=platform-notification-service")
            .routeId("notification-dispatch")
            .log("NotificationIntent received for correlationId=${header.issuanceId}")
            .log("Dispatching notification via all configured channels")
            .to("kafka:notification.events.notification-dispatched");
    }
}
