package com.ais.middleware.policy.issuance.persistence;

import com.ais.middleware.policy.issuance.domain.IssuanceSagaRecord;
import com.ais.middleware.policy.issuance.domain.IssuanceSagaRepository;
import org.springframework.stereotype.Component;

import java.util.Optional;

/**
 * Adapter that implements the domain repository interface using MongoDB.
 * This bridges the clean domain layer to the MongoDB-specific persistence layer.
 */
@Component
public class IssuanceSagaRepositoryAdapter implements IssuanceSagaRepository {

    private final IssuanceSagaMongoRepository mongoRepository;

    public IssuanceSagaRepositoryAdapter(IssuanceSagaMongoRepository mongoRepository) {
        this.mongoRepository = mongoRepository;
    }

    @Override
    public void save(IssuanceSagaRecord saga) {
        mongoRepository.save(toDocument(saga));
    }

    @Override
    public Optional<IssuanceSagaRecord> findById(String issuanceId) {
        return mongoRepository.findById(issuanceId).map(this::toDomain);
    }

    @Override
    public boolean existsById(String issuanceId) {
        return mongoRepository.existsById(issuanceId);
    }

    private IssuanceSagaDocument toDocument(IssuanceSagaRecord saga) {
        IssuanceSagaDocument doc = new IssuanceSagaDocument();
        doc.setIssuanceId(saga.getIssuanceId());
        doc.setAccountId(saga.getAccountId());
        doc.setAccountServiceRequestNumber(saga.getAccountServiceRequestNumber());
        doc.setPolicyNumbers(saga.getPolicyNumbers());
        doc.setStatus(saga.getStatus() != null ? saga.getStatus().name() : null);
        doc.setPolicyTypeCode(saga.getPolicyTypeCode());
        doc.setPolicyTypeSubCode(saga.getPolicyTypeSubCode());
        doc.setTargetPas(saga.getTargetPas());
        doc.setPasRetryCount(saga.getPasRetryCount());
        doc.setBillingComplete(saga.isBillingComplete());
        doc.setCustomerUpdateComplete(saga.isCustomerUpdateComplete());
        doc.setFailureReason(saga.getFailureReason());
        doc.setRequestedAt(saga.getRequestedAt());
        doc.setCompletedAt(saga.getCompletedAt());
        doc.setSubmittingChannel(saga.getSubmittingChannel());
        doc.setRecordId(saga.getRecordId());
        doc.setBatchId(saga.getBatchId());
        return doc;
    }

    private IssuanceSagaRecord toDomain(IssuanceSagaDocument doc) {
        IssuanceSagaRecord saga = new IssuanceSagaRecord();
        saga.setIssuanceId(doc.getIssuanceId());
        saga.setAccountId(doc.getAccountId());
        saga.setAccountServiceRequestNumber(doc.getAccountServiceRequestNumber());
        saga.setPolicyNumbers(doc.getPolicyNumbers());
        saga.setStatus(doc.getStatus() != null ? IssuanceSagaRecord.SagaStatus.valueOf(doc.getStatus()) : null);
        saga.setPolicyTypeCode(doc.getPolicyTypeCode());
        saga.setPolicyTypeSubCode(doc.getPolicyTypeSubCode());
        saga.setTargetPas(doc.getTargetPas());
        saga.setPasRetryCount(doc.getPasRetryCount());
        saga.setBillingComplete(doc.isBillingComplete());
        saga.setCustomerUpdateComplete(doc.isCustomerUpdateComplete());
        saga.setFailureReason(doc.getFailureReason());
        saga.setRequestedAt(doc.getRequestedAt());
        saga.setCompletedAt(doc.getCompletedAt());
        saga.setSubmittingChannel(doc.getSubmittingChannel());
        saga.setRecordId(doc.getRecordId());
        saga.setBatchId(doc.getBatchId());
        return saga;
    }
}
