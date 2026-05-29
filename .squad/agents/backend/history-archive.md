# Backend Agent — History Archive

Older learnings archived from history.md on 2026-05-29.

## Archived Entries

### 2026-05-27 — EDA flow logging across all Java services (backend-4 complete)

- **Satellite services now have EDAFlowProcessor instrumentation.** All five downstream services (Compliance, Customer Identity, Integration, Billing, Notification) now emit route-boundary Kafka `EDA_FLOW` logs identical to policy issuance. This enables the live ops sequence diagram to render the complete fan-out topology including Integration→PolicyAdminSystemResponseReceivedEvent→Billing and Customer Identity consumption paths.

- **Verification:** Issuance 2321192b-72dd-497b-8439-37f2e7c349a9 traced through Compliance, Customer Identity, Integration (PAS), and Billing with all `EDA_FLOW` entries confirmed. The sequence diagram now renders the canonical Java EDA flow end-to-end.

- **Satellite services need the same route-boundary Kafka interceptors as policy issuance.** Adding `interceptFrom("kafka:*")` and `interceptSendToEndpoint("kafka:*")` with a local `EDAFlowProcessor` in Compliance, Customer Identity, Integration, Billing, and Notification makes the live sequence diagram show real fan-out hops instead of only orchestrator-side edges.

- **Outbound topic extraction must normalize both `kafka:topic` and `kafka://topic`.** Reusing `uri.replaceFirst("^kafka:(//)?", "")` plus query-string trimming keeps `EDA_Topic` correct for both direct `.to("kafka:...")` and intercepted endpoint URIs.

- **These Java service Dockerfiles package prebuilt `target/*.jar` files rather than compiling during image build.** In environments without a local JDK/Maven, the reliable fallback is to package the modules in a Maven container first, then rebuild the service images so the new observability code is actually present in the containers.

### Earlier entries

(Prior sessions with UC1 foundation work, EDA architecture decisions, baseline platform setup. See git history for complete record.)
