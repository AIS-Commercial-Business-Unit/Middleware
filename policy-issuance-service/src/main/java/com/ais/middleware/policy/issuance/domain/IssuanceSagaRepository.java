package com.ais.middleware.policy.issuance.domain;

import java.util.Optional;

/**
 * Domain repository interface for IssuanceSaga persistence.
 * CLEAN DOMAIN: No infrastructure imports — just pure Java.
 * Implementation provided by the persistence adapter.
 */
public interface IssuanceSagaRepository {
    void save(IssuanceSagaRecord saga);
    Optional<IssuanceSagaRecord> findById(String issuanceId);
    boolean existsById(String issuanceId);
}
