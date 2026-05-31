# Skill: Verification Gates for Completion

**Domain:** Quality Assurance, Definition of Done  
**Confidence:** medium  
**Last verified:** 2026-05-29  
**Established by:** Coordinator (after 404 gap in demo-control delivery)

## Pattern

Work is NOT complete until it has been verified in its runtime environment. Creating files or passing builds is necessary but not sufficient.

## Verification Requirements by Work Type

### Frontend (UI Routes, Pages, Components)
1. Files created ✅
2. TypeScript compiles (`npx tsc --noEmit`) ✅
3. Build succeeds (`npm run build`) ✅
4. Container rebuilt (`docker compose up -d --build {service}`) ✅
5. **Route verified in browser** (`http://localhost:{port}/{route}`) ✅ ← CRITICAL
6. Screenshot or curl output as evidence

### Backend (APIs, Services)
1. Code written ✅
2. Compiles/builds ✅
3. Container rebuilt and healthy ✅
4. **Endpoint responds correctly** (curl or Postman) ✅ ← CRITICAL
5. Log entries show expected behavior
6. curl output as evidence

### Database/Schema Changes
1. Migration script written ✅
2. Applied to local DB ✅
3. **Query verification** (SELECT confirms schema) ✅ ← CRITICAL
4. Services still start and connect

### Integration/Gateway Stubs
1. Code written ✅
2. Compiles ✅
3. **Log entries fire during flow** ✅ ← CRITICAL
4. Latency/delay visible in timestamps

## Evidence Format

Always provide evidence of runtime verification:

```
✅ Verification complete:
   - Route: http://localhost:3000/demo-control
   - Status: 200 OK
   - Response time: 145ms
   - Screenshot: [describes what was visible]
```

or

```
✅ Verification complete:
   - Endpoint: POST http://localhost:8084/api/demo/reset
   - Status: 200 OK
   - Response: {"status":"success","cleared":157,"seeded":5}
```

## When Verification Fails

If runtime verification fails:
1. DO NOT declare done
2. Investigate immediately (logs, build output, container health)
3. Fix and re-verify
4. Document what was wrong in your history.md

## Why This Matters

The `/demo-control` 404 incident showed that file creation + build success ≠ working feature. The gap was caught by the user, not the team. This skill prevents that class of failure.

## Applies To

- Frontend
- Integration (every agent doing deliverable work)
- Scribe does NOT need runtime verification (file ops are self-verifying)
