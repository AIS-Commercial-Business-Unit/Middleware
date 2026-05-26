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
