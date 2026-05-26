# Decision Note: Pattern Checklist Ends the Technology Debate

**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing

## Decision

Position the BizTalk modernization argument around a hard line: once the target platform demonstrates the major Enterprise Integration Patterns required by the estate, the technical debate is over and the remaining risk is organizational.

## Pattern Checklist

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

## Implication

If the checklist is green, the client does not have a technology selection problem. The client has a delivery problem:

- Sequencing 67+ applications
- Running discovery deeply enough to uncover real customizations
- Standing up CI/CD before the first migration ships
- Training teams on the new operational model
- Governing domain boundaries across portfolios

## Services Framing

The winning professional services message is not "our stack is better than BizTalk." That is expected. The winning message is that AIS has the methodology, architecture patterns, and program discipline to migrate a large BizTalk estate without the effort collapsing under dependency chaos, weak testing, and manual deployment.

## Operational Requirement

CI/CD is a prerequisite, not a follow-up task. If the target organization cannot build, test, promote, and observe integration services independently, the first successful migration will become the first manually operated liability.
