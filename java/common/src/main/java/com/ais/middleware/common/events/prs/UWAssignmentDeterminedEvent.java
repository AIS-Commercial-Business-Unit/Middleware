package com.ais.middleware.common.events.prs;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published after UW assignment is determined (UA or UST).
 * Topic: prs.events.uw-assignment-determined
 *
 * ⚠️ DEMO GAP: Assignment logic inferred from inspectionTypeCode only.
 * Real determination rules (rule codes, producer control codes) needed from PRS team.
 * Suspense days: UA → 45 days, UST → 14 days (ASSUMED — needs PRS validation).
 */
public record UWAssignmentDeterminedEvent(
        String correlationId,
        // ⚠️ DEMO GAP: Possible values and routing rules need PRS team input
        String uwAssignment,       // "UA" or "UST"
        int suspenseDays,          // 45 for UA, 14 for UST — ASSUMED
        OffsetDateTime determinedAt
) {
    @JsonCreator
    public UWAssignmentDeterminedEvent(
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("uwAssignment") String uwAssignment,
            @JsonProperty("suspenseDays") int suspenseDays,
            @JsonProperty("determinedAt") OffsetDateTime determinedAt
    ) {
        this.correlationId = correlationId;
        this.uwAssignment = uwAssignment;
        this.suspenseDays = suspenseDays;
        this.determinedAt = determinedAt;
    }
}
