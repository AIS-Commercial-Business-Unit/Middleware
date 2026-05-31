package com.ais.middleware.prs.appraisal.api;

import com.ais.middleware.prs.appraisal.domain.AppraisalDocumentRequest;
import com.ais.middleware.prs.appraisal.domain.AppraisalDocumentResponse;
import com.ais.middleware.prs.appraisal.domain.AppraisalListRequest;
import com.ais.middleware.prs.appraisal.domain.AppraisalListResponse;
import org.apache.camel.ProducerTemplate;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.UUID;

/**
 * REST controller for the Appraisal domain — UC4 GetAppraisalList and GetAppraisalDocument.
 *
 * POST /api/appraisals/list     — scatter-gather fan-out to @Work SQL + DEIPDE07 MQ
 * POST /api/appraisals/document — content-based router to RiskID WCF or DEIPDE07 MQ
 *
 * Both endpoints delegate to Camel routes via ProducerTemplate.
 * Structured logging: entry + exit with policyNumber/documentKey and duration.
 */
@RestController
@RequestMapping("/api/appraisals")
public class AppraisalController {

    private static final Logger log = LoggerFactory.getLogger(AppraisalController.class);

    private final ProducerTemplate producerTemplate;

    public AppraisalController(ProducerTemplate producerTemplate) {
        this.producerTemplate = producerTemplate;
    }

    /**
     * GetAppraisalList — scatter-gather.
     *
     * POST /api/appraisals/list
     * { "policyNumber": "POL-001-TEST" }
     */
    @PostMapping("/list")
    public ResponseEntity<AppraisalListResponse> getAppraisalList(
            @RequestBody AppraisalListRequest request) {

        String requestId = UUID.randomUUID().toString();
        MDC.put("correlationId", requestId);
        long startMs = System.currentTimeMillis();

        log.info("AppraisalController.getAppraisalList.entry: policyNumber={} requestId={}",
            request.policyNumber(), requestId);

        try {
            AppraisalListResponse response = producerTemplate.requestBody(
                "direct:getAppraisalList", request, AppraisalListResponse.class);

            long durationMs = System.currentTimeMillis() - startMs;
            log.info("AppraisalController.getAppraisalList.exit: policyNumber={} requestId={} " +
                "itemCount={} partialResult={} durationMs={}",
                request.policyNumber(), requestId,
                response != null ? response.items().size() : 0,
                response != null && response.partialResult(),
                durationMs);

            return ResponseEntity.ok(response);
        } catch (Exception e) {
            long durationMs = System.currentTimeMillis() - startMs;
            log.error("AppraisalController.getAppraisalList.error: policyNumber={} requestId={} durationMs={} error={}",
                request.policyNumber(), requestId, durationMs, e.getMessage(), e);
            throw e;
        } finally {
            MDC.clear();
        }
    }

    /**
     * GetAppraisalDocument — content-based router.
     *
     * POST /api/appraisals/document
     * { "documentKey": "12345678901" }
     */
    @PostMapping("/document")
    public ResponseEntity<AppraisalDocumentResponse> getAppraisalDocument(
            @RequestBody AppraisalDocumentRequest request) {

        String requestId = UUID.randomUUID().toString();
        MDC.put("correlationId", requestId);
        long startMs = System.currentTimeMillis();

        log.info("AppraisalController.getAppraisalDocument.entry: documentKey={} requestId={}",
            request.documentKey(), requestId);

        try {
            AppraisalDocumentResponse response = producerTemplate.requestBody(
                "direct:getAppraisalDocument", request, AppraisalDocumentResponse.class);

            long durationMs = System.currentTimeMillis() - startMs;
            log.info("AppraisalController.getAppraisalDocument.exit: documentKey={} requestId={} " +
                "pdfLength={} durationMs={}",
                request.documentKey(), requestId,
                response != null && response.base64Pdf() != null ? response.base64Pdf().length() : 0,
                durationMs);

            return ResponseEntity.ok(response);
        } catch (Exception e) {
            long durationMs = System.currentTimeMillis() - startMs;
            log.error("AppraisalController.getAppraisalDocument.error: documentKey={} requestId={} durationMs={} error={}",
                request.documentKey(), requestId, durationMs, e.getMessage(), e);
            throw e;
        } finally {
            MDC.clear();
        }
    }
}
