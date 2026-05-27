# Session Log: Three Bugs Fixed

**Date:** 2026-05-27T15:25:10Z  
**Agents:** dotnet-3, qa-1, dotnet-4, frontend-3  
**Session Type:** Cross-agent orchestration & bug fixes  

## Summary

Scribe merged 4 agent decisions into unified log. Three critical bugs resolved:

1. **.NET Kafka Serialization** (dotnet-4): Fixed JSON field naming mismatch (PascalCase → camelCase)
   - Batch demo now completes with processedRecords tracked correctly

2. **NServiceBus Startup Ordering** (dotnet-3): Operational fix for `dotnet-platform-integration` container
   - Prevented stuck issuances; recovered stranded error queue messages

3. **Flow Diagram Visualization** (frontend-3): Fixed stale image, dedup logic, mappings, and health checks
   - Platform-UI now serving live Loki-backed EDA flow events

## Decisions Archived

4 inbox files merged into `.squad/decisions/decisions.md`:
- `dotnet-kafka-camelcase.md` → unified cross-stack serialization convention
- `dotnet-stuck-pasgateway.md` → documented structural startup gap
- `frontend-flow-dynamic.md` → established Loki flow visualization rules
- `qa-batches-diagnosis.md` → diagnostic foundation for dotnet-4 fix

## Team Readiness

All three agents' histories updated with cross-team learnings. Conventions documented for:
- JSON serialization: centralized `JsonNamingPolicy.CamelCase` in KafkaBridgeRuntime
- NServiceBus startup: queue infrastructure critical dependency
- Loki event dedup: `messageType|from|to` (no direction)
