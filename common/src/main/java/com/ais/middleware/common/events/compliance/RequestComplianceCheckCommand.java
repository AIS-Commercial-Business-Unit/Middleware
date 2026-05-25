package com.ais.middleware.common.events.compliance;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * Command sent by PolicyIssuanceAndLifecycleManagement to Platform.Compliance.
 * It triggers economic sanctions screening against RSK3X3, and correlationId must equal the IssuanceId so the saga can be resumed when the result arrives (BR-CMP-006).
 */
public record RequestComplianceCheckCommand(
        String checkId,
        String correlationId,
        String sourceDomain,
        String checkType,
        String subjectType,
        String subjectId,
        String subjectData
) {
    @JsonCreator
    public RequestComplianceCheckCommand(
            @JsonProperty("checkId") String checkId,
            @JsonProperty("correlationId") String correlationId,
            @JsonProperty("sourceDomain") String sourceDomain,
            @JsonProperty("checkType") String checkType,
            @JsonProperty("subjectType") String subjectType,
            @JsonProperty("subjectId") String subjectId,
            @JsonProperty("subjectData") String subjectData
    ) {
        this.checkId = checkId;
        this.correlationId = correlationId;
        this.sourceDomain = sourceDomain;
        this.checkType = checkType;
        this.subjectType = subjectType;
        this.subjectId = subjectId;
        this.subjectData = subjectData;
    }
}
