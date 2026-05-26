package com.ais.middleware.policy.issuance.api;

import com.ais.middleware.common.events.policy.IssuePolicyCommand;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRecord;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRepository;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.ProducerTemplate;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;

/**
 * REST entry point for the IssuanceSaga.
 * POST /api/v1/policies/issue — accepts the IssuePolicy command and returns a correlation ID immediately.
 * GET  /api/v1/policies/issue/{issuanceId} — returns current saga state (for Saga Explorer).
 */
@RestController
@RequestMapping("/api/v1/policies")
public class PolicyIssuanceController {

    private static final Logger log = LoggerFactory.getLogger(PolicyIssuanceController.class);

    private final ProducerTemplate producerTemplate;
    private final IssuanceSagaRepository repository;
    private final ObjectMapper objectMapper;

    public PolicyIssuanceController(ProducerTemplate producerTemplate,
                                    IssuanceSagaRepository repository,
                                    ObjectMapper objectMapper) {
        this.producerTemplate = producerTemplate;
        this.repository = repository;
        this.objectMapper = objectMapper;
    }

    /**
     * Accepts an IssuePolicy command. Returns immediately with a correlation ID.
     * The actual workflow happens asynchronously via Kafka (BR-PIL-001, BR-2.6).
     */
    @PostMapping("/issue")
    public ResponseEntity<Map<String, String>> issuePolicy(@RequestBody IssuePolicyCommand command) throws Exception {
        String issuanceId = command.issuanceId() != null ? command.issuanceId() : UUID.randomUUID().toString();
        MDC.put("issuanceId", issuanceId);
        log.info("IssuePolicy command received — starting IssuanceSaga");

        // Reconstruct the command with issuanceId and defaults filled in, then serialize.
        // The Camel route reads issuanceId from the JSON body — it must be present.
        IssuePolicyCommand enriched = new IssuePolicyCommand(
                issuanceId,
                command.accountId(),
                command.policies(),
                command.submittingChannel() != null ? command.submittingChannel() : IssuePolicyCommand.SubmittingChannel.DirectRequest,
                command.requestedAt() != null ? command.requestedAt() : java.time.OffsetDateTime.now()
        );

        // Publish command to Kafka; the Camel route handles the saga
        String payload = objectMapper.writeValueAsString(enriched);
        producerTemplate.sendBodyAndHeader(
                "kafka:policy.commands.issue-policy",
                payload,
                "issuanceId", issuanceId
        );

        MDC.clear();
        return ResponseEntity.accepted().body(Map.of(
                "issuanceId", issuanceId,
                "status", "Initiated",
                "message", "Policy issuance workflow started. Use issuanceId to track progress."
        ));
    }

    /**
     * Returns the current saga state. Used by Platform.UI Saga Explorer.
     */
    @GetMapping("/issue/{issuanceId}")
    public ResponseEntity<?> getSagaState(@PathVariable("issuanceId") String issuanceId) {
        Optional<IssuanceSagaRecord> record = repository.findById(issuanceId);
        return record
                .<ResponseEntity<?>>map(ResponseEntity::ok)
                .orElseGet(() -> ResponseEntity.notFound().build());
    }
}
