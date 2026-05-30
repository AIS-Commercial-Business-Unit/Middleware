# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Integration tests against live docker-compose stack, Serilog/structured log verification, Gatling or k6 for load tests
- **Key principle:** Test evidence required for every feature (inputs + expected output + actual output + log excerpts); log verification asserts structured log entries fired with correct properties
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

## Summary of Prior Work (2026-05-27 to 2026-05-29)

- **2026-05-27:** Batch processing demo investigation found cross-stack JSON serialization mismatch (Java camelCase vs .NET PascalCase in Kafka messages); documented solution in decision for future cross-stack contracts.
- **2026-05-28:** Created UC4 test scenarios (SC-001 through SC-007), demo script, confirmed dual-stack EDA_FLOW logging contract, documented 6 demo gaps (high/medium risk), established pattern distinction between "architecture proven" vs "business rules validated".
- **2026-05-29:** UC4 smoke test executed (SC-001 complete); fixed two bugs (MongoConfig missing OffsetDateTime codec, duplicate CAS criteria), verified all 5 stub gateways logged DEMO GAP markers, confirmed saga completion and join condition; established team rules for MongoConfig requirement and `.nin()` vs chained `.ne()` pattern.

### 2026-05-30 — UC4 integration tests: scatter-gather + CBR API contracts

- **Integration tests must use RestTemplate to exercise the full running stack, not @SpringBootTest.** The UC4 tests call http://localhost:8090 directly, ensuring Camel routes, JMS connections to Artemis, and gateway stubs are all exercised. Spinning up a second application context with @SpringBootTest would avoid testing the actual wiring.

- **Timeout guards are mandatory on tests that exercise MQ timeouts.** The GetAppraisalListIntegrationTest::timeout_deipde07_returnsPartialResult test uses @Timeout(40) to cap execution at 40 seconds. Without it, if the MQ timeout implementation hangs, the test would block CI indefinitely. The 40-second ceiling gives the 30-second MQ timeout room to fire while preventing failure.

- **@Tag("integration") + Maven profile isolation prevents CI failures during development.** All 12 UC4 integration tests are tagged @Tag("integration") and excluded from the default Maven test phase. This allows the tests to run on-demand (mvn test -Dgroups=integration) once the docker stack is ready, without breaking CI while implementation is in progress.

- **API contract clarity reduces rework.** The UC4TestEvidence.md form documents request/response JSON, HTTP status codes, and error cases upfront. Integration agents and QA both sign off on the contract before implementation. This eliminates "works on my machine" surprises and ensures tests match the actual API behavior.
