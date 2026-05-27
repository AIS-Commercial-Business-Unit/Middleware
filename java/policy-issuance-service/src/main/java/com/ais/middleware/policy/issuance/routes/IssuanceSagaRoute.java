package com.ais.middleware.policy.issuance.routes;

import com.ais.middleware.common.events.billing.BillingAssociationCreatedEvent;
import com.ais.middleware.common.events.compliance.ComplianceBlockedEvent;
import com.ais.middleware.common.events.compliance.ComplianceClearedEvent;
import com.ais.middleware.common.events.customer.AccountLookupRequestedEvent;
import com.ais.middleware.common.events.customer.AccountServiceRecordRetrievedEvent;
import com.ais.middleware.common.events.customer.CustomerUpdatedEvent;
import com.ais.middleware.common.events.integration.PolicyAdminSystemCallFailedEvent;
import com.ais.middleware.common.events.integration.PolicyAdminSystemResponseReceivedEvent;
import com.ais.middleware.common.events.policy.IssuePolicyCommand;
import com.ais.middleware.common.events.policy.IssuePolicyRequestedEvent;
import com.ais.middleware.common.events.policy.IssuanceFailedEvent;
import com.ais.middleware.common.events.policy.PolicyIssuedEvent;
import com.ais.middleware.common.events.policy.PolicyIssuanceInitiatedEvent;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRecord;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRepository;
import com.ais.middleware.policy.issuance.persistence.IssuanceSagaDocument;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.data.mongodb.core.FindAndModifyOptions;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.query.Criteria;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.data.mongodb.core.query.Update;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;

/**
 * Apache Camel routes that drive the IssuanceSaga state machine.
 *
 * WHAT IS A CAMEL ROUTE? (see .docs/getting-started.md)
 * A Camel Route is a pipeline: from(source) → process → to(destination).
 * Think of it as a BizTalk orchestration, but defined in Java code.
 * Each route handles one type of incoming message and defines what to do with it.
 */
@Component
public class IssuanceSagaRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(IssuanceSagaRoute.class);

    private final IssuanceSagaRepository repository;
    private final ObjectMapper objectMapper;
    private final MongoTemplate mongoTemplate;

    public IssuanceSagaRoute(IssuanceSagaRepository repository, ObjectMapper objectMapper,
                             MongoTemplate mongoTemplate) {
        this.repository = repository;
        this.objectMapper = objectMapper;
        this.mongoTemplate = mongoTemplate;
    }

    @Override
    public void configure() throws Exception {

        // Global DLQ handler: 2 retries with exponential backoff, then dead-letter.
        // handled(true) commits the Kafka offset so the poison message is not redelivered indefinitely.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                String issuanceId = exchange.getIn().getHeader("issuanceId", String.class);
                log.error("Unhandled exception in issuance-saga — routing to DLQ. issuanceId={} routeId={} error={}",
                        issuanceId, exchange.getFromRouteId(),
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:policy.dlq.issuance-saga");

        // ── Route 1: Accept IssuePolicy command from Kafka → start saga ──────────
        from("kafka:policy.commands.issue-policy?groupId=policy-issuance-saga")
            .routeId("saga-start")
            .log("IssuanceSaga starting for issuanceId=${header.issuanceId}")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                IssuePolicyCommand cmd = objectMapper.readValue(json, IssuePolicyCommand.class);
                String issuanceId = cmd.issuanceId();
                MDC.put("issuanceId", issuanceId);

                if (repository.existsById(issuanceId)) {
                    log.warn("Duplicate IssuePolicy received — saga already exists, skipping");
                    exchange.setRouteStop(true);
                    return;
                }

                IssuanceSagaRecord saga = new IssuanceSagaRecord();
                saga.setIssuanceId(issuanceId);
                saga.setAccountId(cmd.accountId());
                saga.setSubmittingChannel(cmd.submittingChannel() != null ? cmd.submittingChannel().name() : "Unknown");
                saga.setRequestedAt(cmd.requestedAt() != null ? cmd.requestedAt() : OffsetDateTime.now());
                saga.setStatus(IssuanceSagaRecord.SagaStatus.Initiated);

                if (cmd.policies() != null && !cmd.policies().isEmpty()) {
                    saga.setPolicyTypeCode(cmd.policies().get(0).policyTypeCode());
                    saga.setPolicyTypeSubCode(cmd.policies().get(0).policyTypeSubCode());
                }
                repository.save(saga);

                PolicyIssuanceInitiatedEvent event = new PolicyIssuanceInitiatedEvent(
                        issuanceId,
                        cmd.accountId(),
                        saga.getPolicyTypeCode(),
                        saga.getRequestedAt()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(event));
                exchange.getIn().setHeader("issuanceId", issuanceId);

                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingCompliance);
                repository.save(saga);
                log.info("[EDA publish] PolicyIssuanceAndLifecycleManagement publishing PolicyIssuanceInitiatedEvent — issuanceId={}", issuanceId);
                log.info("IssuanceSaga INITIATED — publishing PolicyIssuanceInitiated (EDA: platform.compliance will subscribe and screen) — issuanceId={}", issuanceId);
                MDC.clear();
            })
            .to("kafka:policy.events.policy-issuance-initiated");

        // ── Route 2: ComplianceCleared → request account record ──────────────────
        from("kafka:compliance.events.compliance-cleared?groupId=policy-issuance-saga")
            .routeId("saga-compliance-cleared")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                ComplianceClearedEvent event = objectMapper.readValue(json, ComplianceClearedEvent.class);
                String issuanceId = event.correlationId();
                MDC.put("issuanceId", issuanceId);
                log.info("[EDA subscriber] PolicyIssuanceAndLifecycleManagement received ComplianceClearedEvent — issuanceId={}", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingAccountRecord);
                repository.save(saga);

                AccountLookupRequestedEvent accountLookupRequestedEvent = new AccountLookupRequestedEvent(
                        issuanceId,
                        saga.getAccountId(),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(accountLookupRequestedEvent));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                log.info("[EDA publish] PolicyIssuanceAndLifecycleManagement publishing AccountLookupRequestedEvent — issuanceId={}", issuanceId);
                log.info("ComplianceCleared — publishing AccountLookupRequested (EDA: customer-identity will subscribe and retrieve record) — issuanceId={}", issuanceId);
                MDC.clear();
            })
            .to("kafka:customer.events.account-lookup-requested");

        // ── Route 3: ComplianceBlocked → terminate saga ───────────────────────────
        from("kafka:compliance.events.compliance-blocked?groupId=policy-issuance-saga")
            .routeId("saga-compliance-blocked")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                ComplianceBlockedEvent event = objectMapper.readValue(json, ComplianceBlockedEvent.class);
                String issuanceId = event.correlationId();
                MDC.put("issuanceId", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setStatus(IssuanceSagaRecord.SagaStatus.ComplianceBlocked);
                saga.setFailureReason("ComplianceBlocked: " + event.blockReason());
                saga.setCompletedAt(OffsetDateTime.now());
                repository.save(saga);
                log.warn("Saga terminated — ComplianceBlocked reason={}", event.blockReason());

                var failEvent = new IssuanceFailedEvent(issuanceId, "ComplianceBlocked", OffsetDateTime.now());
                exchange.getIn().setBody(objectMapper.writeValueAsString(failEvent));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                MDC.clear();
            })
            .to("kafka:policy.events.issuance-failed");

        // ── Route 4: AccountServiceRecordRetrieved → publish IssuePolicyRequested ──
        from("kafka:customer.events.account-service-record-retrieved?groupId=policy-issuance-saga")
            .routeId("saga-account-retrieved")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                AccountServiceRecordRetrievedEvent event = objectMapper.readValue(json, AccountServiceRecordRetrievedEvent.class);
                String issuanceId = event.correlationId();
                MDC.put("issuanceId", issuanceId);
                log.info("[EDA subscriber] PolicyIssuanceAndLifecycleManagement received AccountServiceRecordRetrievedEvent — issuanceId={}", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setAccountServiceRequestNumber(event.accountServiceRequestNumber());
                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingPAS);
                repository.save(saga);

                var pasEvent = new IssuePolicyRequestedEvent(
                        issuanceId,
                        saga.getAccountId(),
                        event.accountServiceRequestNumber(),
                        java.util.List.of(new IssuePolicyRequestedEvent.PolicyItem(
                                saga.getPolicyTypeCode(), saga.getPolicyTypeSubCode(), null
                        )),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(pasEvent));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                exchange.getIn().setHeader("policyTypeCode", String.valueOf(saga.getPolicyTypeCode()));
                log.info("[EDA publish] PolicyIssuanceAndLifecycleManagement publishing IssuePolicyRequestedEvent — issuanceId={}", issuanceId);
                MDC.clear();
            })
            .to("kafka:policy.events.issue-policy-requested");

        // ── Route 5: PolicyAdminSystemResponseReceived → update saga state only ──
        from("kafka:integration.events.policy-admin-system-response-received?groupId=policy-issuance-saga")
            .routeId("saga-pas-response")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyAdminSystemResponseReceivedEvent event = objectMapper.readValue(json, PolicyAdminSystemResponseReceivedEvent.class);
                String issuanceId = event.issuanceId();
                MDC.put("issuanceId", issuanceId);
                log.info("[EDA subscriber] PolicyIssuanceAndLifecycleManagement received PolicyAdminSystemResponseReceivedEvent — issuanceId={}", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setTargetPas(event.targetPas());
                saga.setPolicyNumbers(event.policyNumbers());
                if (event.accountServiceRequestNumber() != null) {
                    saga.setAccountServiceRequestNumber(event.accountServiceRequestNumber());
                }
                saga.setStatus(IssuanceSagaRecord.SagaStatus.PASConfirmed);
                repository.save(saga);
                log.info("PASConfirmed — issuanceId={} targetPas={} policyNumbers={} (EDA fan-out: billing-finance and customer-identity subscribed to PolicyAdminSystemResponseReceived)",
                        issuanceId, event.targetPas(), event.policyNumbers());
                exchange.getIn().setHeader("issuanceId", issuanceId);
                MDC.clear();
            })
            .stop();

        // ── Route 6: BillingAssociationCreated → check join condition ─────────────
        from("kafka:billing.events.billing-association-created?groupId=policy-issuance-saga")
            .routeId("saga-billing-complete")
            .process(exchange -> checkJoinCondition(exchange, "billing"))
            .to("kafka:policy.events.policy-issued");

        // ── Route 7: CustomerUpdated → check join condition ───────────────────────
        from("kafka:customer.events.customer-updated?groupId=policy-issuance-saga")
            .routeId("saga-customer-updated")
            .process(exchange -> checkJoinCondition(exchange, "customer"))
            .to("kafka:policy.events.policy-issued");

        // ── Route 8: PAS call failed → retry or fail ──────────────────────────────
        from("kafka:integration.events.policy-admin-system-call-failed?groupId=policy-issuance-saga")
            .routeId("saga-pas-failed")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyAdminSystemCallFailedEvent event = objectMapper.readValue(json, PolicyAdminSystemCallFailedEvent.class);
                String issuanceId = event.issuanceId();
                MDC.put("issuanceId", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                int retries = saga.getPasRetryCount() + 1;
                saga.setPasRetryCount(retries);

                if (retries < 3) {
                    log.warn("PAS call failed — retry {}/3 for issuanceId={}", retries, issuanceId);
                    saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingPAS);
                    repository.save(saga);
                    // Re-publish IssuePolicyRequested (subscription filter re-routes)
                    var pasEvent = new IssuePolicyRequestedEvent(
                            issuanceId, saga.getAccountId(), saga.getAccountServiceRequestNumber(),
                            java.util.List.of(), OffsetDateTime.now()
                    );
                    exchange.getIn().setBody(objectMapper.writeValueAsString(pasEvent));
                    exchange.getIn().setHeader("issuanceId", issuanceId);
                    exchange.getIn().setHeader("policyTypeCode", String.valueOf(saga.getPolicyTypeCode()));
                    exchange.setProperty("shouldRetry", true);
                } else {
                    log.error("PAS call failed after 3 retries — failing saga for issuanceId={}", issuanceId);
                    saga.setStatus(IssuanceSagaRecord.SagaStatus.Failed);
                    saga.setFailureReason("PASCallFailed after 3 retries: " + event.failureReason());
                    saga.setCompletedAt(OffsetDateTime.now());
                    repository.save(saga);
                    var failEvent = new IssuanceFailedEvent(issuanceId, "PASCallFailed", OffsetDateTime.now());
                    exchange.getIn().setBody(objectMapper.writeValueAsString(failEvent));
                    exchange.setProperty("shouldRetry", false);
                }
                MDC.clear();
            })
            .choice()
                .when(exchangeProperty("shouldRetry").isEqualTo(true))
                    .to("kafka:policy.events.issue-policy-requested")
                .otherwise()
                    .to("kafka:policy.events.issuance-failed")
            .end();
    }

    /**
     * Atomically sets the branch completion flag and checks the join condition (BR-PIL-003).
     *
     * The previous read-modify-write (findById → setFlag → save) had a race condition: if billing
     * and customer events arrived concurrently, each thread could read stale data before the other
     * had persisted its flag, meaning neither thread saw both flags set and PolicyIssued was never
     * published. Fixed by using MongoDB findAndModify (atomic set + returnNew), which guarantees
     * exactly one thread observes both flags = true and wins the CAS to publish PolicyIssued.
     */
    private void checkJoinCondition(org.apache.camel.Exchange exchange, String branch) throws Exception {
        String json = exchange.getIn().getBody(String.class);
        String issuanceId;
        if ("billing".equals(branch)) {
            BillingAssociationCreatedEvent event = objectMapper.readValue(json, BillingAssociationCreatedEvent.class);
            issuanceId = event.issuanceId();
            log.info("[EDA subscriber] PolicyIssuanceAndLifecycleManagement received BillingAssociationCreatedEvent — issuanceId={}", issuanceId);
        } else {
            CustomerUpdatedEvent event = objectMapper.readValue(json, CustomerUpdatedEvent.class);
            issuanceId = event.correlationId();
            log.info("[EDA subscriber] PolicyIssuanceAndLifecycleManagement received CustomerUpdatedEvent — issuanceId={}", issuanceId);
        }
        MDC.put("issuanceId", issuanceId);

        // Step 1: Atomically set the branch flag; returnNew=true gives us the post-update state.
        String flagField = "billing".equals(branch) ? "billingComplete" : "customerUpdateComplete";
        var flagQuery = Query.query(Criteria.where("_id").is(issuanceId));
        var flagUpdate = new Update().set(flagField, true);
        IssuanceSagaDocument updated = mongoTemplate.findAndModify(
                flagQuery, flagUpdate,
                FindAndModifyOptions.options().returnNew(true).upsert(false),
                IssuanceSagaDocument.class);

        if (updated == null) {
            log.warn("No saga document for issuanceId={} in checkJoinCondition — skipping", issuanceId);
            exchange.setRouteStop(true);
            MDC.clear();
            return;
        }

        if ("billing".equals(branch)) {
            log.info("Billing branch complete — waitingForCustomer={}", !updated.isCustomerUpdateComplete());
        } else {
            log.info("Customer branch complete — waitingForBilling={}", !updated.isBillingComplete());
        }

        if (updated.isBillingComplete() && updated.isCustomerUpdateComplete()) {
            // Step 2: Both flags are set. Use a conditional CAS to transition status to Completed
            // so exactly one of the two concurrent threads publishes PolicyIssued (BR-PIL-003).
            var completeQuery = Query.query(
                    Criteria.where("_id").is(issuanceId)
                            .and("status").ne("Completed"));
            var completeUpdate = new Update()
                    .set("status", "Completed")
                    .set("completedAt", OffsetDateTime.now());
            IssuanceSagaDocument completed = mongoTemplate.findAndModify(
                    completeQuery, completeUpdate,
                    FindAndModifyOptions.options().returnNew(true).upsert(false),
                    IssuanceSagaDocument.class);

            if (completed == null) {
                log.debug("PolicyIssued already published by sibling branch — suppressing duplicate for issuanceId={}", issuanceId);
                exchange.setRouteStop(true);
            } else {
                log.info("[EDA join] IssuanceSaga — billingComplete={} customerUpdateComplete={} — both branches done → PolicyIssued",
                        completed.isBillingComplete(), completed.isCustomerUpdateComplete());
                log.info("[EDA publish] PolicyIssuanceAndLifecycleManagement publishing PolicyIssuedEvent — issuanceId={}", issuanceId);
                var issued = new PolicyIssuedEvent(
                        issuanceId,
                        completed.getAccountServiceRequestNumber(),
                        completed.getPolicyNumbers(),
                        completed.getTargetPas(),
                        OffsetDateTime.now()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(issued));
                exchange.getIn().setHeader("issuanceId", issuanceId);
            }
        } else {
            exchange.setRouteStop(true);
        }
        MDC.clear();
    }
}

