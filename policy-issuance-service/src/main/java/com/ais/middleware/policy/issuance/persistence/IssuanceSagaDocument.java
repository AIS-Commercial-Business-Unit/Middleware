package com.ais.middleware.policy.issuance.persistence;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;
import java.time.OffsetDateTime;
import java.util.List;

/**
 * MongoDB document for persisting IssuanceSaga state.
 * This is an INFRASTRUCTURE concern — the domain layer uses the pure IssuanceSagaRecord class.
 */
@Document(collection = "issuance_sagas")
public class IssuanceSagaDocument {

    @Id
    private String issuanceId;
    private String accountId;
    private String accountServiceRequestNumber;
    private List<String> policyNumbers;
    private String status;
    private int policyTypeCode;
    private int policyTypeSubCode;
    private String targetPas;
    private int pasRetryCount;
    private boolean billingComplete;
    private boolean customerUpdateComplete;
    private String failureReason;
    private OffsetDateTime requestedAt;
    private OffsetDateTime completedAt;
    private String submittingChannel;
    private String recordId;
    @Indexed
    private String batchId;

    public String getIssuanceId() { return issuanceId; }
    public void setIssuanceId(String issuanceId) { this.issuanceId = issuanceId; }
    public String getAccountId() { return accountId; }
    public void setAccountId(String accountId) { this.accountId = accountId; }
    public String getAccountServiceRequestNumber() { return accountServiceRequestNumber; }
    public void setAccountServiceRequestNumber(String n) { this.accountServiceRequestNumber = n; }
    public List<String> getPolicyNumbers() { return policyNumbers; }
    public void setPolicyNumbers(List<String> policyNumbers) { this.policyNumbers = policyNumbers; }
    public String getStatus() { return status; }
    public void setStatus(String status) { this.status = status; }
    public int getPolicyTypeCode() { return policyTypeCode; }
    public void setPolicyTypeCode(int policyTypeCode) { this.policyTypeCode = policyTypeCode; }
    public int getPolicyTypeSubCode() { return policyTypeSubCode; }
    public void setPolicyTypeSubCode(int policyTypeSubCode) { this.policyTypeSubCode = policyTypeSubCode; }
    public String getTargetPas() { return targetPas; }
    public void setTargetPas(String targetPas) { this.targetPas = targetPas; }
    public int getPasRetryCount() { return pasRetryCount; }
    public void setPasRetryCount(int pasRetryCount) { this.pasRetryCount = pasRetryCount; }
    public boolean isBillingComplete() { return billingComplete; }
    public void setBillingComplete(boolean billingComplete) { this.billingComplete = billingComplete; }
    public boolean isCustomerUpdateComplete() { return customerUpdateComplete; }
    public void setCustomerUpdateComplete(boolean customerUpdateComplete) { this.customerUpdateComplete = customerUpdateComplete; }
    public String getFailureReason() { return failureReason; }
    public void setFailureReason(String failureReason) { this.failureReason = failureReason; }
    public OffsetDateTime getRequestedAt() { return requestedAt; }
    public void setRequestedAt(OffsetDateTime requestedAt) { this.requestedAt = requestedAt; }
    public OffsetDateTime getCompletedAt() { return completedAt; }
    public void setCompletedAt(OffsetDateTime completedAt) { this.completedAt = completedAt; }
    public String getSubmittingChannel() { return submittingChannel; }
    public void setSubmittingChannel(String submittingChannel) { this.submittingChannel = submittingChannel; }
    public String getRecordId() { return recordId; }
    public void setRecordId(String recordId) { this.recordId = recordId; }
    public String getBatchId() { return batchId; }
    public void setBatchId(String batchId) { this.batchId = batchId; }
}
