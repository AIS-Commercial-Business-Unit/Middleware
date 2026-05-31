package com.ais.middleware.prs.deipde07.simulator;

import com.ais.middleware.prs.deipde07.util.MessageParser;
import jakarta.jms.Message;
import jakarta.jms.Session;
import jakarta.jms.TextMessage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.jms.annotation.JmsListener;
import org.springframework.jms.core.JmsTemplate;
import org.springframework.stereotype.Component;

@Component
public class MqRequestDispatcher {

    private static final Logger log = LoggerFactory.getLogger(MqRequestDispatcher.class);

    @Autowired private AppraisalListResponder appraisalListResponder;
    @Autowired private DocumentChunkResponder documentChunkResponder;
    @Autowired private MessageParser messageParser;
    @Autowired private JmsTemplate jmsTemplate;

    @Value("${mq.list.response.queue}") private String listResponseQueue;
    @Value("${mq.document.response.queue}") private String documentResponseQueue;
    @Value("${sim.message.delay.ms:150}") private long messageDelayMs;
    @Value("${sim.chunk.delay.ms:50}") private long chunkDelayMs;

    @JmsListener(destination = "${mq.list.request.queue}")
    public void handleListRequest(Message message, Session session) throws Exception {
        String correlationId = message.getJMSCorrelationID();
        String body = ((TextMessage) message).getText();
        log.info("[MQ-DISPATCHER] List request received correlationId={}", correlationId);

        try {
            String policyNumber = messageParser.parsePolicyNumber(body);
            log.info("[MQ-DISPATCHER] AppraisalList request correlationId={} policyNumber={}", correlationId, policyNumber);
            appraisalListResponder.respond(policyNumber, correlationId, jmsTemplate, listResponseQueue, messageDelayMs);
        } catch (Exception ex) {
            log.error("[MQ-DISPATCHER] Error processing list request correlationId={} error={}", correlationId, ex.getMessage(), ex);
            // Do not rethrow — listener must stay alive
        }
    }

    @JmsListener(destination = "${mq.document.request.queue}")
    public void handleDocumentRequest(Message message, Session session) throws Exception {
        String correlationId = message.getJMSCorrelationID();
        String body = ((TextMessage) message).getText();
        log.info("[MQ-DISPATCHER] Document request received correlationId={}", correlationId);

        try {
            String documentKey = messageParser.parseDocumentKey(body);
            log.info("[MQ-DISPATCHER] Document request correlationId={} documentKey={}", correlationId, documentKey);
            documentChunkResponder.respond(documentKey, correlationId, jmsTemplate, documentResponseQueue, chunkDelayMs);
        } catch (Exception ex) {
            log.error("[MQ-DISPATCHER] Error processing document request correlationId={} error={}", correlationId, ex.getMessage(), ex);
            // Do not rethrow — listener must stay alive
        }
    }
}
