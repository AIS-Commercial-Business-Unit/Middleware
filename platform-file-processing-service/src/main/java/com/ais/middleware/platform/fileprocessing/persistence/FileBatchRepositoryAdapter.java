package com.ais.middleware.platform.fileprocessing.persistence;

import com.ais.middleware.platform.fileprocessing.domain.FileBatch;
import com.ais.middleware.platform.fileprocessing.domain.FileBatchRepository;
import org.springframework.data.domain.Sort;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.query.Criteria;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.data.mongodb.core.query.Update;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Optional;
import java.util.stream.Collectors;

/**
 * Adapter that implements the domain repository interface using MongoDB.
 * This bridges the clean domain layer to the MongoDB-specific persistence layer.
 */
@Component
public class FileBatchRepositoryAdapter implements FileBatchRepository {

    private final FileBatchMongoRepository mongoRepository;
    private final MongoTemplate mongoTemplate;

    public FileBatchRepositoryAdapter(FileBatchMongoRepository mongoRepository, MongoTemplate mongoTemplate) {
        this.mongoRepository = mongoRepository;
        this.mongoTemplate = mongoTemplate;
    }

    @Override
    public void save(FileBatch batch) {
        mongoRepository.save(toDocument(batch));
    }

    @Override
    public Optional<FileBatch> findById(String batchId) {
        return mongoRepository.findById(batchId).map(this::toDomain);
    }

    @Override
    public List<FileBatch> findAll() {
        return mongoRepository.findAll(Sort.by(Sort.Direction.DESC, "receivedAt")).stream()
                .map(this::toDomain)
                .collect(Collectors.toList());
    }

    @Override
    public List<FileBatch> findByStatusIn(List<FileBatch.FileBatchStatus> statuses) {
        List<String> statusStrings = statuses.stream()
                .map(Enum::name)
                .collect(Collectors.toList());
        return mongoRepository.findByStatusIn(statusStrings).stream()
                .map(this::toDomain)
                .collect(Collectors.toList());
    }

    @Override
    public void incrementCounters(String batchId, boolean succeeded) {
        Query query = Query.query(Criteria.where("_id").is(batchId));
        Update update = new Update()
                .inc("processedRecords", 1)
                .inc(succeeded ? "succeededRecords" : "failedRecords", 1);
        mongoTemplate.updateFirst(query, update, FileBatchDocument.class);
    }

    private FileBatchDocument toDocument(FileBatch batch) {
        FileBatchDocument doc = new FileBatchDocument();
        doc.setBatchId(batch.getBatchId());
        doc.setFileName(batch.getFileName());
        doc.setDropZoneName(batch.getDropZoneName());
        doc.setFileType(batch.getFileType());
        doc.setFileLocationReference(batch.getFileLocationReference());
        doc.setFileSizeBytes(batch.getFileSizeBytes());
        doc.setTotalRecords(batch.getTotalRecords());
        doc.setProcessedRecords(batch.getProcessedRecords());
        doc.setSucceededRecords(batch.getSucceededRecords());
        doc.setFailedRecords(batch.getFailedRecords());
        doc.setStatus(batch.getStatus() != null ? batch.getStatus().name() : null);
        doc.setReceivedAt(batch.getReceivedAt());
        doc.setParsingCompletedAt(batch.getParsingCompletedAt());
        doc.setProcessingCompletedAt(batch.getProcessingCompletedAt());
        doc.setProcessingMode(batch.getProcessingMode());
        return doc;
    }

    private FileBatch toDomain(FileBatchDocument doc) {
        FileBatch batch = new FileBatch();
        batch.setBatchId(doc.getBatchId());
        batch.setFileName(doc.getFileName());
        batch.setDropZoneName(doc.getDropZoneName());
        batch.setFileType(doc.getFileType());
        batch.setFileLocationReference(doc.getFileLocationReference());
        batch.setFileSizeBytes(doc.getFileSizeBytes());
        batch.setTotalRecords(doc.getTotalRecords());
        batch.setProcessedRecords(doc.getProcessedRecords());
        batch.setSucceededRecords(doc.getSucceededRecords());
        batch.setFailedRecords(doc.getFailedRecords());
        batch.setStatus(doc.getStatus() != null ? FileBatch.FileBatchStatus.valueOf(doc.getStatus()) : null);
        batch.setReceivedAt(doc.getReceivedAt());
        batch.setParsingCompletedAt(doc.getParsingCompletedAt());
        batch.setProcessingCompletedAt(doc.getProcessingCompletedAt());
        batch.setProcessingMode(doc.getProcessingMode());
        return batch;
    }
}
