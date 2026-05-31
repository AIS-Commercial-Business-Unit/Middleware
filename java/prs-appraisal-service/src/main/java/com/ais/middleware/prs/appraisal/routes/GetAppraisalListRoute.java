package com.ais.middleware.prs.appraisal.routes;

import com.ais.middleware.prs.appraisal.application.gateway.AtWorkGateway;
import com.ais.middleware.prs.appraisal.domain.AppraisalListRequest;
import com.ais.middleware.prs.appraisal.domain.AppraisalListResponse;
import com.ais.middleware.prs.appraisal.processor.AppraisalListAggregationStrategy;
import com.ais.middleware.prs.appraisal.processor.AppraisalListMqPoller;
import jakarta.jms.JMSException;
import org.apache.camel.builder.RouteBuilder;
import org.springframework.stereotype.Component;

import java.util.UUID;

/**
 * GetAppraisalList — Scatter-Gather pattern.
 *
 * Entry: direct:getAppraisalList (called from AppraisalController via ProducerTemplate).
 * Fan-out to @Work SQL and DEIPDE07 MQ simultaneously (BR-APR-001).
 * Aggregates and deduplicates results (BR-APR-003).
 * Sets partialResult=true when DEIPDE07 branch times out (BR-APR-002).
 * Publishes AppraisalListRetrieved audit event to Kafka.
 *
 * Headers expected on entry:
 *   policyNumber — from AppraisalListRequest body, set by controller
 */
@Component
public class GetAppraisalListRoute extends RouteBuilder {

    private final AtWorkGateway atWorkGateway;
    private final AppraisalListMqPoller appraisalListMqPoller;

    public GetAppraisalListRoute(AtWorkGateway atWorkGateway,
                                  AppraisalListMqPoller appraisalListMqPoller) {
        this.atWorkGateway = atWorkGateway;
        this.appraisalListMqPoller = appraisalListMqPoller;
    }

    @Override
    public void configure() {

        onException(JMSException.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1_000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .log("GetAppraisalList JMS error: ${exception.message}")
            .process(exchange -> {
                String policyNumber = exchange.getIn().getHeader("policyNumber", String.class);
                exchange.getIn().setBody(
                    new AppraisalListResponse(policyNumber, java.util.List.of(), true));
            });

        onException(Exception.class)
            .handled(true)
            .log("GetAppraisalList error: ${exception.message}")
            .process(exchange -> {
                String policyNumber = exchange.getIn().getHeader("policyNumber", String.class);
                exchange.getIn().setBody(
                    new AppraisalListResponse(policyNumber, java.util.List.of(), false));
            });

        // ─── Entry point ──────────────────────────────────────────────────────
        from("direct:getAppraisalList")
            .routeId("get-appraisal-list")
            .log("route:get-appraisal-list, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}")
            .process(exchange -> {
                // Extract policyNumber from body (AppraisalListRequest) and set as header
                Object body = exchange.getIn().getBody();
                if (body instanceof AppraisalListRequest req) {
                    exchange.getIn().setHeader("policyNumber", req.policyNumber());
                }
                exchange.getIn().setHeader("CorrelationId", UUID.randomUUID().toString());
            })
            .log("route:get-appraisal-list, start, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}")
            .multicast(new AppraisalListAggregationStrategy())
                .parallelProcessing()
                .timeout(30_000)
                .to("direct:callAtWorkSQL", "direct:callDEIPDE07MQList")
            .end()
            .to("direct:publishAppraisalListRetrievedEvent")
            .log("route:get-appraisal-list, complete, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}");

        // ─── @Work SQL branch ─────────────────────────────────────────────────
        from("direct:callAtWorkSQL")
            .routeId("call-atwork-sql")
            .log("route:call-atwork-sql, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}")
            .process(exchange -> {
                String policyNumber = exchange.getIn().getHeader("policyNumber", String.class);
                java.util.List<com.ais.middleware.prs.appraisal.domain.AppraisalListItem> items =
                    atWorkGateway.getAppraisalList(policyNumber);
                exchange.getIn().setBody(items);
            })
            .log("route:call-atwork-sql, returned=${body.size()} records, policyNumber=${header.policyNumber}");

        // ─── DEIPDE07 MQ branch ───────────────────────────────────────────────
        from("direct:callDEIPDE07MQList")
            .routeId("call-deipde07-mq-list")
            .log("route:call-deipde07-mq-list, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}")
            .process(exchange -> {
                // Set JMSCorrelationID for the outbound MQ request
                exchange.getIn().setHeader("JMSCorrelationID",
                    exchange.getIn().getHeader("CorrelationId", String.class));
                exchange.getIn().setBody("APPRAISAL_LIST|||" + exchange.getIn().getHeader("policyNumber", String.class) + "|||ACTIVE|||");
            })
            .to("jms:queue:{{appraisal.list.request.queue}}")
            .log("route:call-deipde07-mq-list, request sent, correlationId=${header.CorrelationId}")
            .process(appraisalListMqPoller)
            .log("route:call-deipde07-mq-list, aggregation complete, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}");
    }
}
