package com.ais.middleware.platform.fileprocessing.persistence;

import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

/**
 * Spring Data MongoDB repository for FileBatchDocument.
 * This is an INFRASTRUCTURE concern — accessed only by the adapter.
 */
@Repository
public interface FileBatchMongoRepository extends MongoRepository<FileBatchDocument, String> {
    List<FileBatchDocument> findByStatusIn(List<String> statuses);
}
