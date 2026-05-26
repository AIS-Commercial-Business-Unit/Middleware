package com.ais.middleware.platform.fileprocessing.domain;

import java.util.List;
import java.util.Optional;

/**
 * Domain repository interface for FileBatch persistence.
 * CLEAN DOMAIN: No infrastructure imports — just pure Java.
 * Implementation provided by the persistence adapter.
 */
public interface FileBatchRepository {
    void save(FileBatch batch);
    Optional<FileBatch> findById(String batchId);
    List<FileBatch> findAll();
    List<FileBatch> findByStatusIn(List<FileBatch.FileBatchStatus> statuses);
    
    /**
     * Atomically increment batch progress counters.
     * This is a domain operation that the persistence layer must support atomically.
     */
    void incrementCounters(String batchId, boolean succeeded);
}
