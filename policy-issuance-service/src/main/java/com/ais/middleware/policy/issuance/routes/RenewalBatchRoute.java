package com.ais.middleware.policy.issuance.routes;

import com.ais.middleware.common.events.compliance.RequestComplianceCheckCommand;
import com.ais.middleware.common.events.fileprocessing.RenewalRecordFailedEvent;
import com.ais.middleware.common.events.fileprocessing.RenewalRecordProcessedEvent;
import com.ais.middleware.common.events.fileprocessing.RenewalRecordReadyForIssuanceEvent;
import com.ais.middleware.common.events.policy.IssuanceFailedEvent;
import com.ais.middleware.common.events.policy.PolicyIssuedEvent;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRecord;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRepository;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;
import java.util.UUID;

/**
 * Camel routes that handle the AutomatedRenewal batch path.
 *
 * Route 1: RenewalRecordReadyForIssuance → creates IssuanceSaga (AutomatedRenewal channel) → compliance check
 * Route 2: PolicyIssued (AutomatedRenewal only) → publishes RenewalRecordProcessed
 * Route 3: IssuanceFailed (AutomatedRenewal only) → publishes RenewalRecordFailed
 *
 * Group IDs are distinct from IssuanceSagaRoute to allow parallel consumption of shared topics.
 */
@Component
public class RenewalBatchRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(RenewalBatchRoute.class);

    private final IssuanceSagaRepository repository;
    private final ObjectMapper objectMapper;

    public RenewalBatchRoute(IssuanceSagaRepository repository, ObjectMapper objectMapper) {
        this.repository = repository;
        this.objectMapper = objectMapper;
    }

    @Override
    public void configure() throws Exception {

        // Global DLQ handler: 2 retries with exponential backoff, then dead-letter.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                log.error("Unhandled exception in renewal-batch route — routing to DLQ. routeId={} error={}",
                        exchange.getFromRouteId(),
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:policy.dlq.renewal-batch");

        // ── Route 1: RenewalRecordReadyForIssuance → start IssuanceSaga (AutomatedRenewal) ──
        from("kafka:file.events.renewal-record-ready-for-issuance?groupId=policy-issuance-saga-renewal")
            .routeId("renewal-record-issuance")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                RenewalRecordReadyForIssuanceEvent event =
                        objectMapper.readValue(json, RenewalRecordReadyForIssuanceEvent.class);

                String issuanceId = event.correlationId();
                MDC.put("issuanceId", issuanceId);
                MDC.put("batchId", event.batchId());
                MDC.put("recordId", event.recordId());

                // Idempotency: skip if saga already exists
                if (repository.existsById(issuanceId)) {
                    log.warn("Duplicate RenewalRecord — saga already exists, skipping issuanceId={}", issuanceId);
                    exchange.setRouteStop(true);
                    MDC.clear();
                    return;
                }

                IssuanceSagaRecord saga = new IssuanceSagaRecord();
                saga.setIssuanceId(issuanceId);
                saga.setAccountId(event.accountId());
                saga.setSubmittingChannel("AutomatedRenewal");
                saga.setPolicyTypeCode(event.policyTypeCode());
                saga.setPolicyTypeSubCode(event.policyTypeSubCode());
                saga.setRequestedAt(OffsetDateTime.now());
                saga.setStatus(IssuanceSagaRecord.SagaStatus.Initiated);
                saga.setRecordId(event.recordId());
                saga.setBatchId(event.batchId());
                repository.save(saga);
                log.info("RenewalBatch IssuanceSaga created — status=Initiated batchId={} recordId={}",
                        event.batchId(), event.recordId());

                var checkCmd = new RequestComplianceCheckCommand(
                        UUID.randomUUID().toString(),
                        issuanceId, "Policy", "EconomicSanctions", "CommercialAccount",
                        event.accountId(), objectMapper.writeValueAsString(event)
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(checkCmd));
                exchange.getIn().setHeader("issuanceId", issuanceId);

                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingCompliance);
                repository.save(saga);
                log.info("RenewalBatch IssuanceSaga transitioned — status=AwaitingCompliance");
                MDC.clear();
            })
            .to("kafka:compliance.commands.request-compliance-check");

        // ── Route 2: PolicyIssued → publish RenewalRecordProcessed (AutomatedRenewal only) ──
        from("kafka:policy.events.policy-issued?groupId=policy-issuance-saga-renewal-outcome")
            .routeId("renewal-record-issued")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyIssuedEvent event = objectMapper.readValue(json, PolicyIssuedEvent.class);
                String issuanceId = event.issuanceId();

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null || !"AutomatedRenewal".equals(saga.getSubmittingChannel())) {
                    exchange.getIn().setBody(null);
                    return;
                }

                MDC.put("issuanceId", issuanceId);
                MDC.put("batchId", saga.getBatchId());
                log.info("AutomatedRenewal saga completed — publishing RenewalRecordProcessed batchId={} recordId={}",
                        saga.getBatchId(), saga.getRecordId());

                var processed = new RenewalRecordProcessedEvent(
                        saga.getRecordId(),
                        saga.getBatchId(),
                        issuanceId,
                        saga.getPolicyNumbers(),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(processed));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                MDC.clear();
            })
            .choice()
                .when(body().isNotNull())
                    .to("kafka:policy.events.renewal-record-processed")
            .end();

        // ── Route 3: IssuanceFailed → publish RenewalRecordFailed (AutomatedRenewal only) ──
        from("kafka:policy.events.issuance-failed?groupId=policy-issuance-saga-renewal-outcome-fail")
            .routeId("renewal-record-failed")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                IssuanceFailedEvent event = objectMapper.readValue(json, IssuanceFailedEvent.class);
                String issuanceId = event.issuanceId();

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null || !"AutomatedRenewal".equals(saga.getSubmittingChannel())) {
                    exchange.getIn().setBody(null);
                    return;
                }

                MDC.put("issuanceId", issuanceId);
                MDC.put("batchId", saga.getBatchId());
                log.warn("AutomatedRenewal saga failed — publishing RenewalRecordFailed batchId={} recordId={}",
                        saga.getBatchId(), saga.getRecordId());

                String failureCategory = determineFailureCategory(saga.getFailureReason());
                var failed = new RenewalRecordFailedEvent(
                        saga.getRecordId(),
                        saga.getBatchId(),
                        issuanceId,
                        saga.getFailureReason(),
                        failureCategory,
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(failed));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                MDC.clear();
            })
            .choice()
                .when(body().isNotNull())
                    .to("kafka:policy.events.renewal-record-failed")
            .end();
    }

    private String determineFailureCategory(String failureReason) {
        if (failureReason == null) return "Unknown";
        if (failureReason.contains("ComplianceBlocked")) return "ComplianceBlocked";
        if (failureReason.contains("PASCallFailed")) return "PASTimeout";
        if (failureReason.contains("Billing")) return "BillingFailure";
        return "Unknown";
    }
}
