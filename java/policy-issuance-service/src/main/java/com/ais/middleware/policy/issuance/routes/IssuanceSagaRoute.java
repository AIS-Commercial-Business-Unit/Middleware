package com.ais.middleware.policy.issuance.routes;

import com.ais.middleware.common.events.compliance.ComplianceBlockedEvent;
import com.ais.middleware.common.events.compliance.ComplianceClearedEvent;
import com.ais.middleware.common.events.customer.AccountServiceRecordRetrievedEvent;
import com.ais.middleware.common.events.customer.UpdateCustomerRecordCommand;
import com.ais.middleware.common.events.billing.AssociateBillingAccountCommand;
import com.ais.middleware.common.events.billing.BillingAssociationCreatedEvent;
import com.ais.middleware.common.events.customer.CustomerUpdatedEvent;
import com.ais.middleware.common.events.integration.PolicyAdminSystemResponseReceivedEvent;
import com.ais.middleware.common.events.integration.PolicyAdminSystemCallFailedEvent;
import com.ais.middleware.common.events.policy.*;
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

                // Idempotency: skip if saga already exists
                if (repository.existsById(issuanceId)) {
                    log.warn("Duplicate IssuePolicy received — saga already exists, skipping");
                    exchange.setRouteStop(true);
                    return;
                }

                // Create and persist the saga record
                IssuanceSagaRecord saga = new IssuanceSagaRecord();
                saga.setIssuanceId(issuanceId);
                saga.setAccountId(cmd.accountId());
                saga.setSubmittingChannel(cmd.submittingChannel() != null ? cmd.submittingChannel().name() : "Unknown");
                saga.setRequestedAt(cmd.requestedAt() != null ? cmd.requestedAt() : java.time.OffsetDateTime.now());
                saga.setStatus(IssuanceSagaRecord.SagaStatus.Initiated);

                if (cmd.policies() != null && !cmd.policies().isEmpty()) {
                    saga.setPolicyTypeCode(cmd.policies().get(0).policyTypeCode());
                    saga.setPolicyTypeSubCode(cmd.policies().get(0).policyTypeSubCode());
                }
                repository.save(saga);
                log.info("IssuanceSaga created — status=Initiated");

                // Send RequestComplianceCheck to Platform.Compliance
                var checkCmd = new com.ais.middleware.common.events.compliance.RequestComplianceCheckCommand(
                        java.util.UUID.randomUUID().toString(),
                        issuanceId,
                        "Policy",
                        "EconomicSanctions",
                        "CommercialAccount",
                        cmd.accountId(),
                        objectMapper.writeValueAsString(cmd)
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(checkCmd));
                exchange.getIn().setHeader("issuanceId", issuanceId);

                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingCompliance);
                repository.save(saga);
                log.info("IssuanceSaga transitioned — status=AwaitingCompliance");
                MDC.clear();
            })
            .to("kafka:compliance.commands.request-compliance-check");

        // ── Route 2: ComplianceCleared → request account record ──────────────────
        from("kafka:compliance.events.compliance-cleared?groupId=policy-issuance-saga")
            .routeId("saga-compliance-cleared")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                ComplianceClearedEvent event = objectMapper.readValue(json, ComplianceClearedEvent.class);
                String issuanceId = event.correlationId();
                MDC.put("issuanceId", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingAccountRecord);
                repository.save(saga);
                log.info("Compliance cleared — requesting account record");

                var cmd = new com.ais.middleware.common.events.customer.GetOrCreateAccountServiceRecordCommand(
                        issuanceId, saga.getAccountId(), saga.getAccountId()
                );
                exchange.getIn().setBody(objectMapper.writeValueAsString(cmd));
                exchange.getIn().setHeader("issuanceId", issuanceId);
                MDC.clear();
            })
            .to("kafka:customer.commands.get-or-create-account-record");

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

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setAccountServiceRequestNumber(event.accountServiceRequestNumber());
                saga.setStatus(IssuanceSagaRecord.SagaStatus.AwaitingPAS);
                repository.save(saga);
                log.info("Account record retrieved — publishing IssuePolicyRequested to PAS gateway");

                // This event is routed to the correct PAS via subscription filter in platform-integration-service
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
                MDC.clear();
            })
            .to("kafka:policy.events.issue-policy-requested");

        // ── Route 5: PolicyAdminSystemResponseReceived → fan-out parallel steps ──
        from("kafka:integration.events.policy-admin-system-response-received?groupId=policy-issuance-saga")
            .routeId("saga-pas-response")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                PolicyAdminSystemResponseReceivedEvent event = objectMapper.readValue(json, PolicyAdminSystemResponseReceivedEvent.class);
                String issuanceId = event.issuanceId();
                MDC.put("issuanceId", issuanceId);

                IssuanceSagaRecord saga = repository.findById(issuanceId).orElse(null);
                if (saga == null) { log.warn("No saga for issuanceId={}", issuanceId); return; }

                saga.setTargetPas(event.targetPas());
                saga.setPolicyNumbers(event.policyNumbers());
                saga.setStatus(IssuanceSagaRecord.SagaStatus.PASConfirmed);
                repository.save(saga);
                log.info("PAS confirmed — targetPas={} policyNumbers={} — starting parallel billing + customer update",
                        event.targetPas(), event.policyNumbers());

                // Parallel step 1: Billing association
                var billingCmd = new AssociateBillingAccountCommand(
                        issuanceId,
                        saga.getAccountServiceRequestNumber(),
                        event.policyNumbers(),
                        AssociateBillingAccountCommand.BillingChannel.DirectBill,
                        saga.getPolicyTypeCode()
                );
                String billingJson = objectMapper.writeValueAsString(billingCmd);

                // Parallel step 2: Customer record update (non-critical parallel path)
                var customerCmd = new UpdateCustomerRecordCommand(
                        issuanceId, saga.getAccountId(), null, null
                );
                String customerJson = objectMapper.writeValueAsString(customerCmd);

                exchange.getIn().setHeader("issuanceId", issuanceId);
                // Store command JSONs as exchange properties for sequential sends below
                exchange.setProperty("billingCommandJson", billingJson);
                exchange.setProperty("customerCommandJson", customerJson);
                MDC.clear();
            })
            // Send billing command with correct body, then customer command with correct body.
            // Sequential is fine for the demo — true parallel can be added via Camel Parallelism EIP later.
            .setBody(exchangeProperty("billingCommandJson"))
            .to("kafka:billing.commands.associate-billing-account")
            .setBody(exchangeProperty("customerCommandJson"))
            .to("kafka:customer.commands.update-customer-record")
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
        } else {
            CustomerUpdatedEvent event = objectMapper.readValue(json, CustomerUpdatedEvent.class);
            issuanceId = event.correlationId();
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
                log.info("Saga join complete — both branches done — publishing PolicyIssued for issuanceId={}", issuanceId);
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
