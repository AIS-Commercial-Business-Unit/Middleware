package com.ais.middleware.platform.compliance.persistence;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;
import java.time.OffsetDateTime;

/**
 * MongoDB document for persisting ComplianceCheck state.
 * This is an INFRASTRUCTURE concern — the domain layer uses the pure ComplianceCheck class.
 */
@Document(collection = "compliance_checks")
public class ComplianceCheckDocument {
    @Id
    private String checkId;
    @Indexed
    private String correlationId;
    private String sourceDomain;
    private String checkType;
    private String subjectType;
    private String subjectId;
    private String status;
    private OffsetDateTime requestedAt;
    private OffsetDateTime resultReceivedAt;
    private String externalReferenceId;

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
    public String getStatus() { return status; }
    public void setStatus(String status) { this.status = status; }
    public OffsetDateTime getRequestedAt() { return requestedAt; }
    public void setRequestedAt(OffsetDateTime requestedAt) { this.requestedAt = requestedAt; }
    public OffsetDateTime getResultReceivedAt() { return resultReceivedAt; }
    public void setResultReceivedAt(OffsetDateTime resultReceivedAt) { this.resultReceivedAt = resultReceivedAt; }
    public String getExternalReferenceId() { return externalReferenceId; }
    public void setExternalReferenceId(String externalReferenceId) { this.externalReferenceId = externalReferenceId; }
}
