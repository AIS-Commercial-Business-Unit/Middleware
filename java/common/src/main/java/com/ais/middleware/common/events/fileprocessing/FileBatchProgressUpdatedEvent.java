package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published every 5 records processed (or on completion) for UI polling.
 */
public record FileBatchProgressUpdatedEvent(
        String batchId,
        String fileName,
        int processedRecords,
        int succeededRecords,
        int failedRecords,
        int totalRecords,
        double percentComplete,
        OffsetDateTime updatedAt
) {
    @JsonCreator
    public FileBatchProgressUpdatedEvent(
            @JsonProperty("batchId") String batchId,
            @JsonProperty("fileName") String fileName,
            @JsonProperty("processedRecords") int processedRecords,
            @JsonProperty("succeededRecords") int succeededRecords,
            @JsonProperty("failedRecords") int failedRecords,
            @JsonProperty("totalRecords") int totalRecords,
            @JsonProperty("percentComplete") double percentComplete,
            @JsonProperty("updatedAt") OffsetDateTime updatedAt
    ) {
        this.batchId = batchId;
        this.fileName = fileName;
        this.processedRecords = processedRecords;
        this.succeededRecords = succeededRecords;
        this.failedRecords = failedRecords;
        this.totalRecords = totalRecords;
        this.percentComplete = percentComplete;
        this.updatedAt = updatedAt;
    }
}
