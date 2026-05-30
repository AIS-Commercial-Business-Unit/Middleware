# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel (Java DSL + YAML DSL), Kafka, MongoDB, Azure, Docker
- **Architecture:** Event-driven pub/sub; all Camel components abstract-interfaced for stack portability; schema registry for Kafka events; DLQ on every consumer
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

## Summary of Prior Work (2026-05-25 to 2026-05-28)

- **2026-05-26:** Integration coverage matrix research across 5 Chubb BizTalk applications (SCI, PRS, ClaimCare, ECOS, Sanctions); identified IBM MQ, WCF, MSMQ, DB2 as critical adapter patterns; output: `.docs/intel-integration-coverage.md`
- **2026-05-25:** UC3 file polling, CSV parsing, atomic MongoDB counters, Kafka group ID isolation patterns established; DLQ pattern standardized across 9 routes (exponential backoff, handled(true)); race condition in saga join condition fixed with `findAndModify` atomic operation
- **2026-05-27:** Renewal volume bootstrap fixed (named volume ownership issue); `FileProcessingDirectoryInitializer` added for startup validation
- **2026-05-28:** UC4 RiskIDMQGateway seam pattern established (direct:riskid-kafka-publish); AppraisalReceivedEvent created; prs.* topics pre-created in kafka-setup; demo gap markers visible in logs; 8 requirements gaps documented

### 2026-05-30 — UC4 prs-appraisal-service Full Rewrite (GetAppraisalList + GetAppraisalDocument)

- **Scope boundary is non-negotiable**: `prs-appraisal-service` now owns ONLY the two query-side workflows (GetAppraisalList scatter-gather, GetAppraisalDocument content-based router). The UpdateStatus saga is entirely removed. No saga state, no MongoDB saga documents, no Kafka consumers — only Kafka producers for audit events.

- **Scatter-gather multicast**: Camel `multicast(AggregationStrategy).parallelProcessing().timeout(30_000)` is the correct construct for BR-APR-001/002. The two branches (`direct:callAtWorkSQL`, `direct:callDEIPDE07MQList`) run in parallel and the `AppraisalListAggregationStrategy` merges them. The 30s `timeout()` on the multicast enforces the overall BR-APR-002 deadline without any manual deadline loop.

- **ConsumerTemplate for MQ poll loops**: Both `AppraisalListMqPoller` and `PdfChunkMqPoller` use `ConsumerTemplate.receive(endpoint, timeoutMs)` in a manual while-loop. This is the correct pattern for async-to-sync MQ bridges where loop control (sequence detection, sentinel detection) must happen in Java code. `pollEnrich()` inside `loopDoWhile()` has complexity with exchange property propagation.

- **JMS selector URL encoding**: The JMS message selector `JMSCorrelationID = 'value'` must be URL-encoded when embedded in the Camel endpoint URI. Use `java.net.URLEncoder.encode(selector, UTF_8)` when constructing the endpoint URI in processors.

- **DEIPDE07 message format**: AppraisalList messages are KEY=VALUE plain-text lines with a `SEQUENCE=X OF N` header. The poller detects completion when X == N. PDF document chunks are raw base64 strings with `\r\n` EBCDIC artifacts; the last chunk carries `||END-OF-DOCUMENT||` sentinel. Strip sentinel first, then strip `\r\n`.

- **JmsConfig explicit bean approach**: Provide `@Bean JmsComponent jms(ConnectionFactory connectionFactory)` wired to Spring Boot's auto-configured Artemis `ConnectionFactory`. Do NOT rely on the `camel.component.jms.connection-factory` YAML property when providing an explicit bean — the bean takes precedence and the YAML stanza is redundant/conflicting.

- **partialResult flag propagation**: The `AppraisalListMqPoller` sets `exchange.setProperty("mqPartialResult", boolean)`. The `AppraisalListAggregationStrategy` reads this property from either branch exchange and propagates it to the merged `AppraisalListResponse.partialResult` field. This is the correct pattern for propagating branch-level flags through a multicast aggregation.

- **onException in RouteBuilder**: `onException(SomeException.class)` in `configure()` does NOT support `.routeId()` — that method only exists on `RouteDefinition`. Remove `.routeId()` from all `onException` blocks.

- **Key files created (2026-05-30)**:
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/domain/` — 5 domain records + DocumentKeySource enum
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/api/AppraisalController.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/GetAppraisalListRoute.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/GetAppraisalDocumentRoute.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/AppraisalAuditEventRoute.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/processor/AppraisalListMqPoller.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/processor/PdfChunkMqPoller.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/processor/AppraisalListAggregationStrategy.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/application/gateway/AtWorkGateway.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/application/gateway/RiskIdWcfGateway.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/config/JmsConfig.java`


### 2026-05-30 — prs-appraisal-service rewrite for UC4 (scatter-gather + CBR routes)

- **Scatter-gather aggregation must detect timeouts and return partialResult.** GetAppraisalListRoute fans out to @Work SQL and DEIPDE07 MQ in parallel. When DEIPDE07 times out after 30 seconds, the aggregation route must not fail the entire request; instead, it returns only the @Work results with partialResult=true so the UI can inform the user. Use onTimeoutDelay on the Splitter + AggregationStrategy to implement this cleanly.

- **Content-based routing by key format avoids service proliferation.** GetAppraisalDocument routes _RiskID_I/_RiskID_A keys to RiskID WCF service and numeric keys to DEIPDE07 MQ. A single route with a choice/when/otherwise DSL is simpler than separate endpoint and gateway services. The route IDs are all kebab-case for consistency.

- **Camel ConsumerTemplate poll loop + sentinel detection is the pattern for multi-message aggregation.** Instead of a blocking aggregate phase, PdfChunkMqPoller uses a ConsumerTemplate.receive(queue, timeout) loop that collects messages until it sees ||END-OF-DOCUMENT|| in the body. This avoids deadlock and timeout issues inherent in the Splitter/Aggregator EIP.

- **Audit events must publish after route completion, not during.** AppraisalAuditEventRoute publishes to prs.events.appraisal-list-retrieved and prs.events.document-retrieved Kafka topics. These are published AFTER the response is committed, ensuring the events reflect the outcome (success, partialResult=true, error) not just an attempt.
