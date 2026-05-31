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

### 2026-05-31T16:58:30.069-04:00 — UC4 document flow test evidence + EDA_FLOW verification

- **Existing Java UC4 integration tests are legacy-path coverage, not primary-path coverage.** `GetAppraisalListIntegrationTest` and `GetAppraisalDocumentIntegrationTest` still exercise the older Java POST endpoints, while the current `dotnet-prs-appraisal` primary path exposes GET endpoints in `AppraisalDocumentsController`. They remain useful for legacy behavior evidence, but they do not validate the active .NET UC4 implementation directly.

- **There is no dedicated .NET test project for `dotnet-prs-appraisal`.** The only discovered .NET test project is `dotnet/tests/Middleware.Tests.csproj`, which targets `dotnet-policy-issuance`; its 6 NUnit tests passed, but there is no appraisal-documents test suite for the .NET primary path.

- **Current UC4 `EDA_FLOW` logging does not satisfy the frontend Loki parser contract.** `dotnet-prs-appraisal/Behaviors/EDAFlowBehavior.cs` populates `EDA_IssuanceId` from `AppraisalId`, but the UC4 document commands/events are correlated by `RequestId` / `CorrelationId`. Combined with `Program.cs` using `WriteTo.Console()` with no JSON formatter, the current logs are not shaped for `platform-ui/src/app/api/policies/[issuanceId]/flow/route.ts` to parse and filter reliably.

- **Build/test evidence on this date:** `dotnet build dotnet\\dotnet-prs-appraisal\\dotnet-prs-appraisal.csproj` succeeded, `dotnet test dotnet\\tests\\Middleware.Tests.csproj` passed 6/6, and the Java Maven test run could not execute because `mvn` is unavailable in this environment. Evidence captured in `.docs/demo/uc4-integration-test-evidence.md`.

### 2026-05-31T16:58:30.069Z — UC4 observability gaps + Decisions merge

- **Decision #46 (UC4 observability gaps — test coverage and EDAFlowBehavior fallback) merged into squad/decisions.md.** This formalizes the team's acknowledgment of three release-significant gaps: missing .NET test project, missing correlation fallback, and missing JSON logging.

- **Gap acceptance pattern:** Document gaps in decisions/recommendations before claiming a feature is complete. This prevents future "we thought it was done" surprises and creates an explicit action list for the responsible agent.

- **Observable evidence captured:** UC4 test evidence, correlation requirement analysis, and logging contract mismatch all recorded in `.docs/demo/uc4-integration-test-evidence.md` and reinforced in Decision #46.
