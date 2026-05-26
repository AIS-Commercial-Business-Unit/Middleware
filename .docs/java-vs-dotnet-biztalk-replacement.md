# Java vs .NET: BizTalk Replacement Comparison

**Author:** AIS Architecture Team  
**Date:** 2026-05-26  
**Audience:** Client executive stakeholders, AIS professional services leadership  
**Purpose:** Strategic recommendation for BizTalk modernization stack selection

---

## Executive Summary

This document recommends **Java/Apache Camel** as the target integration stack for this client's BizTalk modernization. The recommendation is grounded in three factors: (1) Apache Camel provides native, production-tested components for every integration pattern found in the client's BizTalk environment — EDI, SOAP/WCF, file-based batch, IBM MQ, SFTP, DB2, and SQL polling; (2) the client's operational team already runs Java, Kafka, MongoDB, and Docker in production; and (3) the actual risk in this migration is not technology selection but program execution — discovery completeness, orchestration-to-saga decomposition, cutover sequencing, and change management across 67+ BizTalk applications spanning four business portfolios. The Java/Kafka stack eliminates Azure Service Bus and Azure Logic Apps from the target architecture entirely; the .NET/NServiceBus path reintroduces both as new Azure spend on top of the Kafka investment the team has already made.

---

## The Comparison Frame

### In Scope

This comparison evaluates the two stacks specifically on:

- **EDI and proprietary protocol adapters** (X12, EDIFACT, AS2, HL7)
- **File-based integration patterns** (SFTP, file drop zones, batch processing, CSV/Excel parsing)
- **Message queue connectivity** (IBM MQ/MQSC, MSMQ)
- **Database polling and DB2 integration**
- **SOAP/WCF service consumption and publishing**
- **Complex orchestration decomposition** (BizTalk orchestrations → event-driven sagas)
- **Operational model fit** (team skills, licensing, support burden)

### Out of Scope (Not Differentiators)

| Concern | Why Excluded |
|---|---|
| **HTTP/REST APIs** | Azure API Management is shared by both stacks. Not a differentiator. |
| **Blob / artifact storage** | Azure Blob Storage is shared by both stacks. Not a differentiator. |
| **Secrets / configuration** | Azure Key Vault and App Configuration are shared by both stacks. Not a differentiator. |
| **Monitoring / Observability** | Azure Monitor and App Insights are shared by both stacks. Not a differentiator. |
| **Authentication / Identity** | Azure Entra ID handles both. Commodity. |
| **ETL / Data Pipeline** | Azure Data Factory is off the table. Neither stack handles this. |
| **Stack impact** | **Java + Kafka:** no Azure Service Bus, no Logic Apps. **.NET + NServiceBus:** Azure Service Bus or SQL transport, plus Logic Apps for adapter gaps. |

---

## BizTalk Integration Patterns Found in This Client's Environment

Based on analysis of the SCI, PRS, ECOS, and Sanctions POC documentation, the following BizTalk adapter usage and integration patterns are in active production:

### Adapter Inventory (from Intel Docs)

| BizTalk Adapter | Count of Apps Using | Example Application |
|---|---|---|
| **WCF-SQL** (SQL Stored Procedure polling) | SCI, PRS, ECOS | ECOS Outbound (10-12K txn/hour), Appraisal List |
| **WCF-WebHTTP** | SCI, PRS | Policy Issuance (EPL39X1) |
| **WCF-BasicHTTP / WCF-WSHTTP** | SCI, PRS, Sanctions | Appraisal Documents, CLink compliance |
| **SFTP** | SCI, PRS | Renewal batch files, external feeds |
| **File** (filesystem drop zones) | SCI, PRS | Renewal batch (40-60K records nightly) |
| **MSMQ** | SCI, PRS, ECOS | Inter-application messaging |
| **MQSC** (IBM MQ) | PRS, Sanctions | RiskID, Clearance processor, Trax |
| **DB2** | ECOS | Legacy mainframe integration |

### Orchestration Patterns

| Pattern | BizTalk Implementation | Business Context |
|---|---|---|
| **Multi-system saga with compensation** | BizTalk Orchestration (sequential) | Policy Issuance: Compliance → PAS → Billing → CRM |
| **File-based batch with per-record isolation** | BizTalk FILE receive + orchestration loop | 50K renewal records nightly; sequential today |
| **Request-response with MQ backends** | WCF receive → MQSC send → correlate reply | Sanctions screening (18K/day/region) |
| **SQL polling with transform + dispatch** | WCF-SQL polling → Map → WCF send | ECOS outbound (10-12K/hour) |
| **Dynamic routing via BRE** | Business Rules Engine + content-based routing | Clearance notifications to Cadence, UWP, WorkView, CGM |
| **ESB itinerary processing** | ESB Toolkit itinerary + pipeline | Sanctions compliance flow |
| **Scheduled batch with manifest validation** | FILE receive + .ctl manifest + orchestration | Renewal, Medicare eligibility, invoice feeds |

### Pattern-to-Stack Mapping

| Pattern | BizTalk Artifact | Java/Apache Camel Solution | .NET/NServiceBus Solution | Complexity Delta |
|---|---|---|---|---|
| IBM MQ consume/produce | MQSC Adapter | `camel-jms` + IBM MQ client (native, zero custom code) | MassTransit + IBM MQ transport OR custom `IHostedService` with IBM MQ .NET client | **Camel: trivial** / .NET: moderate custom |
| File drop zone monitoring | FILE Receive Location | `camel-file` component (polling, move, error folder built-in) | `FileSystemWatcher` + custom handling OR Logic Apps File connector | **Camel: trivial** / .NET: moderate custom |
| SFTP file retrieval | SFTP Adapter | `camel-sftp` (polling, retry, move, idempotent consumer built-in) | WinSCP .NET library + custom polling OR Logic Apps SFTP connector | **Camel: trivial** / .NET: moderate |
| SQL Polling (detect new rows) | WCF-SQL polling | `camel-sql` with scheduled polling + idempotent consumer | EF Core + `IHostedService` background polling OR Logic Apps SQL connector | **Camel: trivial** / .NET: moderate custom |
| DB2 connectivity | DB2 adapter | `camel-jdbc` with DB2 JDBC driver (standard) | IBM.Data.DB2 .NET provider (available but less common in .NET ecosystem) | **Comparable** |
| SOAP/WCF service call | WCF-BasicHTTP / WSHTTP | `camel-cxf` (full WS-* support, WSDL-first) | WCF client or `HttpClient` with manual SOAP envelope | **Camel: native** / .NET: WCF deprecated, manual |
| MSMQ messaging | MSMQ Adapter | `camel-jms` with ActiveMQ bridge OR direct Camel MSMQ (via JNI) | NServiceBus MSMQ transport (native, first-class) | **.NET wins** — NServiceBus owns MSMQ |
| Content-based routing (BRE) | Business Rules Engine | Camel routes with predicates, choice/when DSL | NServiceBus message handlers + custom routing logic | **Comparable** |
| Message transformation (maps) | BizTalk Maps (XSLT) | Camel processors, XSLT component, or Java mapping code | AutoMapper, XSLT, or custom C# mapping | **Comparable** |
| Saga/orchestration decomposition | BizTalk Orchestration | Camel + Kafka state stores OR embedded saga state machine | NServiceBus Sagas (native, first-class) | **.NET wins** — NServiceBus sagas are best-in-class |
| Batch file with per-record isolation | Orchestration loop | `camel-file` + splitter EIP + Kafka per-record dispatch | Custom file processor + NServiceBus message-per-record | **Camel: native EIP** / .NET: custom |
| Economic sanctions screening (high volume) | WCF-Service + Orchestration | Camel route: consume → transform → CLink call → route response | NServiceBus handler → HttpClient → route response | **Comparable** |

---

## Deep Dive: Where Camel Wins

### Protocol Adapter Coverage

Apache Camel's 300+ components provide **production-ready, community-maintained** adapters for every protocol in this client's BizTalk environment:

| BizTalk Need | Camel Component | Maturity | Notes |
|---|---|---|---|
| IBM MQ (MQSC) | `camel-jms` + `mq-jms-spring-boot-starter` | Production (20+ years) | Zero-config IBM MQ connectivity; Spring Boot auto-configuration |
| SFTP file feeds | `camel-ftp` / `camel-sftp` | Production | Built-in polling, idempotent consumer, move-on-success, error folder |
| File system drop zones | `camel-file` | Production | Glob patterns, polling interval, file locking, atomic move |
| SQL polling | `camel-sql` | Production | Scheduled polling, named queries, result-set-to-message mapping |
| DB2 | `camel-jdbc` | Production | Standard JDBC; any JDBC driver works |
| SOAP/WCF services | `camel-cxf` | Production | Full WS-Security, MTOM, WSDL-first code generation |
| EDI X12/EDIFACT | `camel-edi` (Smooks) | Production | X12, EDIFACT, HL7 parsing and validation |
| AS2 protocol | `camel-as2` | Production | B2B document exchange; signed, encrypted, receipt handling |
| Content-based routing | Camel DSL (`choice/when`) | Core | Enterprise Integration Pattern native to the framework |
| Splitter (batch→records) | Camel DSL (`split`) | Core | Streaming splitter for large files; parallel processing built-in |

### Enterprise Integration Patterns (EIPs) as First-Class Citizens

Camel implements **all 65 Enterprise Integration Patterns** from the Hohpe/Woolf book as first-class DSL constructs. This directly maps to BizTalk's orchestration patterns:

- **Splitter** → BizTalk's debatching/envelope processing
- **Aggregator** → BizTalk's convoy pattern
- **Content-Based Router** → BizTalk's subscription filters
- **Recipient List** → BizTalk's dynamic send ports
- **Wire Tap** → BizTalk's tracking/auditing
- **Dead Letter Channel** → BizTalk's suspended messages
- **Idempotent Consumer** → BizTalk's deduplication

### Specific Client Wins

1. **Sanctions Screening (18K transactions/day/region):** The BizTalk implementation uses WCF-Service → Orchestration → CLink SOAP call → dynamic routing to subscriber systems (Cadence, UWP, WorkView, CGM, TRAX). In Camel, this is a single route definition: consume from endpoint, transform via processor, call CLink via `camel-cxf`, route response via `choice/when` based on source system. No orchestration overhead.

2. **ECOS Outbound (10-12K/hour SQL polling):** BizTalk uses WCF-SQL polling adapter → orchestration → WCF service call → status update. In Camel: `camel-sql` scheduled poll → processor → `camel-cxf` call → `camel-sql` update. Four lines of route DSL.

3. **Renewal Batch (50K records/night):** BizTalk processes sequentially. Camel's streaming splitter with parallel processing and Kafka dispatch provides the per-record isolation pattern described in UC3 natively.

4. **IBM MQ (RiskID, Sanctions Clearance, Trax):** `camel-jms` with IBM MQ is a solved, production-tested integration used by thousands of enterprises. No custom adapter code required.

---

## Deep Dive: Where .NET Needs Help

### The NServiceBus Core Strength

Let's be honest about what NServiceBus does well:

- **Saga pattern:** NServiceBus sagas are the gold standard for long-running process coordination. They're type-safe, unit-testable, and handle timeout/retry natively.
- **Exactly-once processing semantics:** With SQL Server transport, NServiceBus provides transactional outbox and deduplication built-in.
- **Message routing:** Type-based routing and pub/sub are first-class.

### Where .NET Requires Additional Components

| BizTalk Need | .NET Solution Required | Concern |
|---|---|---|
| **Durable messaging transport in Azure** | **Azure Service Bus** for NServiceBus transport (typical) OR **SQL Server transport** | Service Bus adds a second message bus alongside Kafka; SQL transport swaps that for SQL Server as message infrastructure |
| **IBM MQ** | IBM MQ .NET client + custom `IHostedService` OR Azure Logic Apps / custom bridge into NServiceBus | Operational expertise gap; bridge pattern is extra moving parts that Camel avoids |
| **SFTP polling** | WinSCP .NET + custom scheduler OR Azure Logic Apps SFTP connector | Logic Apps becomes the practical adapter layer in Azure; custom code adds maintenance |
| **File drop zones** | `FileSystemWatcher` + custom error handling + idempotent consumer pattern OR Logic Apps file connector | Must build what Camel provides out of the box or add Logic Apps |
| **SQL polling** | EF Core + `BackgroundService` + custom polling loop OR Logic Apps SQL connector | Reinventing `camel-sql` from scratch or adding Logic Apps |
| **DB2** | IBM.Data.DB2 .NET provider (exists but niche in .NET world) | Less community support, fewer Stack Overflow answers |
| **SOAP/WCF consumption** | WCF is deprecated in .NET Core. Options: `System.ServiceModel` (limited), `HttpClient` with manual SOAP, or CoreWCF (server-only) | Active pain point for any .NET Core migration from WCF-heavy BizTalk |
| **EDI X12/EDIFACT** | EdiFabric (commercial, licensed per transaction) OR custom parsing | **No open-source equivalent to Smooks/camel-edi in .NET** |
| **AS2** | No mature .NET library; would require Logic Apps AS2 connector ($$$) or commercial library | Logic Apps AS2 connector adds significant per-message cost at scale |
| **Batch file splitting with parallel dispatch** | Custom implementation: parse → message-per-record → NServiceBus | Works but must be built and maintained |

### The Logic Apps Escape Hatch

For the protocol gaps, Microsoft's answer is typically **Azure Logic Apps**. In this migration that is not an optional convenience layer — it is the required adapter tier for SFTP, IBM MQ bridging, file polling, AS2, and SQL polling patterns that Camel handles natively inside the service. However:

| Logic Apps Concern | Impact |
|---|---|
| **Per-action pricing** | At 18K sanctions transactions/day × multiple actions per flow = significant monthly cost |
| **Operational sprawl** | Each Logic App is a separate Azure resource; 67+ BizTalk apps → potentially hundreds of Logic Apps |
| **Two operational models** | Team now operates NServiceBus workers + Logic Apps flows — different monitoring, different deployment, different debugging |
| **Consumption vs. Standard tier** | Standard tier requires dedicated App Service Plan; Consumption tier has cold-start latency |
| **Container boundary break** | Adapter logic now lives outside the containerized microservice architecture instead of with the service that owns the flow |
| **Vendor lock-in** | Logic Apps is Azure-only; no local development parity (despite VS Code extension) |
| **Testing** | Logic Apps are notoriously difficult to unit test compared to Camel routes or NServiceBus handlers |

The net effect is architectural, not cosmetic: the .NET option keeps Kafka for event streaming, adds Azure Service Bus (or SQL Server transport) for NServiceBus messaging, and adds Logic Apps for adapter coverage. That is three integration layers where Java + Kafka + Camel uses one.

### NServiceBus Licensing

NServiceBus is a **commercial product** with per-endpoint licensing. For a migration spanning 67+ BizTalk applications decomposed into dozens of microservices:

- **Development licenses:** Free (for dev/test only)
- **Production:** Requires Particular Platform license (undisclosed pricing, typically $10K–$50K+/year depending on scale)
- **Comparison:** Apache Camel is Apache 2.0 licensed. Zero licensing cost at any scale.

---

## Operational Fit Analysis

### What the Client Team Already Knows (Java Stack)

| Capability | Current State |
|---|---|
| **Java** | Production operational experience |
| **SQL Server** | Deep operational knowledge |
| **MongoDB** | Production operational experience |
| **Kafka** | Production operational experience |
| **Docker/Containers** | Standard deployment model |
| **Spring Boot** | Framework familiarity (implied by Java + Docker) |

With Java/Apache Camel:
- The messaging infrastructure (Kafka) is already operational
- The database tier (MongoDB for read models, SQL Server for transactional) is already staffed
- Container orchestration (Docker → AKS) is a natural extension
- Apache Camel runs as Spring Boot applications — same operational model as existing services
- Monitoring via Micrometer → Azure Monitor / Grafana is a known pattern

### What .NET Introduces (New Operational Surface)

| New Capability Required | Learning Curve | Risk |
|---|---|---|
| .NET Runtime operations | High | Team has no .NET production experience |
| NServiceBus deployment and monitoring | High | Proprietary tooling (ServicePulse, ServiceInsight) |
| Azure Service Bus or NServiceBus SQL transport | Medium | Either a second message bus or SQL Server as message infrastructure |
| C# development | High | Entire development team retooling |
| Logic Apps operations (for protocol gaps) | Medium | Azure Portal debugging, different deployment model |
| Visual Studio / Rider toolchain | Medium | IDE migration across team |

### The Operational Math

**Java stack components:**
- Kafka (already running) ✓
- MongoDB (already running) ✓
- Apache Camel on AKS (new, but zero licensing)
- Azure API Management (shared)
- Azure Blob Storage (shared)
- **Azure Service Bus: NOT NEEDED**
- **Logic Apps: NOT NEEDED**

**.NET stack components:**
- Kafka (still needed for event streaming — cannot eliminate it)
- MongoDB (still needed for domain persistence)
- NServiceBus on AKS (new, commercial license)
- Azure API Management (shared)
- Azure Blob Storage (shared)
- **Azure Service Bus: ADDED** (typical NServiceBus transport in Azure)
- **Logic Apps: ADDED** (protocol adapter gaps)
- SQL Server (message infrastructure if avoiding Service Bus via SQL transport)

That is the real cost model: Java adds Camel to the stack the team already runs. .NET adds NServiceBus **plus** either Azure Service Bus or SQL transport **plus** Logic Apps, while Kafka still remains in place for event streaming.

---

## The Real Question: Platform vs. Program

### Steven's Hypothesis, Validated

> *"The client's existing Java operational expertise, combined with Apache Camel's integration breadth, makes Java the right stack choice. What the client actually needs is professional services for program management, discovery, migration, testing, and change management — not a new technology platform."*

**This hypothesis is correct.** Here's why:

The BizTalk modernization challenge for this client is **not** a technology gap problem. It's a **program execution** problem:

1. **Discovery complexity:** 67+ BizTalk applications across 4 portfolios (SCI: 18 apps, PRS/RiskID: 19 apps, Claims/ClaimCare: 30 apps, ECOS: 4+ apps). Each application has receive locations, send ports, orchestrations, maps, schemas, pipelines, and BRE rules that need to be inventoried, understood, and decomposed.

2. **Orchestration decomposition:** Converting BizTalk's sequential, single-threaded orchestrations into event-driven sagas requires deep domain analysis. The Policy Issuance orchestration alone involves 5 domains, 3 external systems, and multiple compensation paths. This is architecture work, not framework work.

3. **Cutover sequencing:** With 18K sanctions transactions/day and 50K nightly renewal records, the migration must be phased with parallel-run periods, traffic shadowing, and progressive cutover. This is program management, not coding.

4. **Testing at scale:** The Sanctions system processes across EMEA, APAC, and LATAM regions with 24/7 availability requirements. Migration testing requires synthetic load generation, response comparison, and production-like environments.

5. **Change management:** Operations, support, and compliance teams need training on the new observability model (from BizTalk Admin Console to Grafana dashboards and control planes).

### What Professional Services Actually Looks Like

| Phase | Duration | Deliverables |
|---|---|---|
| **Discovery & Inventory** | 6-8 weeks | Complete artifact inventory, dependency mapping, integration flow documentation for all 67+ apps |
| **Architecture & Pattern Mapping** | 4-6 weeks | Orchestration-to-saga decomposition blueprints, adapter mapping, domain boundary definitions |
| **Reference Implementation** | 8-12 weeks | POC with 2-3 representative patterns (policy issuance saga, batch file processing, sanctions screening) |
| **Migration Execution (Wave 1)** | 12-16 weeks | First portfolio migrated (recommend: Sanctions — bounded scope, high visibility, 24/7 requirement validates operational readiness) |
| **Migration Execution (Waves 2-4)** | 24-36 weeks | Remaining portfolios migrated in priority order |
| **Parallel Run & Cutover** | 4-8 weeks per wave | Traffic shadowing, response comparison, progressive switchover |
| **Hypercare & Knowledge Transfer** | 8-12 weeks | Operational handoff, runbook creation, team enablement |

Total program duration: **12-18 months** for full migration.

The professional services value is in:
- **Program management** — sequencing 67+ applications through discovery → build → test → cutover
- **Architecture guidance** — ensuring the saga decomposition is correct and the domain boundaries hold
- **Integration expertise** — knowing the Camel component library and EIP patterns deeply enough to make clean translations from BizTalk artifacts
- **Testing methodology** — building confidence that 18K transactions/day and 50K batch records process correctly before cutover
- **Change management** — ensuring operations teams can run the new system from day one

---

## Final Recommendation

### Stack: Java / Apache Camel / Kafka / MongoDB

**Unambiguous. No conditions.**

The rationale:

1. **Adapter coverage is complete.** Every BizTalk adapter in this client's environment (IBM MQ, SFTP, File, WCF/SOAP, SQL polling, DB2) has a mature, production-tested Camel component equivalent. Zero custom adapter development required.

2. **The team already operates this stack.** Java, Kafka, MongoDB, Docker, SQL Server — all in production today. The operational risk of adding Apache Camel to this stack is minimal. The operational risk of adopting .NET, NServiceBus, and Logic Apps is substantial.

3. **EIP patterns are native.** Camel was built specifically to implement Enterprise Integration Patterns. The decomposition from BizTalk orchestrations to Camel routes is conceptually direct — both speak the same pattern language (Content-Based Router, Splitter, Aggregator, Dead Letter Channel).

4. **Zero licensing cost.** Apache Camel is Apache 2.0. NServiceBus licensing for 67+ applications at enterprise scale would be a significant annual cost with no corresponding capability advantage for this client's patterns.

5. **One operational model.** Camel runs as Spring Boot containers. Kafka is the messaging backbone. MongoDB + SQL Server handle persistence. Grafana + App Insights provide observability. No Azure Service Bus layered on top of Kafka. No Logic Apps sprawl. No second deployment model. No second monitoring tool.

6. **The saga gap is addressable.** NServiceBus has better saga primitives — acknowledged. However, sagas can be implemented cleanly with Camel + Kafka Streams state stores, or via a lightweight saga framework layered on top of the Camel/Kafka foundation. The UC1 IssuanceSaga and UC3 RenewalBatchSaga patterns are already designed technology-agnostically and are implementable on either stack.

### What .NET/NServiceBus Would Require That Java/Camel Does Not

- Full team retraining (Java → C#)
- NServiceBus licensing (commercial, ongoing)
- Azure Service Bus (NServiceBus Azure transport) **or** SQL Server (NServiceBus SQL transport) — adds a second messaging layer alongside Kafka
- Logic Apps Standard tier for protocol adapters — adds Azure-only, per-action-billed, non-containerizable integration flows
- New monitoring toolchain (ServicePulse/ServiceInsight)
- Kafka still remains in the estate for event streaming, so the .NET path adds infrastructure rather than removing it

### Professional Services Scope

The AIS recommendation to the client:

> *"Your technology choice is already made — your team runs Java, your patterns fit Apache Camel, and your messaging backbone (Kafka) is operational. What you need from AIS is the program to execute the migration: discovery of 67+ BizTalk applications, architecture for orchestration-to-saga decomposition, reference implementation validation, and phased migration execution with parallel-run cutover. This is a 12-18 month engagement focused on program management, integration architecture, and change management — not a platform rewrite."*

---

## Appendix A: Key Intel Sources

| Source Document | Key Finding |
|---|---|
| SCI BizTalk Modernization POC | 18 BizTalk apps; adapters: File, SFTP, MSMQ, WCF-WebHTTP, WCF-BasicHTTP, WCF-SQL; complex orchestrations including Policy Issuance (EPL39X1) |
| PRS BizTalk Modernization POC | 19 RiskID apps + 30 ClaimCare apps; adapters: FILE, SFTP, WCF-Service, DB2, MQSC, WCF-SQL; includes Appraisal Documents integration |
| Sanctions BizTalk Modernization POC | Sanctions checking across EMEA/APAC/LATAM; 18K compliance transactions/day/region; IBM MQ (MQSC), WCF-Service, BRE routing; 24/7 requirement |
| ECOS BizTalk Modernization POC | SQL polling at 10-12K txn/hour; DB2 integration; 4 BizTalk apps |
| UC1 Policy Issuance Rebuild Guide | Saga decomposition: Compliance → PAS → Billing → CRM; 5 domains, subscription filter self-selection pattern |
| UC3 Automated Renewal Batch Guide | 40-60K records nightly; per-record isolation; file processing framework with dead-letter and replay; SFTP/File drop zones |
| Sanctions Log Files | CLink SOAP integration (RSK3X3); XML message formats; compliance callback REST; Trax screening with IBM MQ |
| Platform.Integration Enterprise Spec | DuckCreek, ForeFront, Insurity PAS gateways; @Work, RiskID inspection systems; DEIPDE07/08 legacy archive |

## Appendix B: Camel Component Quick Reference for This Migration

```
camel-jms          → IBM MQ (MQSC adapter replacement)
camel-sftp         → SFTP receive/send locations
camel-file         → FILE receive/send locations
camel-sql          → WCF-SQL polling adapter replacement
camel-jdbc         → DB2 adapter replacement
camel-cxf          → WCF-BasicHTTP, WCF-WSHTTP, WCF-WebHTTP replacement
camel-kafka        → Event backbone (replaces MSMQ for inter-service messaging)
camel-edi (Smooks) → EDI X12/EDIFACT if future business requires it
camel-as2          → AS2 B2B document exchange if required
camel-bean         → Business logic invocation (replaces BRE calls)
camel-xslt         → BizTalk Map replacement (where XSLT is appropriate)
camel-jackson      → JSON transformation
camel-jaxb         → XML schema-driven transformation
```
