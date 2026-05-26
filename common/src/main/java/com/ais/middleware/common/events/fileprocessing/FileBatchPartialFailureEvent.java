package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published when all records in a batch are processed but at least one failed.
 */
public record FileBatchPartialFailureEvent(
        String batchId,
        String fileName,
        int totalRecords,
        int succeededRecords,
        int failedRecords,
        OffsetDateTime completedAt
) {
    @JsonCreator
    public FileBatchPartialFailureEvent(
            @JsonProperty("batchId") String batchId,
            @JsonProperty("fileName") String fileName,
            @JsonProperty("totalRecords") int totalRecords,
            @JsonProperty("succeededRecords") int succeededRecords,
            @JsonProperty("failedRecords") int failedRecords,
            @JsonProperty("completedAt") OffsetDateTime completedAt
    ) {
        this.batchId = batchId;
        this.fileName = fileName;
        this.totalRecords = totalRecords;
        this.succeededRecords = succeededRecords;
        this.failedRecords = failedRecords;
        this.completedAt = completedAt;
    }
}
