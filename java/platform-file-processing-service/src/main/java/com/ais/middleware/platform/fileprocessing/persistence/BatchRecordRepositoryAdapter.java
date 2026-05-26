package com.ais.middleware.platform.fileprocessing.persistence;

import com.ais.middleware.platform.fileprocessing.domain.BatchRecord;
import com.ais.middleware.platform.fileprocessing.domain.BatchRecordRepository;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Optional;
import java.util.stream.Collectors;

/**
 * Adapter that implements the domain repository interface using MongoDB.
 * This bridges the clean domain layer to the MongoDB-specific persistence layer.
 */
@Component
public class BatchRecordRepositoryAdapter implements BatchRecordRepository {

    private final BatchRecordMongoRepository mongoRepository;

    public BatchRecordRepositoryAdapter(BatchRecordMongoRepository mongoRepository) {
        this.mongoRepository = mongoRepository;
    }

    @Override
    public void save(BatchRecord record) {
        mongoRepository.save(toDocument(record));
    }

    @Override
    public List<BatchRecord> findByBatchId(String batchId) {
        return mongoRepository.findByBatchId(batchId).stream()
                .map(this::toDomain)
                .collect(Collectors.toList());
    }

    @Override
    public long countByBatchIdAndStatus(String batchId, BatchRecord.BatchRecordStatus status) {
        return mongoRepository.countByBatchIdAndStatus(batchId, status.name());
    }

    @Override
    public Optional<BatchRecord> findByCorrelationId(String correlationId) {
        return mongoRepository.findByCorrelationId(correlationId).map(this::toDomain);
    }

    private BatchRecordDocument toDocument(BatchRecord record) {
        BatchRecordDocument doc = new BatchRecordDocument();
        doc.setRecordId(record.getRecordId());
        doc.setBatchId(record.getBatchId());
        doc.setSequenceNumber(record.getSequenceNumber());
        doc.setRawContent(record.getRawContent());
        doc.setStatus(record.getStatus() != null ? record.getStatus().name() : null);
        doc.setRetryCount(record.getRetryCount());
        doc.setProcessorResult(record.getProcessorResult());
        doc.setProcessedAt(record.getProcessedAt());
        doc.setCorrelationId(record.getCorrelationId());
        return doc;
    }

    private BatchRecord toDomain(BatchRecordDocument doc) {
        BatchRecord record = new BatchRecord();
        record.setRecordId(doc.getRecordId());
        record.setBatchId(doc.getBatchId());
        record.setSequenceNumber(doc.getSequenceNumber());
        record.setRawContent(doc.getRawContent());
        record.setStatus(doc.getStatus() != null ? BatchRecord.BatchRecordStatus.valueOf(doc.getStatus()) : null);
        record.setRetryCount(doc.getRetryCount());
        record.setProcessorResult(doc.getProcessorResult());
        record.setProcessedAt(doc.getProcessedAt());
        record.setCorrelationId(doc.getCorrelationId());
        return record;
    }
}
