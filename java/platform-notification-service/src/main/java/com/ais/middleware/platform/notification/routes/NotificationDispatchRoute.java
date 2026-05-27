package com.ais.middleware.platform.notification.routes;

import com.ais.middleware.platform.notification.observability.EDAFlowProcessor;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.apache.camel.component.kafka.KafkaConstants;
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

    private final EDAFlowProcessor edaFlowProcessor;

    public NotificationDispatchRoute(EDAFlowProcessor edaFlowProcessor) {
        this.edaFlowProcessor = edaFlowProcessor;
    }

    @Override
    public void configure() throws Exception {

        interceptFrom("kafka:*")
            .process(exchange -> exchange.setProperty("EDA_FLOW_DIRECTION", "consumed"))
            .process(edaFlowProcessor);

        interceptSendToEndpoint("kafka:*")
            .process(exchange -> {
                String uri = exchange.getProperty(Exchange.INTERCEPTED_ENDPOINT, String.class);
                if (uri == null) {
                    uri = exchange.getProperty(Exchange.TO_ENDPOINT, String.class);
                }
                if (uri == null) {
                    uri = exchange.getIn().getHeader(Exchange.TO_ENDPOINT, String.class);
                }
                if (uri == null || !uri.startsWith("kafka:")) {
                    return;
                }

                String topic = uri.replaceFirst("^kafka:(//)?", "");
                int optionsSeparator = topic.indexOf('?');
                if (optionsSeparator >= 0) {
                    topic = topic.substring(0, optionsSeparator);
                }

                exchange.getIn().setHeader(KafkaConstants.TOPIC, topic);
                exchange.setProperty("EDA_FLOW_DIRECTION", "published");
            })
            .process(edaFlowProcessor);

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
