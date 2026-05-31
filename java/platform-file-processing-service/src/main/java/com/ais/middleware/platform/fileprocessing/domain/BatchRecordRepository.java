package com.ais.middleware.platform.fileprocessing.domain;

import java.util.List;
import java.util.Optional;

/**
 * Domain repository interface for BatchRecord persistence.
 * CLEAN DOMAIN: No infrastructure imports — just pure Java.
 * Implementation provided by the persistence adapter.
 */
public interface BatchRecordRepository {
    void save(BatchRecord record);
    List<BatchRecord> findByBatchId(String batchId);
    long countByBatchIdAndStatus(String batchId, BatchRecord.BatchRecordStatus status);
    Optional<BatchRecord> findByCorrelationId(String correlationId);
    Optional<BatchRecord> findById(String recordId);
}
