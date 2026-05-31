package com.ais.middleware.platform.fileprocessing.routes;

import org.springframework.stereotype.Component;

/**
 * Deterministic failure injection for batch processing demo.
 * Simulates ~7% failure rate (2 out of every 28 records):
 *   - sequenceNumber % 28 == 1  → Business failure (~3.57%)
 *   - sequenceNumber % 28 == 15 → Technical failure (~3.57%)
 *
 * Using sequence number ensures failures are reproducible and spread evenly across a batch.
 * Business failures are non-retryable (policy eligibility, account status).
 * Technical failures are retryable (transient infrastructure issues).
 */
@Component
public class FailureSimulator {

    public record FailureResult(boolean shouldFail, String reason, String category) {
        public static FailureResult none() {
            return new FailureResult(false, null, null);
        }
    }

    /**
     * Evaluate whether a batch record should be pre-failed before dispatch.
     *
     * @param sequenceNumber  1-based row number in the batch file
     * @param policyTypeCode  from CSV column PolicyTypeCode
     * @param accountId       from CSV column AccountId
     * @return FailureResult indicating whether/how to fail the record
     */
    public FailureResult evaluate(int sequenceNumber, int policyTypeCode, String accountId) {

        // Business failure: every 28th record starting at position 1
        if (sequenceNumber % 28 == 1) {
            if (policyTypeCode == 10) {
                return new FailureResult(true,
                        "Policy type 10 (Specialty Lines) requires manual underwriting review: " +
                        "not eligible for automated renewal",
                        "Business");
            }
            return new FailureResult(true,
                    "Account flagged for collections review: automated renewal suspended " +
                    "pending account resolution for account " + accountId,
                    "Business");
        }

        // Technical failure: every 28th record starting at position 15
        if (sequenceNumber % 28 == 15) {
            // Alternate between two technical failure modes for variety
            if ((sequenceNumber / 28) % 2 == 0) {
                return new FailureResult(true,
                        "PAS system connection timeout after 3 retries: " +
                        "DuckCreek service unavailable (HTTP 503)",
                        "Technical");
            }
            return new FailureResult(true,
                    "Kafka producer send timeout: message delivery unconfirmed within 30s " +
                    "(broker partition leader election in progress)",
                    "Technical");
        }

        return FailureResult.none();
    }
}
