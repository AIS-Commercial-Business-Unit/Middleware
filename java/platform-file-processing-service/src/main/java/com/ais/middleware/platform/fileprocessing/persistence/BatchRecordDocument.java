package com.ais.middleware.platform.fileprocessing.persistence;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.OffsetDateTime;

/**
 * MongoDB document for persisting BatchRecord state.
 * This is an INFRASTRUCTURE concern — the domain layer uses the pure BatchRecord class.
 */
@Document(collection = "batch_records")
public class BatchRecordDocument {

    @Id
    private String recordId;
    @Indexed
    private String batchId;
    private int sequenceNumber;
    private String rawContent;
    private String status;
    private int retryCount;
    private String processorResult;
    private OffsetDateTime processedAt;
    @Indexed(unique = true)
    private String correlationId;
    private String failureCategory;

    public String getRecordId() { return recordId; }
    public void setRecordId(String recordId) { this.recordId = recordId; }
    public String getBatchId() { return batchId; }
    public void setBatchId(String batchId) { this.batchId = batchId; }
    public int getSequenceNumber() { return sequenceNumber; }
    public void setSequenceNumber(int sequenceNumber) { this.sequenceNumber = sequenceNumber; }
    public String getRawContent() { return rawContent; }
    public void setRawContent(String rawContent) { this.rawContent = rawContent; }
    public String getStatus() { return status; }
    public void setStatus(String status) { this.status = status; }
    public int getRetryCount() { return retryCount; }
    public void setRetryCount(int retryCount) { this.retryCount = retryCount; }
    public String getProcessorResult() { return processorResult; }
    public void setProcessorResult(String processorResult) { this.processorResult = processorResult; }
    public OffsetDateTime getProcessedAt() { return processedAt; }
    public void setProcessedAt(OffsetDateTime processedAt) { this.processedAt = processedAt; }
    public String getCorrelationId() { return correlationId; }
    public void setCorrelationId(String correlationId) { this.correlationId = correlationId; }
    public String getFailureCategory() { return failureCategory; }
    public void setFailureCategory(String failureCategory) { this.failureCategory = failureCategory; }
}
