package com.ais.middleware.platform.compliance.domain;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;
import java.time.OffsetDateTime;

@Document(collection = "compliance_checks")
public class ComplianceCheck {
    @Id
    private String checkId;
    private String correlationId;
    private String sourceDomain;
    private String checkType;
    private String subjectType;
    private String subjectId;
    private CheckStatus status;
    private OffsetDateTime requestedAt;
    private OffsetDateTime resultReceivedAt;
    private String externalReferenceId;

    public enum CheckStatus { Pending, Clear, PendingReview, Blocked, Cleared }

    public String getCheckId() { return checkId; }
    public void setCheckId(String checkId) { this.checkId = checkId; }
    public String getCorrelationId() { return correlationId; }
    public void setCorrelationId(String correlationId) { this.correlationId = correlationId; }
    public String getSourceDomain() { return sourceDomain; }
    public void setSourceDomain(String sourceDomain) { this.sourceDomain = sourceDomain; }
    public String getCheckType() { return checkType; }
    public void setCheckType(String checkType) { this.checkType = checkType; }
    public String getSubjectType() { return subjectType; }
    public void setSubjectType(String subjectType) { this.subjectType = subjectType; }
    public String getSubjectId() { return subjectId; }
    public void setSubjectId(String subjectId) { this.subjectId = subjectId; }
    public CheckStatus getStatus() { return status; }
    public void setStatus(CheckStatus status) { this.status = status; }
    public OffsetDateTime getRequestedAt() { return requestedAt; }
    public void setRequestedAt(OffsetDateTime requestedAt) { this.requestedAt = requestedAt; }
    public OffsetDateTime getResultReceivedAt() { return resultReceivedAt; }
    public void setResultReceivedAt(OffsetDateTime resultReceivedAt) { this.resultReceivedAt = resultReceivedAt; }
    public String getExternalReferenceId() { return externalReferenceId; }
    public void setExternalReferenceId(String externalReferenceId) { this.externalReferenceId = externalReferenceId; }
}
