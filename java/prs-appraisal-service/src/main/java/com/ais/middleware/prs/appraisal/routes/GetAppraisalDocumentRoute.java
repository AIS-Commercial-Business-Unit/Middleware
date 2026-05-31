package com.ais.middleware.prs.appraisal.routes;

import com.ais.middleware.prs.appraisal.application.gateway.RiskIdWcfGateway;
import com.ais.middleware.prs.appraisal.domain.AppraisalDocumentRequest;
import com.ais.middleware.prs.appraisal.domain.AppraisalDocumentResponse;
import com.ais.middleware.prs.appraisal.processor.PdfChunkMqPoller;
import jakarta.jms.JMSException;
import org.apache.camel.builder.RouteBuilder;
import org.springframework.stereotype.Component;

import java.util.UUID;

/**
 * GetAppraisalDocument — Content-Based Router.
 *
 * Entry: direct:getAppraisalDocument (called from AppraisalController via ProducerTemplate).
 * Routes by DocumentKey format (BR-APR-004):
 *   _RiskID_I  → RiskID WCF insured path
 *   _RiskID_A  → RiskID WCF agent path
 *   numeric 10-15 digits → DEIPDE07 MQ chunked PDF path (BR-APR-006)
 *   otherwise  → IllegalArgumentException
 *
 * Headers expected on entry:
 *   documentKey — from AppraisalDocumentRequest body, set by controller
 */
@Component
public class GetAppraisalDocumentRoute extends RouteBuilder {

    private final RiskIdWcfGateway riskIdWcfGateway;
    private final PdfChunkMqPoller pdfChunkMqPoller;

    public GetAppraisalDocumentRoute(RiskIdWcfGateway riskIdWcfGateway,
                                      PdfChunkMqPoller pdfChunkMqPoller) {
        this.riskIdWcfGateway = riskIdWcfGateway;
        this.pdfChunkMqPoller = pdfChunkMqPoller;
    }

    @Override
    public void configure() {

        onException(JMSException.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1_000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .log("GetAppraisalDocument JMS error: ${exception.message}");

        onException(java.util.concurrent.TimeoutException.class)
            .handled(false)
            .log("GetAppraisalDocument MQ timeout (BR-APR-007): documentKey=${header.documentKey} error=${exception.message}");

        onException(Exception.class)
            .handled(false)
            .log("GetAppraisalDocument error: documentKey=${header.documentKey} error=${exception.message}");

        // ─── Entry point ──────────────────────────────────────────────────────
        from("direct:getAppraisalDocument")
            .routeId("get-appraisal-document")
            .process(exchange -> {
                Object body = exchange.getIn().getBody();
                if (body instanceof AppraisalDocumentRequest req) {
                    exchange.getIn().setHeader("documentKey", req.documentKey());
                }
                exchange.getIn().setHeader("CorrelationId", UUID.randomUUID().toString());
            })
            .log("route:get-appraisal-document, start, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}")
            .choice()
                .when(header("documentKey").regex(".*_RiskID_I.*"))
                    .to("direct:callRiskIdWcfInsured")
                .when(header("documentKey").regex(".*_RiskID_A.*"))
                    .to("direct:callRiskIdWcfAgent")
                .when(header("documentKey").regex("^[0-9]{10,15}$"))
                    .to("direct:callDEIPDE07MQDocument")
                .otherwise()
                    .process(exchange -> {
                        String key = exchange.getIn().getHeader("documentKey", String.class);
                        throw new IllegalArgumentException("Unknown DocumentKey format: " + key);
                    })
            .end()
            .log("route:get-appraisal-document, complete, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}");

        // ─── RiskID WCF insured path ──────────────────────────────────────────
        from("direct:callRiskIdWcfInsured")
            .routeId("call-riskid-wcf-insured")
            .log("route:call-riskid-wcf-insured, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}")
            .process(exchange -> {
                String documentKey = exchange.getIn().getHeader("documentKey", String.class);
                String base64Pdf = riskIdWcfGateway.getInspectionPdfInsured(documentKey);
                exchange.getIn().setBody(
                    new AppraisalDocumentResponse(documentKey, base64Pdf, "application/pdf"));
            })
            .log("route:call-riskid-wcf-insured, complete, documentKey=${header.documentKey}");

        // ─── RiskID WCF agent path ────────────────────────────────────────────
        from("direct:callRiskIdWcfAgent")
            .routeId("call-riskid-wcf-agent")
            .log("route:call-riskid-wcf-agent, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}")
            .process(exchange -> {
                String documentKey = exchange.getIn().getHeader("documentKey", String.class);
                String base64Pdf = riskIdWcfGateway.getInspectionPdfAgent(documentKey);
                exchange.getIn().setBody(
                    new AppraisalDocumentResponse(documentKey, base64Pdf, "application/pdf"));
            })
            .log("route:call-riskid-wcf-agent, complete, documentKey=${header.documentKey}");

        // ─── DEIPDE07 chunked PDF path ────────────────────────────────────────
        from("direct:callDEIPDE07MQDocument")
            .routeId("call-deipde07-mq-document")
            .log("route:call-deipde07-mq-document, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}")
            .process(exchange -> {
                exchange.getIn().setHeader("JMSCorrelationID",
                    exchange.getIn().getHeader("CorrelationId", String.class));
                exchange.getIn().setBody("APPRAISAL_DOC|||" + exchange.getIn().getHeader("documentKey", String.class) + "|||");
            })
            .to("jms:queue:{{appraisal.document.request.queue}}")
            .log("route:call-deipde07-mq-document, request sent, correlationId=${header.CorrelationId}")
            .process(pdfChunkMqPoller)
            .process(exchange -> {
                String documentKey = exchange.getIn().getHeader("documentKey", String.class);
                String base64Pdf = exchange.getIn().getBody(String.class);
                exchange.getIn().setBody(
                    new AppraisalDocumentResponse(documentKey, base64Pdf, "application/pdf"));
            })
            .log("route:call-deipde07-mq-document, complete, documentKey=${header.documentKey}, correlationId=${header.CorrelationId}");
    }
}
