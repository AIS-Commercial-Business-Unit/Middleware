package com.ais.middleware.prs.appraisal.domain;

/**
 * Response from GetAppraisalDocument.
 * base64Pdf contains the full PDF encoded as Base64 (BR-APR-005).
 * Raw PDF bytes are never exposed — consumers render client-side.
 */
public record AppraisalDocumentResponse(
        String documentKey,
        String base64Pdf,
        String contentType
) {}
