package com.ais.middleware.platform.fileprocessing.persistence;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.OffsetDateTime;

/**
 * MongoDB document for persisting FileBatch state.
 * This is an INFRASTRUCTURE concern — the domain layer uses the pure FileBatch class.
 */
@Document(collection = "file_batches")
public class FileBatchDocument {

    @Id
    private String batchId;
    private String fileName;
    private String dropZoneName;
    private String fileType;
    private String fileLocationReference;
    private long fileSizeBytes;
    private Integer totalRecords;
    private int processedRecords;
    private int succeededRecords;
    private int failedRecords;
    private String status;
    @Indexed(direction = org.springframework.data.mongodb.core.index.IndexDirection.DESCENDING)
    private OffsetDateTime receivedAt;
    private OffsetDateTime parsingCompletedAt;
    private OffsetDateTime processingCompletedAt;
    private String processingMode;

    public String getBatchId() { return batchId; }
    public void setBatchId(String batchId) { this.batchId = batchId; }
    public String getFileName() { return fileName; }
    public void setFileName(String fileName) { this.fileName = fileName; }
    public String getDropZoneName() { return dropZoneName; }
    public void setDropZoneName(String dropZoneName) { this.dropZoneName = dropZoneName; }
    public String getFileType() { return fileType; }
    public void setFileType(String fileType) { this.fileType = fileType; }
    public String getFileLocationReference() { return fileLocationReference; }
    public void setFileLocationReference(String fileLocationReference) { this.fileLocationReference = fileLocationReference; }
    public long getFileSizeBytes() { return fileSizeBytes; }
    public void setFileSizeBytes(long fileSizeBytes) { this.fileSizeBytes = fileSizeBytes; }
    public Integer getTotalRecords() { return totalRecords; }
    public void setTotalRecords(Integer totalRecords) { this.totalRecords = totalRecords; }
    public int getProcessedRecords() { return processedRecords; }
    public void setProcessedRecords(int processedRecords) { this.processedRecords = processedRecords; }
    public int getSucceededRecords() { return succeededRecords; }
    public void setSucceededRecords(int succeededRecords) { this.succeededRecords = succeededRecords; }
    public int getFailedRecords() { return failedRecords; }
    public void setFailedRecords(int failedRecords) { this.failedRecords = failedRecords; }
    public String getStatus() { return status; }
    public void setStatus(String status) { this.status = status; }
    public OffsetDateTime getReceivedAt() { return receivedAt; }
    public void setReceivedAt(OffsetDateTime receivedAt) { this.receivedAt = receivedAt; }
    public OffsetDateTime getParsingCompletedAt() { return parsingCompletedAt; }
    public void setParsingCompletedAt(OffsetDateTime parsingCompletedAt) { this.parsingCompletedAt = parsingCompletedAt; }
    public OffsetDateTime getProcessingCompletedAt() { return processingCompletedAt; }
    public void setProcessingCompletedAt(OffsetDateTime processingCompletedAt) { this.processingCompletedAt = processingCompletedAt; }
    public String getProcessingMode() { return processingMode; }
    public void setProcessingMode(String processingMode) { this.processingMode = processingMode; }
}
