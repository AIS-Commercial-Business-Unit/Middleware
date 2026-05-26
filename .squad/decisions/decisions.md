# Decision Log

---

## Decision: Java/Apache Camel Stack for BizTalk Replacement

**Decision ID:** architect-java-vs-dotnet-recommendation  
**Status:** Proposed  
**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing  

---

### Context

Client is modernizing 67+ BizTalk applications across four business portfolios (SCI: 18, PRS/RiskID: 19, ClaimCare: 30, ECOS: 4+). The client's existing operational team runs Java, Kafka, MongoDB, SQL Server, and Docker. The question was whether to build the replacement on Java/Apache Camel or .NET/NServiceBus.

### Decision

**Java/Apache Camel** is the recommended stack. Unambiguous. No conditions.

### Rationale

1. **Adapter coverage:** Every BizTalk adapter in use (IBM MQ, SFTP, File, WCF/SOAP, SQL polling, DB2) has a mature Camel component. .NET would require Logic Apps or custom code for the same coverage.
2. **Operational fit:** The team already runs Java/Kafka/MongoDB. Adopting .NET requires full team retraining.
3. **Licensing:** Camel is Apache 2.0 (free). NServiceBus is commercially licensed per-endpoint.
4. **One operational model:** Camel as Spring Boot containers on Kafka. No Logic Apps sprawl. No split tooling.
5. **The saga gap is manageable:** NServiceBus has better saga primitives, but sagas are implementable on Camel/Kafka with the architecture already designed.

### Consequences

- Professional services engagement focuses on program execution (discovery, architecture, migration, testing, change management) rather than technology platform adoption
- AIS delivers integration architecture expertise, not .NET retraining
- Estimated 12-18 month program duration
- Zero framework licensing cost

### Alternatives Considered

- **.NET/NServiceBus:** Superior saga support but requires team retraining, commercial licensing, Logic Apps for adapter gaps, and introduces operational complexity
- **Hybrid:** Rejected — maintaining two stacks doubles operational burden

### Supporting Document

Full analysis: `.docs/java-vs-dotnet-biztalk-replacement.md`

---

## Decision: AIS Stack Framing for BizTalk Replacement

**Decision ID:** architect-ais-stack-framing  
**Status:** Adopted  
**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing

---

### Context

Architecture review of Azure Integration Services as a differentiator in technology selection. Azure services (APIM, Blob Storage, Key Vault, App Config, Entra ID, Monitor, App Insights) are shared across both Java/Camel and .NET/NServiceBus stacks.

### Decision

Frame Azure Integration Services as a **shared platform baseline plus .NET-specific additions**, not as an inherent reason to choose .NET.

### Shared by Both Stacks

- Azure API Management
- Azure Blob Storage
- Azure Key Vault
- Azure App Configuration
- Microsoft Entra ID
- Azure Monitor / Application Insights

These are not differentiators. Both Java/Camel and .NET/NServiceBus can use them.

### Differentiating Stack Impact

#### Java + Kafka + Camel
- Reuses Kafka as the message bus
- Keeps integration logic in containerized services
- Eliminates Azure Service Bus
- Eliminates Azure Logic Apps

#### .NET + NServiceBus
- Keeps Kafka for event streaming because it already exists in production
- Typically adds Azure Service Bus for NServiceBus transport in Azure
- Or adds SQL Server as message infrastructure if avoiding Service Bus
- Adds Logic Apps for protocol adapters Camel handles natively (SFTP, IBM MQ bridging, file polling, SQL polling, AS2)

### Rationale

If the client already has Kafka, the Azure Integration Services pitch becomes an argument against the .NET path: it introduces a second message bus and a second integration runtime instead of simplifying the estate. Java/Camel is the smaller target architecture because it preserves the shared Azure platform services while removing Azure Service Bus and Logic Apps from the solution.

---

## Decision: Pattern Checklist Closes the Technology Debate

**Decision ID:** architect-patterns-org-argument  
**Status:** Adopted  
**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing

---

### Context

Clarifying the scope of the technical platform decision. Once a platform demonstrates the major Enterprise Integration Patterns required by the BizTalk estate, the technical viability is proven.

### Decision

Position the BizTalk modernization argument around a hard line: once the target platform demonstrates the major Enterprise Integration Patterns required by the estate, the technical debate is over and the remaining risk is organizational.

### Pattern Checklist (Proof of Capability)

The platform must prove, end-to-end, the patterns that matter for BizTalk replacement:

- Dead letter queue
- Retry / redelivery
- Publish / subscribe
- Scatter-gather
- Saga / long-running process
- Idempotent consumer
- Outbox
- Content-based routing
- Message translation
- Data movement / ETL-style adapter flows

In this repository, that proof point is already established across the Java/Camel and .NET/NServiceBus demonstrations.

### Consequences

If the checklist is green, the client does not have a technology selection problem. The client has a delivery problem:

- Sequencing 67+ applications
- Running discovery deeply enough to uncover real customizations
- Standing up CI/CD before the first migration ships
- Training teams on the new operational model
- Governing domain boundaries across portfolios

### Services Framing

The winning professional services message is not "our stack is better than BizTalk." That is expected. The winning message is that AIS has the methodology, architecture patterns, and program discipline to migrate a large BizTalk estate without the effort collapsing under dependency chaos, weak testing, and manual deployment.

### Operational Requirement

CI/CD is a prerequisite, not a follow-up task. If the target organization cannot build, test, promote, and observe integration services independently, the first successful migration will become the first manually operated liability.

---

## Directive: Organizational Problem vs. Technical Problem

**Directive ID:** copilot-organizational-framing  
**Status:** Captured  
**Date:** 2026-05-26  
**Author:** Steven Suing (via Copilot)  
**Source:** User directive

---

### Context

Clarification of the core winning argument for professional services engagement.

### Directive

If all major EIP patterns are accounted for in the platform (dead letter queue, retry, pub/sub, scatter-gather, saga, idempotent messages, outbox, translation/data movement), then the client's problem is NOT a technical problem. It is an organizational and process problem — program management, strategy, good communication, team organization, and CI/CD investment.

### Rationale

This is the core winning argument for professional services. The platform proof establishes viability; AIS's value is in delivering program discipline, architecture governance, and operational enablement across 67+ applications without the effort collapsing.
