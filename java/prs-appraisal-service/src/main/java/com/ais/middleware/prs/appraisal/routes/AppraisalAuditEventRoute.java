package com.ais.middleware.prs.appraisal.routes;

import org.apache.camel.builder.RouteBuilder;
import org.springframework.stereotype.Component;

/**
 * Publishes audit events after successful GetAppraisalList and GetAppraisalDocument calls.
 *
 * Topics:
 *   prs.events.appraisal-list-retrieved  — after GetAppraisalList
 *   prs.events.document-retrieved         — after GetAppraisalDocument
 */
@Component
public class AppraisalAuditEventRoute extends RouteBuilder {

    @Override
    public void configure() {

        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1_000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .log("AppraisalAuditEventRoute error: ${exception.message}");

        // ─── AppraisalListRetrieved audit event ───────────────────────────────
        from("direct:publishAppraisalListRetrievedEvent")
            .routeId("publish-appraisal-list-audit")
            .log("route:publish-appraisal-list-audit, policyNumber=${header.policyNumber}, correlationId=${header.CorrelationId}")
            .marshal().json()
            .to("kafka:prs.events.appraisal-list-retrieved")
            .log("route:publish-appraisal-list-audit, published, topic=prs.events.appraisal-list-retrieved, correlationId=${header.CorrelationId}");

        // ─── DocumentRetrieved audit event ────────────────────────────────────
        from("direct:publishDocumentRetrievedEvent")
            .routeId("publish-document-retrieved-audit")
            .log("route:publish-document-retrieved-audit, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}")
            .marshal().json()
            .to("kafka:prs.events.document-retrieved")
            .log("route:publish-document-retrieved-audit, published, topic=prs.events.document-retrieved, correlationId=${header.CorrelationId}");
    }
}
