package com.ais.middleware.platform.fileprocessing.domain;

import java.time.OffsetDateTime;

/**
 * Domain entity representing a single CSV row extracted from a FileBatch.
 * CLEAN DOMAIN: No infrastructure imports (Spring, MongoDB, etc.).
 * The correlationId field equals the issuanceId used in the IssuanceSaga.
 */
public class BatchRecord {

    private String recordId;
    private String batchId;
    private int sequenceNumber;
    private String rawContent;
    private BatchRecordStatus status;
    private int retryCount;
    private String processorResult;
    private OffsetDateTime processedAt;
    private String correlationId;

    public enum BatchRecordStatus {
        Pending, Processing, Succeeded, Failed, DeadLettered
    }

    public String getRecordId() { return recordId; }
    public void setRecordId(String recordId) { this.recordId = recordId; }
    public String getBatchId() { return batchId; }
    public void setBatchId(String batchId) { this.batchId = batchId; }
    public int getSequenceNumber() { return sequenceNumber; }
    public void setSequenceNumber(int sequenceNumber) { this.sequenceNumber = sequenceNumber; }
    public String getRawContent() { return rawContent; }
    public void setRawContent(String rawContent) { this.rawContent = rawContent; }
    public BatchRecordStatus getStatus() { return status; }
    public void setStatus(BatchRecordStatus status) { this.status = status; }
    public int getRetryCount() { return retryCount; }
    public void setRetryCount(int retryCount) { this.retryCount = retryCount; }
    public String getProcessorResult() { return processorResult; }
    public void setProcessorResult(String processorResult) { this.processorResult = processorResult; }
    public OffsetDateTime getProcessedAt() { return processedAt; }
    public void setProcessedAt(OffsetDateTime processedAt) { this.processedAt = processedAt; }
    public String getCorrelationId() { return correlationId; }
    public void setCorrelationId(String correlationId) { this.correlationId = correlationId; }
}
