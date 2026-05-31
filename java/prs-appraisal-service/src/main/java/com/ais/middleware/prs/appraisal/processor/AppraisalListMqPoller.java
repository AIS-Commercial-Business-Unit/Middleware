package com.ais.middleware.prs.appraisal.processor;

import com.ais.middleware.prs.appraisal.domain.AppraisalListItem;
import com.ais.middleware.prs.appraisal.domain.AppraisalListItem.Source;
import org.apache.camel.ConsumerTemplate;
import org.apache.camel.Exchange;
import org.apache.camel.Processor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

/**
 * Polls the configured appraisal list reply queue for GetAppraisalList multi-message responses.
 *
 * Pattern: AsyncToSync Bridge + MultiMessageAggregation (BR-APR-002).
 * - Each MQ message body contains one appraisal record in KEY=VALUE format
 *   with a SEQUENCE=X OF N header line.
 * - Polls with 1-second timeout per attempt; 30-second overall deadline.
 * - Stops when SEQUENCE=N OF N received or deadline elapses.
 * - Sets exchange property "partialResult"=true on timeout.
 * - Sets exchange body to List<AppraisalListItem> from DEIPDE07.
 */
@Component
public class AppraisalListMqPoller implements Processor {

    private static final Logger log = LoggerFactory.getLogger(AppraisalListMqPoller.class);

    private static final long POLL_TIMEOUT_MS = 1_000L;
    private static final long MAX_TIMEOUT_MS = 30_000L;

    @Value("${appraisal.list.reply.queue:APPRAISAL.LIST.REPLY}")
    private String listReplyQueue;

    private final ConsumerTemplate consumerTemplate;

    public AppraisalListMqPoller(ConsumerTemplate consumerTemplate) {
        this.consumerTemplate = consumerTemplate;
    }

    @Override
    public void process(Exchange exchange) {
        String correlationId = exchange.getIn().getHeader("CorrelationId", String.class);
        String policyNumber = exchange.getIn().getHeader("policyNumber", String.class);

        log.info("AppraisalListMqPoller.start: policyNumber={} correlationId={}", policyNumber, correlationId);

        List<AppraisalListItem> items = new ArrayList<>();
        boolean complete = false;
        Instant deadline = Instant.now().plusMillis(MAX_TIMEOUT_MS);

        String selector = "JMSCorrelationID = '" + correlationId + "'";
        String endpointUri = "jms:queue:" + listReplyQueue + "?selector=" + java.net.URLEncoder.encode(selector,
            java.nio.charset.StandardCharsets.UTF_8);

        while (!complete && Instant.now().isBefore(deadline)) {
            Exchange pollResult = consumerTemplate.receive(endpointUri, POLL_TIMEOUT_MS);

            if (pollResult == null || pollResult.getIn().getBody() == null) {
                log.debug("AppraisalListMqPoller.poll: no message received within {}ms correlationId={}", POLL_TIMEOUT_MS, correlationId);
                continue;
            }

            String body = pollResult.getIn().getBody(String.class);
            log.debug("AppraisalListMqPoller.poll: received message correlationId={} bodyLength={}", correlationId, body.length());

            AppraisalListItem item = parseAppraisalListMessage(body, policyNumber);
            if (item != null) {
                items.add(item);
            }

            // Check sequence completion: SEQUENCE=X OF N — complete when X == N
            String sequenceLine = extractLine(body, "SEQUENCE");
            if (sequenceLine != null) {
                complete = isSequenceComplete(sequenceLine);
                if (complete) {
                    log.info("AppraisalListMqPoller.complete: policyNumber={} correlationId={} recordCount={} sequence={}",
                        policyNumber, correlationId, items.size(), sequenceLine);
                }
            }
        }

        boolean partialResult = !complete;
        if (partialResult) {
            log.warn("AppraisalListMqPoller.timeout: policyNumber={} correlationId={} recordCount={} partialResult=true",
                policyNumber, correlationId, items.size());
        }

        exchange.getIn().setBody(items);
        exchange.setProperty("mqPartialResult", partialResult);
        exchange.setProperty("deipde07Count", items.size());
    }

    private AppraisalListItem parseAppraisalListMessage(String body, String policyNumber) {
        try {
            return new AppraisalListItem(
                extractLine(body, "APPRAISAL_UID"),
                coalesce(extractLine(body, "POLICY_QUOTE_NBR"), policyNumber),
                extractLine(body, "STREET_ADR"),
                extractLine(body, "CITY_ADR"),
                extractLine(body, "STATE_CDE"),
                extractLine(body, "ZIP_ADR"),
                extractLine(body, "APPRAISAL_DTE"),
                extractLine(body, "DOCUMENTTYPE"),
                extractLine(body, "DOCUMENTNAME"),
                extractLine(body, "DOCUMENTKEY"),
                Source.DEIPDE07
            );
        } catch (Exception e) {
            log.warn("AppraisalListMqPoller.parse: failed to parse message body={} error={}", body, e.getMessage());
            return null;
        }
    }

    /**
     * Extract VALUE from a line formatted "KEY=VALUE" within a multi-line body.
     */
    private String extractLine(String body, String key) {
        if (body == null) return null;
        for (String line : body.split("\\r?\\n")) {
            if (line.startsWith(key + "=")) {
                return line.substring(key.length() + 1).trim();
            }
        }
        return null;
    }

    /**
     * Returns true when sequence is complete, e.g. "1 OF 1" or "3 OF 3".
     */
    private boolean isSequenceComplete(String sequenceValue) {
        // Format: "X OF N"
        String[] parts = sequenceValue.toUpperCase().split("\\s+OF\\s+");
        if (parts.length == 2) {
            try {
                int current = Integer.parseInt(parts[0].trim());
                int total = Integer.parseInt(parts[1].trim());
                return current == total && total > 0;
            } catch (NumberFormatException e) {
                log.warn("AppraisalListMqPoller.isSequenceComplete: could not parse sequence='{}'", sequenceValue);
            }
        }
        return false;
    }

    private String coalesce(String a, String b) {
        return (a != null && !a.isBlank()) ? a : b;
    }
}
