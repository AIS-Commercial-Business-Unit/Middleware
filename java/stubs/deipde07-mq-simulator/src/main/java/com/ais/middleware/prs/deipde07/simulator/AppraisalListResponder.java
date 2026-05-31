package com.ais.middleware.prs.deipde07.simulator;

import jakarta.jms.TextMessage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.jms.core.JmsTemplate;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Map;

@Component
public class AppraisalListResponder {

    private static final Logger log = LoggerFactory.getLogger(AppraisalListResponder.class);

    public record AppraisalRecord(
            String appraisalUid,
            String policyQuoteNbr,
            String streetAdr,
            String cityAdr,
            String stateCde,
            String zipAdr,
            String appraisalDte,
            String documentType,
            String documentName,
            String documentKey
    ) {}

    private static final Map<String, List<AppraisalRecord>> FIXTURE_APPRAISALS = Map.of(
            "POL-001-TEST", List.of(
                    new AppraisalRecord("APR-001", "POL-001-TEST", "123 OAK LANE",    "MINNEAPOLIS", "MN", "55401", "2024-03-15", "APPRAISAL", "Full Appraisal Report",   "10000000001"),
                    new AppraisalRecord("APR-002", "POL-001-TEST", "456 ELM STREET",  "MINNEAPOLIS", "MN", "55402", "2024-03-16", "APPRAISAL", "Exterior Appraisal",      "10000000002"),
                    new AppraisalRecord("APR-003", "POL-001-TEST", "789 MAPLE DRIVE", "MINNEAPOLIS", "MN", "55403", "2024-03-17", "APPRAISAL", "Replacement Cost Report", "10000000003")
            ),
            "POL-002-TEST", List.of(), // zero results — consumer will time out on MQ side
            "POL-003-TEST", List.of(
                    new AppraisalRecord("APR-004", "POL-003-TEST", "321 PINE AVENUE", "MINNEAPOLIS", "MN", "55404", "2024-04-01", "APPRAISAL", "Single Property Report",  "10000000004")
            )
            // POL-TIMEOUT handled separately via special-case logic below
    );

    /**
     * Respond to an appraisal list request by sending JMS response messages to the response queue.
     * Each message is tagged with the original correlationId for selective consumption.
     *
     * @param policyNumber   policy number extracted from request body
     * @param correlationId  JMSCorrelationID from the incoming request
     * @param template       JmsTemplate for sending response messages
     * @param responseQueue  destination queue name
     * @param messageDelayMs delay between consecutive messages (simulates mainframe pacing)
     */
    public void respond(String policyNumber, String correlationId, JmsTemplate template,
                        String responseQueue, long messageDelayMs) throws Exception {

        log.info("[APPRAISAL-RESPONDER] Starting response correlationId={} policyNumber={}", correlationId, policyNumber);

        // Special case: simulate mainframe timeout — hold response longer than consumer timeout
        if ("POL-TIMEOUT".equals(policyNumber)) {
            log.info("[APPRAISAL-RESPONDER] POL-TIMEOUT scenario — sleeping 35s to trigger consumer timeout correlationId={}", correlationId);
            Thread.sleep(35_000);
            log.info("[APPRAISAL-RESPONDER] POL-TIMEOUT sleep complete — consumer should have timed out by now correlationId={}", correlationId);
            return;
        }

        List<AppraisalRecord> records = FIXTURE_APPRAISALS.getOrDefault(policyNumber, List.of());
        int total = records.size();

        log.info("[APPRAISAL-RESPONDER] Sending {} message(s) correlationId={} policyNumber={}", total, correlationId, policyNumber);

        for (int i = 0; i < total; i++) {
            Thread.sleep(messageDelayMs);
            final int sequence = i + 1;
            final String body = buildBody(records.get(i), sequence, total);
            template.send(responseQueue, session -> {
                TextMessage msg = session.createTextMessage(body);
                msg.setJMSCorrelationID(correlationId);
                return msg;
            });
            log.info("[APPRAISAL-RESPONDER] Sent message {} of {} correlationId={} appraisalUid={}",
                    sequence, total, correlationId, records.get(i).appraisalUid());
        }

        log.info("[APPRAISAL-RESPONDER] Response complete correlationId={} policyNumber={} messageCount={}", correlationId, policyNumber, total);
    }

    private String buildBody(AppraisalRecord r, int sequence, int total) {
        return "SEQUENCE=" + sequence + " OF " + total + "\r\n"
                + "APPRAISAL_UID=" + r.appraisalUid() + "\r\n"
                + "POLICY_QUOTE_NBR=" + r.policyQuoteNbr() + "\r\n"
                + "STREET_ADR=" + r.streetAdr() + "\r\n"
                + "CITY_ADR=" + r.cityAdr() + "\r\n"
                + "STATE_CDE=" + r.stateCde() + "\r\n"
                + "ZIP_ADR=" + r.zipAdr() + "\r\n"
                + "APPRAISAL_DTE=" + r.appraisalDte() + "\r\n"
                + "DOCUMENTTYPE=" + r.documentType() + "\r\n"
                + "DOCUMENTNAME=" + r.documentName() + "\r\n"
                + "DOCUMENTKEY=" + r.documentKey();
    }
}
