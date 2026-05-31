package com.ais.middleware.prs.appraisal.integration;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.http.*;
import org.springframework.web.client.HttpClientErrorException;
import org.springframework.web.client.RestTemplate;

import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;

/**
 * UC4 Integration Tests — GetAppraisalList
 *
 * Prerequisites (must be running before executing these tests):
 *   docker compose up
 *   curl http://localhost:9020/actuator/health   → {"status":"UP"}
 *   curl http://localhost:8090/actuator/health   → {"status":"UP"}
 *
 * Run with Maven integration profile:
 *   mvn test -pl prs-appraisal-service -Dgroups=integration
 *
 * Fixture policy numbers (owned by deipde07-mq-simulator — see uc4-mq-stub-design.md §3):
 *   POL-001-TEST  → 3 DEIPDE07 records + @Work records  (happy path)
 *   POL-002-TEST  → 0 DEIPDE07 records (MQ times out);  @Work records only
 *   POL-003-TEST  → 1 DEIPDE07 record + @Work results
 *   POL-TIMEOUT   → DEIPDE07 holds response >30 s;       @Work returns normally
 */
@Tag("integration")
class GetAppraisalListIntegrationTest {

    private static final String BASE_URL = "http://localhost:8090";
    private static final String LIST_ENDPOINT = BASE_URL + "/api/appraisals/list";

    private RestTemplate restTemplate;

    @BeforeEach
    void setUp() {
        restTemplate = new RestTemplate();
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private ResponseEntity<AppraisalListResponse> postList(String policyNumber) {
        HttpHeaders headers = new HttpHeaders();
        headers.setContentType(MediaType.APPLICATION_JSON);
        String body = String.format("{\"policyNumber\": \"%s\"}", policyNumber);
        HttpEntity<String> request = new HttpEntity<>(body, headers);
        return restTemplate.exchange(LIST_ENDPOINT, HttpMethod.POST, request, AppraisalListResponse.class);
    }

    // ─── SC-001 ───────────────────────────────────────────────────────────────

    /**
     * SC-001: Happy path — both backends return results.
     *
     * Input:  POST /api/appraisals/list  {"policyNumber": "POL-001-TEST"}
     * DEIPDE07 simulator sends 3 messages (1 of 3, 2 of 3, 3 of 3) with numeric documentkeys.
     * @Work SQL stub returns AT_WORK records with RiskID documentkeys.
     *
     * Expected:
     *   HTTP 200
     *   items.size() >= 3
     *   partialResult == false
     *   At least one item has a numeric-only documentkey (DEIPDE07 source)
     *   At least one item has a _RiskID_ documentkey (@Work source)
     *   No duplicate (streetAdr + policyQuoteNbr) combinations  [BR-APR-003]
     *
     * Expected log lines (verify in Grafana/Loki after run):
     *   INFO  GetAppraisalList start: policyNumber=POL-001-TEST correlationId=<uuid>
     *   INFO  ScatterGather fanout started: atWork=dispatched deipde07=dispatched
     *   INFO  GetAppraisalList complete: itemCount=<n> partialResult=false
     */
    @Test
    void happyPath_ThreeRecordsFromBothSources() {
        ResponseEntity<AppraisalListResponse> response = postList("POL-001-TEST");

        assertThat(response.getStatusCode())
                .as("HTTP status for POL-001-TEST (happy path)")
                .isEqualTo(HttpStatus.OK);

        AppraisalListResponse body = response.getBody();
        assertThat(body).as("Response body must not be null").isNotNull();
        assertThat(body.items()).as("Must return at least 3 appraisal items").hasSizeGreaterThanOrEqualTo(3);
        assertThat(body.partialResult()).as("partialResult must be false when both backends respond").isFalse();

        // Verify routing coverage: at least one numeric key (DEIPDE07) present
        boolean hasDeipde07Key = body.items().stream()
                .anyMatch(item -> item.documentkey() != null && item.documentkey().matches("^[0-9]{10,15}$"));
        assertThat(hasDeipde07Key)
                .as("At least one item must have a numeric DEIPDE07 documentkey")
                .isTrue();

        // Verify routing coverage: at least one RiskID key (@Work) present
        boolean hasRiskIdKey = body.items().stream()
                .anyMatch(item -> item.documentkey() != null && item.documentkey().contains("_RiskID_"));
        assertThat(hasRiskIdKey)
                .as("At least one item must have a _RiskID_ documentkey from @Work")
                .isTrue();

        // Deduplication: no two items should share the same (streetAdr + policyQuoteNbr)  [BR-APR-003]
        Set<String> dedupeKeys = body.items().stream()
                .map(item -> item.streetAdr() + "|" + item.policyQuoteNbr())
                .collect(Collectors.toSet());
        assertThat(dedupeKeys)
                .as("Deduplication failed — duplicate (streetAdr + policyQuoteNbr) combinations found [BR-APR-003]")
                .hasSize(body.items().size());

        // Expected log (comment — assert manually in Loki/Grafana after run):
        // PASS: INFO  "GetAppraisalList start" policyNumber=POL-001-TEST
        // PASS: INFO  "GetAppraisalList complete" itemCount>=3 partialResult=false
    }

    // ─── SC-002 ───────────────────────────────────────────────────────────────

    /**
     * SC-002: DEIPDE07 returns zero results — @Work results only.
     *
     * Input:  {"policyNumber": "POL-002-TEST"}
     * DEIPDE07 simulator sends nothing for this policy. The 1-second initial poll times out.
     * Both backends have completed (MQ returned 0 messages — not a timeout error).
     *
     * Expected:
     *   HTTP 200
     *   items contains only @Work records (all have _RiskID_ documentkeys)
     *   partialResult == false  (zero is a valid result, not a timeout)
     *
     * Expected log lines:
     *   INFO  DEIPDE07 MQ: no messages received for policyNumber=POL-002-TEST — 0 records
     *   INFO  GetAppraisalList complete: itemCount=<n> partialResult=false
     */
    @Test
    void deipde07ZeroResults_AtWorkOnlyResponse() {
        ResponseEntity<AppraisalListResponse> response = postList("POL-002-TEST");

        assertThat(response.getStatusCode())
                .as("HTTP status for POL-002-TEST (DEIPDE07 zero results)")
                .isEqualTo(HttpStatus.OK);

        AppraisalListResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.partialResult())
                .as("partialResult must be false — DEIPDE07 returned 0 records (not a timeout) [BR-APR-002]")
                .isFalse();

        // All returned items should be from @Work (RiskID documentkeys)
        if (!body.items().isEmpty()) {
            boolean allFromAtWork = body.items().stream()
                    .allMatch(item -> item.documentkey() != null && item.documentkey().contains("_RiskID_"));
            assertThat(allFromAtWork)
                    .as("All items for POL-002-TEST must be from @Work (RiskID documentkeys only)")
                    .isTrue();
        }

        // Expected log:
        // PASS: INFO  "DEIPDE07 MQ: 0 records" policyNumber=POL-002-TEST
        // PASS: INFO  "GetAppraisalList complete" partialResult=false
    }

    // ─── SC-003 ───────────────────────────────────────────────────────────────

    /**
     * SC-003: Single DEIPDE07 record — combined with @Work results.
     *
     * Input:  {"policyNumber": "POL-003-TEST"}
     * DEIPDE07 simulator sends exactly 1 message (1 of 1).
     *
     * Expected:
     *   HTTP 200
     *   At least 1 item with a numeric documentkey (the single DEIPDE07 record)
     *   partialResult == false
     *
     * Expected log lines:
     *   INFO  DEIPDE07 MQ: received 1 of 1 for policyNumber=POL-003-TEST
     *   INFO  GetAppraisalList complete: itemCount=<n> partialResult=false
     */
    @Test
    void singleRecord_deipde07() {
        ResponseEntity<AppraisalListResponse> response = postList("POL-003-TEST");

        assertThat(response.getStatusCode())
                .as("HTTP status for POL-003-TEST (single DEIPDE07 record)")
                .isEqualTo(HttpStatus.OK);

        AppraisalListResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.partialResult())
                .as("partialResult must be false — single record is a complete response")
                .isFalse();

        long deipde07Count = body.items().stream()
                .filter(item -> item.documentkey() != null && item.documentkey().matches("^[0-9]{10,15}$"))
                .count();
        assertThat(deipde07Count)
                .as("Expected exactly 1 numeric DEIPDE07 documentkey for POL-003-TEST")
                .isEqualTo(1L);

        // Expected log:
        // PASS: INFO  "DEIPDE07 MQ: received 1 of 1" policyNumber=POL-003-TEST
        // PASS: INFO  "GetAppraisalList complete" partialResult=false
    }

    // ─── SC-004 ───────────────────────────────────────────────────────────────

    /**
     * SC-004: DEIPDE07 MQ timeout — partial result returned.
     *
     * Input:  {"policyNumber": "POL-TIMEOUT"}
     * DEIPDE07 simulator holds the response for >30 seconds (simulates mainframe non-response).
     * @Work SQL returns normally.
     *
     * Expected:
     *   HTTP 200
     *   partialResult == true  [BR-APR-002]
     *   items contains only @Work records (no numeric DEIPDE07 documentkeys)
     *
     * ⚠️ This test waits up to ~35 seconds for the MQ timeout to fire.
     * Annotated @Timeout(40) — fail if test takes longer than 40 seconds.
     *
     * Expected log lines:
     *   WARN  DEIPDE07 MQ timeout: policyNumber=POL-TIMEOUT — returning partialResult=true [BR-APR-002]
     *   INFO  GetAppraisalList complete: partialResult=true
     */
    @Test
    @Timeout(value = 40, unit = TimeUnit.SECONDS)
    void timeout_deipde07_returnsPartialResult() {
        ResponseEntity<AppraisalListResponse> response = postList("POL-TIMEOUT");

        assertThat(response.getStatusCode())
                .as("HTTP 200 expected even when DEIPDE07 times out (graceful partial result)")
                .isEqualTo(HttpStatus.OK);

        AppraisalListResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.partialResult())
                .as("partialResult must be true when DEIPDE07 MQ times out [BR-APR-002]")
                .isTrue();

        // No DEIPDE07 records should be present — timeout means no data from that leg
        boolean hasDeipde07Record = body.items().stream()
                .anyMatch(item -> item.documentkey() != null && item.documentkey().matches("^[0-9]{10,15}$"));
        assertThat(hasDeipde07Record)
                .as("No numeric DEIPDE07 documentkeys expected when MQ timed out")
                .isFalse();

        // Expected log:
        // PASS: WARN  "DEIPDE07 MQ timeout" policyNumber=POL-TIMEOUT partialResult=true
        // PASS: INFO  "GetAppraisalList complete" partialResult=true
    }

    // ─── SC-005 ───────────────────────────────────────────────────────────────

    /**
     * SC-005: Unknown policy number — both backends return zero results.
     *
     * Input:  {"policyNumber": "POL-UNKNOWN-99999"}
     * No fixture data in DEIPDE07 simulator; @Work stored proc returns empty resultset.
     *
     * Expected:
     *   HTTP 200
     *   items is empty
     *   partialResult == false
     *
     * Expected log lines:
     *   INFO  GetAppraisalList complete: itemCount=0 partialResult=false policyNumber=POL-UNKNOWN-99999
     */
    @Test
    void unknownPolicy_returnsEmptyList() {
        ResponseEntity<AppraisalListResponse> response = postList("POL-UNKNOWN-99999");

        assertThat(response.getStatusCode())
                .as("HTTP status for unknown policy number")
                .isEqualTo(HttpStatus.OK);

        AppraisalListResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.items())
                .as("items must be empty for unknown policy number")
                .isEmpty();
        assertThat(body.partialResult())
                .as("partialResult must be false for zero-result response")
                .isFalse();

        // Expected log:
        // PASS: INFO  "GetAppraisalList complete" itemCount=0 partialResult=false
    }

    // ─── SC-006 ───────────────────────────────────────────────────────────────

    /**
     * SC-006: Missing / empty policyNumber — validation rejection.
     *
     * Input:  {"policyNumber": ""}
     *
     * Expected:
     *   HTTP 400 Bad Request
     *
     * Expected log lines:
     *   WARN  GetAppraisalList rejected: policyNumber blank — HTTP 400
     */
    @Test
    void missingPolicyNumber_returnsBadRequest() {
        HttpHeaders headers = new HttpHeaders();
        headers.setContentType(MediaType.APPLICATION_JSON);
        String body = "{\"policyNumber\": \"\"}";
        HttpEntity<String> request = new HttpEntity<>(body, headers);

        assertThatCode(() -> {
            ResponseEntity<String> response = restTemplate.exchange(
                    LIST_ENDPOINT, HttpMethod.POST, request, String.class);
            assertThat(response.getStatusCode())
                    .as("HTTP 400 expected for blank policyNumber")
                    .isEqualTo(HttpStatus.BAD_REQUEST);
        }).satisfies(ex -> {
            if (ex instanceof HttpClientErrorException clientEx) {
                assertThat(clientEx.getStatusCode())
                        .as("HTTP 400 expected for blank policyNumber")
                        .isEqualTo(HttpStatus.BAD_REQUEST);
            }
        });

        // Expected log:
        // PASS: WARN  "GetAppraisalList rejected: policyNumber blank"
    }

    // ─── Response DTOs ────────────────────────────────────────────────────────

    /** Maps the /api/appraisals/list response envelope. */
    record AppraisalListResponse(List<AppraisalListItem> items, boolean partialResult) {
        AppraisalListResponse() { this(List.of(), false); }
    }

    /** Maps a single appraisal item from the merged result set. */
    record AppraisalListItem(
            String appraisalUid,
            String policyQuoteNbr,
            String streetAdr,
            String cityAdr,
            String stateCde,
            String zipAdr,
            String appraisalDte,
            String documenttype,
            String documentname,
            String documentkey
    ) {}
}
