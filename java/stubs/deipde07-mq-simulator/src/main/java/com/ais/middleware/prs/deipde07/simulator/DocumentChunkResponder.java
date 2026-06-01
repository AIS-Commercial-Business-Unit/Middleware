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

    private static final Map<String, String> FIXTURE_PDFS;

    static {
        Map<String, String> m = new java.util.HashMap<>();
        // Legacy fixture keys
        m.put("12345678901", Base64.getEncoder().encodeToString(
                "FAKE-PDF-CONTENT-FOR-DEMO-SMALL".repeat(20).getBytes(StandardCharsets.UTF_8)));
        m.put("98765432109876", Base64.getEncoder().encodeToString(
                "FAKE-LARGE-PDF-CONTENT-FOR-DEMO".repeat(200).getBytes(StandardCharsets.UTF_8)));
        // POL-001-TEST document keys (from AppraisalListResponder fixture)
        m.put("10000000001", Base64.getEncoder().encodeToString(
                buildMinimalPdf("10000000001", "Full Appraisal Report")));
        m.put("10000000002", Base64.getEncoder().encodeToString(
                buildMinimalPdf("10000000002", "Exterior Appraisal")));
        m.put("10000000003", Base64.getEncoder().encodeToString(
                buildMinimalPdf("10000000003", "Replacement Cost Report")));
        // POL-003-TEST document keys
        m.put("10000000004", Base64.getEncoder().encodeToString(
                buildMinimalPdf("10000000004", "Single Property Report")));
        FIXTURE_PDFS = java.util.Collections.unmodifiableMap(m);
    }

    private static byte[] buildMinimalPdf(String documentKey, String title) {
        StringBuilder sb = new StringBuilder();
        sb.append("%PDF-1.4\n");

        int obj1Offset = sb.length();
        sb.append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        int obj2Offset = sb.length();
        sb.append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        int obj3Offset = sb.length();
        sb.append("3 0 obj\n<</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources <</Font <</F1 5 0 R>>>>>>\nendobj\n");

        String streamContent = "BT\n/F1 14 Tf\n72 720 Td\n(" + title + ") Tj\n0 -30 Td\n(Document: " + documentKey + ") Tj\n0 -30 Td\n(Source: Mainframe / IBM MQ DEIPDE07) Tj\n0 -30 Td\n(Generated: UC4 Appraisal Documents Demo) Tj\nET";

        int obj4Offset = sb.length();
        sb.append("4 0 obj\n<< /Length ").append(streamContent.length()).append(" >>\nstream\n");
        sb.append(streamContent);
        sb.append("\nendstream\nendobj\n");

        int obj5Offset = sb.length();
        sb.append("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        int xrefOffset = sb.length();
        sb.append("xref\n");
        sb.append("0 6\n");
        sb.append("0000000000 65535 f \n");
        sb.append(String.format("%010d 00000 n \n", obj1Offset));
        sb.append(String.format("%010d 00000 n \n", obj2Offset));
        sb.append(String.format("%010d 00000 n \n", obj3Offset));
        sb.append(String.format("%010d 00000 n \n", obj4Offset));
        sb.append(String.format("%010d 00000 n \n", obj5Offset));
        sb.append("trailer\n<< /Size 6 /Root 1 0 R >>\n");
        sb.append("startxref\n");
        sb.append(xrefOffset);
        sb.append("\n%%EOF\n");

        return sb.toString().getBytes(StandardCharsets.UTF_8);
    }

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
