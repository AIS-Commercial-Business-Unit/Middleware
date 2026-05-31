package com.ais.middleware.prs.appraisal.processor;

import org.apache.camel.ConsumerTemplate;
import org.apache.camel.Exchange;
import org.apache.camel.Processor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.util.concurrent.TimeoutException;

/**
 * Polls the configured appraisal document reply queue for GetAppraisalDocument chunked PDF messages.
 *
 * Pattern: MultiMessageAggregation (BR-APR-006 / BR-APR-007).
 * - Mainframe sends PDF as N ordered JMS messages, each containing 64 bytes of
 *   base64-encoded content with CRLF artifacts appended (EBCDIC encoding).
 * - Last message contains "||END-OF-DOCUMENT||" sentinel after the final chunk.
 * - Processor strips \r\n from each chunk, concatenates in order.
 * - 30-second overall timeout; throws TimeoutException on expiry (BR-APR-007).
 * - Sets exchange body to the complete base64 PDF string on success.
 */
@Component
public class PdfChunkMqPoller implements Processor {

    private static final Logger log = LoggerFactory.getLogger(PdfChunkMqPoller.class);

    private static final long POLL_TIMEOUT_MS = 1_000L;
    private static final long MAX_TIMEOUT_MS = 30_000L;
    private static final String END_OF_DOCUMENT_SENTINEL = "||END-OF-DOCUMENT||";

    @Value("${appraisal.document.reply.queue:APPRAISAL.DOCUMENT.REPLY}")
    private String documentReplyQueue;

    private final ConsumerTemplate consumerTemplate;

    public PdfChunkMqPoller(ConsumerTemplate consumerTemplate) {
        this.consumerTemplate = consumerTemplate;
    }

    @Override
    public void process(Exchange exchange) throws TimeoutException {
        String correlationId = exchange.getIn().getHeader("CorrelationId", String.class);
        String documentKey = exchange.getIn().getHeader("documentKey", String.class);

        log.info("PdfChunkMqPoller.start: documentKey={} correlationId={}", documentKey, correlationId);

        StringBuilder pdfChunks = new StringBuilder();
        boolean complete = false;
        int chunkCount = 0;
        Instant deadline = Instant.now().plusMillis(MAX_TIMEOUT_MS);

        String selector = "JMSCorrelationID = '" + correlationId + "'";
        String endpointUri = "jms:queue:" + documentReplyQueue + "?selector=" + java.net.URLEncoder.encode(selector,
            java.nio.charset.StandardCharsets.UTF_8);

        while (!complete && Instant.now().isBefore(deadline)) {
            Exchange pollResult = consumerTemplate.receive(endpointUri, POLL_TIMEOUT_MS);

            if (pollResult == null || pollResult.getIn().getBody() == null) {
                log.debug("PdfChunkMqPoller.poll: no chunk received within {}ms documentKey={} correlationId={}",
                    POLL_TIMEOUT_MS, documentKey, correlationId);
                continue;
            }

            String chunkBody = pollResult.getIn().getBody(String.class);
            chunkCount++;

            // Check for end-of-document sentinel BEFORE stripping
            if (chunkBody.contains(END_OF_DOCUMENT_SENTINEL)) {
                // Strip sentinel, then strip CRLF artifacts
                chunkBody = chunkBody.replace(END_OF_DOCUMENT_SENTINEL, "");
                complete = true;
            }

            // Strip EBCDIC CRLF artifacts (BR-APR-006)
            String cleaned = chunkBody.replace("\r", "").replace("\n", "");
            pdfChunks.append(cleaned);

            log.debug("PdfChunkMqPoller.chunk: chunkNumber={} cleanedLength={} documentKey={} complete={}",
                chunkCount, cleaned.length(), documentKey, complete);
        }

        if (!complete) {
            log.error("PdfChunkMqPoller.timeout: documentKey={} correlationId={} chunksReceived={} exceeded {}ms",
                documentKey, correlationId, chunkCount, MAX_TIMEOUT_MS);
            throw new TimeoutException(
                "DEIPDE07 MQ document retrieval timed out after " + MAX_TIMEOUT_MS + "ms (BR-APR-007) documentKey=" + documentKey);
        }

        String base64Pdf = pdfChunks.toString();
        log.info("PdfChunkMqPoller.complete: documentKey={} correlationId={} chunksReceived={} base64Length={}",
            documentKey, correlationId, chunkCount, base64Pdf.length());

        exchange.getIn().setBody(base64Pdf);
    }
}
