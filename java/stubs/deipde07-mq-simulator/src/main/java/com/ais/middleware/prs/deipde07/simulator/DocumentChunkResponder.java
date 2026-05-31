package com.ais.middleware.prs.deipde07.simulator;

import jakarta.jms.TextMessage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.jms.core.JmsTemplate;
import org.springframework.stereotype.Component;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Base64;
import java.util.List;
import java.util.Map;

@Component
public class DocumentChunkResponder {

    private static final Logger log = LoggerFactory.getLogger(DocumentChunkResponder.class);

    private static final Map<String, String> FIXTURE_PDFS = Map.of(
            // Small PDF: ~640 bytes of base64 → ~10 chunks of 64 chars
            "12345678901", Base64.getEncoder().encodeToString(
                    "FAKE-PDF-CONTENT-FOR-DEMO-SMALL".repeat(20).getBytes(StandardCharsets.UTF_8)),
            // Large PDF: ~6000+ bytes of base64 → 100+ chunks
            "98765432109876", Base64.getEncoder().encodeToString(
                    "FAKE-LARGE-PDF-CONTENT-FOR-DEMO".repeat(200).getBytes(StandardCharsets.UTF_8))
    );

    /**
     * Respond to a document retrieval request by sending chunked JMS messages to the response queue.
     * Each chunk is 64 characters with CRLF appended (simulating EBCDIC artifact from mainframe).
     * The final chunk is suffixed with {@code ||END-OF-DOCUMENT||}.
     *
     * @param documentKey   document key extracted from request body
     * @param correlationId JMSCorrelationID from the incoming request
     * @param template      JmsTemplate for sending response messages
     * @param responseQueue destination queue name
     * @param chunkDelayMs  delay between consecutive chunks (simulates mainframe pacing)
     */
    public void respond(String documentKey, String correlationId, JmsTemplate template,
                        String responseQueue, long chunkDelayMs) throws Exception {

        log.info("[DOC-CHUNK-RESPONDER] Starting response correlationId={} documentKey={}", correlationId, documentKey);

        String base64Pdf = FIXTURE_PDFS.get(documentKey);
        if (base64Pdf == null) {
            log.warn("[DOC-CHUNK-RESPONDER] Document not found correlationId={} documentKey={}", correlationId, documentKey);
            template.send(responseQueue, session -> {
                TextMessage msg = session.createTextMessage("ERROR=DOCUMENT_NOT_FOUND");
                msg.setJMSCorrelationID(correlationId);
                return msg;
            });
            return;
        }

        List<String> chunks = splitInto64ByteChunks(base64Pdf);
        int total = chunks.size();

        log.info("[DOC-CHUNK-RESPONDER] Sending {} chunk(s) correlationId={} documentKey={}", total, correlationId, documentKey);

        for (int i = 0; i < total; i++) {
            Thread.sleep(chunkDelayMs);
            final boolean isLast = (i == total - 1);
            String chunkBody = chunks.get(i) + "\r\n";
            if (isLast) {
                chunkBody = chunkBody + "||END-OF-DOCUMENT||";
            }
            final String body = chunkBody;
            template.send(responseQueue, session -> {
                TextMessage msg = session.createTextMessage(body);
                msg.setJMSCorrelationID(correlationId);
                return msg;
            });
        }

        log.info("[DOC-CHUNK-RESPONDER] Response complete correlationId={} documentKey={} chunkCount={}", correlationId, documentKey, total);
    }

    List<String> splitInto64ByteChunks(String input) {
        List<String> chunks = new ArrayList<>();
        int len = input.length();
        for (int i = 0; i < len; i += 64) {
            chunks.add(input.substring(i, Math.min(i + 64, len)));
        }
        return chunks;
    }
}
