# Apache Camel vs NServiceBus: Testability Comparison

## NServiceBus (C#/.NET)
- **Unit testing: FULL support** via `NServiceBus.Testing` package
- `TestableMessageHandlerContext` provides in-memory context for sagas and handlers
- Can assert: sent messages, published events, saga state, timeouts
- No external infrastructure needed for unit tests
- Tests run in milliseconds
- Pattern: `saga.Handle(command, context)` → assert `context.SentMessages`

## Apache Camel (Java)
- **Unit testing: LIMITED** — routes are integration points, not units
- `CamelTestSupport` provides in-memory Camel context but requires full route wiring
- `RouteBuilder` logic (routing, transformation) can be tested but is complex to mock
- Kafka endpoints in tests typically require a `MockEndpoint` or embedded Kafka
- True "unit" isolation is difficult because routes depend on the Camel context

## Recommendation
- For pure logic (transformations, domain rules): Extract into separate classes, test those
- For route behavior: Use `@SpringBootTest` integration tests with `MockEndpoint`
- See Java team backlog: Add `CamelTestSupport`-based tests for critical routes

## Verdict
NServiceBus has significantly better unit test ergonomics for saga/handler logic.
Apache Camel routes can be tested but require more infrastructure setup.
