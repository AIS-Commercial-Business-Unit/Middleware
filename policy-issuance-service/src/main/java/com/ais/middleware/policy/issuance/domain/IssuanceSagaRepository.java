package com.ais.middleware.policy.issuance.domain;

import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface IssuanceSagaRepository extends MongoRepository<IssuanceSagaRecord, String> {
}
