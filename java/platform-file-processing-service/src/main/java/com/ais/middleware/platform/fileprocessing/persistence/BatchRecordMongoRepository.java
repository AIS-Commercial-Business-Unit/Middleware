package com.ais.middleware.platform.fileprocessing.persistence;

import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

/**
 * Spring Data MongoDB repository for BatchRecordDocument.
 * This is an INFRASTRUCTURE concern — accessed only by the adapter.
 */
@Repository
public interface BatchRecordMongoRepository extends MongoRepository<BatchRecordDocument, String> {
    List<BatchRecordDocument> findByBatchId(String batchId);
    long countByBatchIdAndStatus(String batchId, String status);
    Optional<BatchRecordDocument> findByCorrelationId(String correlationId);
}
