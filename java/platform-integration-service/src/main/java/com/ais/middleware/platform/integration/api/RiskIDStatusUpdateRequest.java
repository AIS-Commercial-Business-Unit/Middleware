package com.ais.middleware.platform.integration.api;

import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * Inbound HTTP body for POST /api/riskid/status-update.
 *
 * ⚠️ DEMO GAP: Real RiskID MQ message format unknown - using mock structure.
 * Production: IBM MQ consumer reads native RiskID message and maps to this schema.
 */
public record RiskIDStatusUpdateRequest(
        @JsonProperty("policyNumber") String policyNumber,
        // ⚠️ DEMO GAP: Actual RiskID schema needed from integration team
        @JsonProperty("appraisalId") String appraisalId,
        @JsonProperty("statusCode") int statusCode,
        @JsonProperty("inspectionTypeCode") String inspectionTypeCode,
        @JsonProperty("timestamp") String timestamp
) {}
