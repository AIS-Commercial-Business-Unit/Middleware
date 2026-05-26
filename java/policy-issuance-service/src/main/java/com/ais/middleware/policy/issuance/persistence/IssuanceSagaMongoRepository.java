package com.ais.middleware.policy.issuance.persistence;

import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

/**
 * Spring Data MongoDB repository for IssuanceSagaDocument.
 * This is an INFRASTRUCTURE concern — accessed only by the adapter.
 */
@Repository
public interface IssuanceSagaMongoRepository extends MongoRepository<IssuanceSagaDocument, String> {
}
