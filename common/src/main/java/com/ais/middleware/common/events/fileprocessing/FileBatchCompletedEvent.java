package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published when all records in a batch are processed with zero failures.
 */
public record FileBatchCompletedEvent(
        String batchId,
        String fileName,
        int totalRecords,
        int succeededRecords,
        OffsetDateTime completedAt
) {
    @JsonCreator
    public FileBatchCompletedEvent(
            @JsonProperty("batchId") String batchId,
            @JsonProperty("fileName") String fileName,
            @JsonProperty("totalRecords") int totalRecords,
            @JsonProperty("succeededRecords") int succeededRecords,
            @JsonProperty("completedAt") OffsetDateTime completedAt
    ) {
        this.batchId = batchId;
        this.fileName = fileName;
        this.totalRecords = totalRecords;
        this.succeededRecords = succeededRecords;
        this.completedAt = completedAt;
    }
}
