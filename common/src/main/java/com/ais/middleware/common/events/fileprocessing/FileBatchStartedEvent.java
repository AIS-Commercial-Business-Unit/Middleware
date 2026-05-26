package com.ais.middleware.common.events.fileprocessing;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;

/**
 * Published by platform-file-processing-service when a new batch file is detected and parsing begins.
 */
public record FileBatchStartedEvent(
        String batchId,
        String fileName,
        String dropZoneName,
        int totalRecords,
        String fileType,
        OffsetDateTime startedAt
) {
    @JsonCreator
    public FileBatchStartedEvent(
            @JsonProperty("batchId") String batchId,
            @JsonProperty("fileName") String fileName,
            @JsonProperty("dropZoneName") String dropZoneName,
            @JsonProperty("totalRecords") int totalRecords,
            @JsonProperty("fileType") String fileType,
            @JsonProperty("startedAt") OffsetDateTime startedAt
    ) {
        this.batchId = batchId;
        this.fileName = fileName;
        this.dropZoneName = dropZoneName;
        this.totalRecords = totalRecords;
        this.fileType = fileType;
        this.startedAt = startedAt;
    }
}
