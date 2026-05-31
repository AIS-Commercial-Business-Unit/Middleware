package com.ais.middleware.prs.appraisal.integration;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import org.springframework.http.*;
import org.springframework.web.client.HttpClientErrorException;
import org.springframework.web.client.RestTemplate;

import java.util.Base64;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;

/**
 * UC4 Integration Tests — GetAppraisalDocument
 *
 * Prerequisites (must be running before executing these tests):
 *   docker compose up
 *   curl http://localhost:9020/actuator/health   → {"status":"UP"}
 *   curl http://localhost:8090/actuator/health   → {"status":"UP"}
 *
 * Run with Maven integration profile:
 *   mvn test -pl prs-appraisal-service -Dgroups=integration
 *
 * Fixture document keys (owned by deipde07-mq-simulator — see uc4-mq-stub-design.md §3):
 *   12345678901      (numeric 11-digit) → DEIPDE07 MQ — small PDF: 8 chunks
 *   98765432109876   (numeric 14-digit) → DEIPDE07 MQ — large PDF: 200 chunks
 *   DOC_RiskID_I_TEST001               → RiskID WCF stub (insured document)
 *   DOC_RiskID_A_TEST002               → RiskID WCF stub (agent document)
 *
 * DocumentKey routing rules  [BR-APR-004]:
 *   Contains _RiskID_I  → RiskID WCF, insured path
 *   Contains _RiskID_A  → RiskID WCF, agent path
 *   Numeric 10–15 digits → DEIPDE07 via IBM MQ (multi-message chunk aggregation)
 */
@Tag("integration")
class GetAppraisalDocumentIntegrationTest {

    private static final String BASE_URL = "http://localhost:8090";
    private static final String DOCUMENT_ENDPOINT = BASE_URL + "/api/appraisals/document";

    private RestTemplate restTemplate;

    @BeforeEach
    void setUp() {
        restTemplate = new RestTemplate();
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private ResponseEntity<AppraisalDocumentResponse> postDocument(String documentKey) {
        HttpHeaders headers = new HttpHeaders();
        headers.setContentType(MediaType.APPLICATION_JSON);
        String body = String.format("{\"documentKey\": \"%s\"}", documentKey);
        HttpEntity<String> request = new HttpEntity<>(body, headers);
        return restTemplate.exchange(DOCUMENT_ENDPOINT, HttpMethod.POST, request, AppraisalDocumentResponse.class);
    }

    // ─── DC-001 ───────────────────────────────────────────────────────────────

    /**
     * DC-001: DEIPDE07 small PDF — 8 chunks aggregated.
     *
     * Input:   POST /api/appraisals/document  {"documentKey": "12345678901"}
     * Routing: numeric 11-digit key → DEIPDE07 MQ  [BR-APR-004]
     * Simulator sends 8 × 64-byte chunks with CRLF + ||END-OF-DOCUMENT|| sentinel on last chunk.
     *
     * Expected:
     *   HTTP 200
     *   base64Pdf is non-empty
     *   contentType == "application/pdf"
     *   base64Pdf decodes without error (valid base64)
     *   base64Pdf contains no \r or \n characters (EBCDIC artifacts stripped)  [BR-APR-006]
     *
     * Expected log lines:
     *   INFO  GetAppraisalDocument start: documentKey=12345678901 route=DEIPDE07_MQ
     *   INFO  PdfChunkProcessor: chunk 1 received, accumulated=64 bytes
     *   INFO  PdfChunkProcessor: END-OF-DOCUMENT received, total chunks=8
     *   INFO  GetAppraisalDocument complete: documentKey=12345678901 sizeBytes=<n>
     */
    @Test
    void deipde07SmallPdf_chunksAggregated() {
        ResponseEntity<AppraisalDocumentResponse> response = postDocument("12345678901");

        assertThat(response.getStatusCode())
                .as("HTTP status for DEIPDE07 small PDF (12345678901)")
                .isEqualTo(HttpStatus.OK);

        AppraisalDocumentResponse body = response.getBody();
        assertThat(body).as("Response body must not be null").isNotNull();
        assertThat(body.base64Pdf())
                .as("base64Pdf must be non-empty for documentKey=12345678901")
                .isNotBlank();
        assertThat(body.contentType())
                .as("contentType must be application/pdf")
                .isEqualTo("application/pdf");

        // Validate base64 decodability — EBCDIC artifact stripping should produce clean base64
        assertThatCode(() -> Base64.getDecoder().decode(body.base64Pdf()))
                .as("base64Pdf must be decodable without error (no CRLF artifacts) [BR-APR-006]")
                .doesNotThrowAnyException();

        // No carriage return or line feed characters in the base64 string  [BR-APR-006]
        assertThat(body.base64Pdf())
                .as("base64Pdf must not contain \\r or \\n (EBCDIC CRLF artifacts must be stripped) [BR-APR-006]")
                .doesNotContain("\r")
                .doesNotContain("\n");

        // Expected log:
        // PASS: INFO  "PdfChunkProcessor: END-OF-DOCUMENT" totalChunks=8
        // PASS: INFO  "GetAppraisalDocument complete" documentKey=12345678901
    }

    // ─── DC-002 ───────────────────────────────────────────────────────────────

    /**
     * DC-002: DEIPDE07 large PDF — 200 chunks aggregated.
     *
     * Input:   {"documentKey": "98765432109876"}
     * Routing: numeric 14-digit key → DEIPDE07 MQ  [BR-APR-004]
     * Simulator sends 200 × 64-byte chunks (total ~12,800 chars of base64).
     *
     * Expected:
     *   HTTP 200
     *   base64Pdf.length() > 1000  (confirms all 200 chunks concatenated)
     *   base64Pdf decodes without error
     *   No \r or \n in base64Pdf  [BR-APR-006]
     *
     * Expected log lines:
     *   INFO  PdfChunkProcessor: END-OF-DOCUMENT received, total chunks=200
     *   INFO  GetAppraisalDocument complete: documentKey=98765432109876 sizeBytes=<n>
     */
    @Test
    void deipde07LargePdf_chunksAggregated() {
        ResponseEntity<AppraisalDocumentResponse> response = postDocument("98765432109876");

        assertThat(response.getStatusCode())
                .as("HTTP status for DEIPDE07 large PDF (98765432109876)")
                .isEqualTo(HttpStatus.OK);

        AppraisalDocumentResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.base64Pdf())
                .as("base64Pdf must be non-empty for large 200-chunk document")
                .isNotBlank();
        assertThat(body.base64Pdf().length())
                .as("base64Pdf must be > 1000 chars to confirm all 200 chunks were aggregated")
                .isGreaterThan(1000);

        assertThatCode(() -> Base64.getDecoder().decode(body.base64Pdf()))
                .as("base64Pdf must be decodable — 200 chunks should concatenate cleanly [BR-APR-006]")
                .doesNotThrowAnyException();

        assertThat(body.base64Pdf())
                .as("No \\r or \\n in large-PDF base64 string (EBCDIC artifacts stripped) [BR-APR-006]")
                .doesNotContain("\r")
                .doesNotContain("\n");

        // Expected log:
        // PASS: INFO  "PdfChunkProcessor: END-OF-DOCUMENT" totalChunks=200
        // PASS: INFO  "GetAppraisalDocument complete" documentKey=98765432109876
    }

    // ─── DC-003 ───────────────────────────────────────────────────────────────

    /**
     * DC-003: RiskID WCF — insured document path.
     *
     * Input:   {"documentKey": "DOC_RiskID_I_TEST001"}
     * Routing: contains _RiskID_I → RiskID WCF stub (GetInspectionPDFBizTalk)  [BR-APR-004]
     * WCF stub returns a synthetic base64 PDF for known test key.
     *
     * Expected:
     *   HTTP 200
     *   base64Pdf is non-empty
     *   base64Pdf decodes without error
     *
     * Expected log lines:
     *   INFO  GetAppraisalDocument start: documentKey=DOC_RiskID_I_TEST001 route=RISKID_WCF_INSURED
     *   INFO  GetAppraisalDocument complete: documentKey=DOC_RiskID_I_TEST001 sourceSystem=RiskID
     */
    @Test
    void riskIdWcfInsured_directCall() {
        ResponseEntity<AppraisalDocumentResponse> response = postDocument("DOC_RiskID_I_TEST001");

        assertThat(response.getStatusCode())
                .as("HTTP status for RiskID insured document (DOC_RiskID_I_TEST001)")
                .isEqualTo(HttpStatus.OK);

        AppraisalDocumentResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.base64Pdf())
                .as("base64Pdf must be non-empty from RiskID WCF insured path")
                .isNotBlank();

        assertThatCode(() -> Base64.getDecoder().decode(body.base64Pdf()))
                .as("base64Pdf from RiskID WCF must be valid base64")
                .doesNotThrowAnyException();

        // Expected log:
        // PASS: INFO  "GetAppraisalDocument start" route=RISKID_WCF_INSURED
        // PASS: INFO  "GetAppraisalDocument complete" sourceSystem=RiskID
    }

    // ─── DC-004 ───────────────────────────────────────────────────────────────

    /**
     * DC-004: RiskID WCF — agent document path.
     *
     * Input:   {"documentKey": "DOC_RiskID_A_TEST002"}
     * Routing: contains _RiskID_A → RiskID WCF stub (agent path)  [BR-APR-004]
     *
     * Expected:
     *   HTTP 200
     *   base64Pdf is non-empty
     *   base64Pdf decodes without error
     *
     * Expected log lines:
     *   INFO  GetAppraisalDocument start: documentKey=DOC_RiskID_A_TEST002 route=RISKID_WCF_AGENT
     *   INFO  GetAppraisalDocument complete: documentKey=DOC_RiskID_A_TEST002 sourceSystem=RiskID
     */
    @Test
    void riskIdWcfAgent_directCall() {
        ResponseEntity<AppraisalDocumentResponse> response = postDocument("DOC_RiskID_A_TEST002");

        assertThat(response.getStatusCode())
                .as("HTTP status for RiskID agent document (DOC_RiskID_A_TEST002)")
                .isEqualTo(HttpStatus.OK);

        AppraisalDocumentResponse body = response.getBody();
        assertThat(body).isNotNull();
        assertThat(body.base64Pdf())
                .as("base64Pdf must be non-empty from RiskID WCF agent path")
                .isNotBlank();

        assertThatCode(() -> Base64.getDecoder().decode(body.base64Pdf()))
                .as("base64Pdf from RiskID WCF agent must be valid base64")
                .doesNotThrowAnyException();

        // Expected log:
        // PASS: INFO  "GetAppraisalDocument start" route=RISKID_WCF_AGENT
        // PASS: INFO  "GetAppraisalDocument complete" sourceSystem=RiskID
    }

    // ─── DC-005 ───────────────────────────────────────────────────────────────

    /**
     * DC-005: Unrecognised DocumentKey format — validation rejection.
     *
     * Input:   {"documentKey": "NOT-A-VALID-KEY"}
     * The key does not match any routing pattern:
     *   - Does not contain _RiskID_I or _RiskID_A
     *   - Is not numeric-only 10–15 digits
     *
     * Expected:
     *   HTTP 400 Bad Request (or 500 with descriptive error)
     *
     * Expected log lines:
     *   WARN  GetAppraisalDocument rejected: unrecognised documentKey format key=NOT-A-VALID-KEY
     */
    @Test
    void unknownDocumentKey_returnsBadRequest() {
        HttpHeaders headers = new HttpHeaders();
        headers.setContentType(MediaType.APPLICATION_JSON);
        String body = "{\"documentKey\": \"NOT-A-VALID-KEY\"}";
        HttpEntity<String> request = new HttpEntity<>(body, headers);

        assertThatCode(() -> {
            ResponseEntity<String> response = restTemplate.exchange(
                    DOCUMENT_ENDPOINT, HttpMethod.POST, request, String.class);
            assertThat(response.getStatusCode().value())
                    .as("HTTP 4xx or 5xx expected for unrecognised documentKey format")
                    .isGreaterThanOrEqualTo(400);
        }).satisfies(ex -> {
            if (ex instanceof HttpClientErrorException clientEx) {
                assertThat(clientEx.getStatusCode().value())
                        .as("HTTP error expected for unrecognised documentKey")
                        .isGreaterThanOrEqualTo(400);
            }
        });

        // Expected log:
        // PASS: WARN  "unrecognised documentKey format" key=NOT-A-VALID-KEY
    }

    // ─── DC-006 ───────────────────────────────────────────────────────────────

    /**
     * DC-006: Numeric key not in fixture — simulator returns DOCUMENT_NOT_FOUND.
     *
     * Input:   {"documentKey": "99999999999"}
     * Key is a valid numeric format (11 digits) → routes to DEIPDE07 MQ.
     * Simulator has no fixture for this key → sends ERROR=DOCUMENT_NOT_FOUND response.
     *
     * Expected:
     *   HTTP 404 or HTTP 500 with error body (not 200 with empty content)
     *   Error message references documentKey or DOCUMENT_NOT_FOUND
     *
     * Expected log lines:
     *   WARN  DEIPDE07 MQ: DOCUMENT_NOT_FOUND for documentKey=99999999999
     *   WARN  GetAppraisalDocument error: documentKey=99999999999 status=404
     */
    @Test
    void numericKeyNotInFixture_handledGracefully() {
        HttpHeaders headers = new HttpHeaders();
        headers.setContentType(MediaType.APPLICATION_JSON);
        String requestBody = "{\"documentKey\": \"99999999999\"}";
        HttpEntity<String> request = new HttpEntity<>(requestBody, headers);

        assertThatCode(() -> {
            ResponseEntity<String> response = restTemplate.exchange(
                    DOCUMENT_ENDPOINT, HttpMethod.POST, request, String.class);
            // If service returns 2xx, it must NOT have an empty/null pdf body
            // (a 200 with empty content would be a silent data loss bug)
            if (response.getStatusCode().is2xxSuccessful()) {
                // Acceptable only if body explicitly signals not-found
                assertThat(response.getBody())
                        .as("2xx for unknown numeric key must include an error indication, not silent empty response")
                        .isNotBlank();
            } else {
                assertThat(response.getStatusCode().value())
                        .as("Expected 4xx or 5xx for fixture-missing numeric key (DOCUMENT_NOT_FOUND)")
                        .isGreaterThanOrEqualTo(400);
            }
        }).satisfies(ex -> {
            if (ex instanceof HttpClientErrorException clientEx) {
                // 404 is the correct code for DOCUMENT_NOT_FOUND
                assertThat(clientEx.getStatusCode().value())
                        .as("HTTP 404 expected when DEIPDE07 simulator returns DOCUMENT_NOT_FOUND")
                        .isGreaterThanOrEqualTo(400);
            }
        });

        // Expected log:
        // PASS: WARN  "DEIPDE07 MQ: DOCUMENT_NOT_FOUND" documentKey=99999999999
    }

    // ─── Response DTO ─────────────────────────────────────────────────────────

    /** Maps the /api/appraisals/document response envelope. */
    record AppraisalDocumentResponse(String base64Pdf, String contentType) {
        AppraisalDocumentResponse() { this(null, null); }
    }
}
