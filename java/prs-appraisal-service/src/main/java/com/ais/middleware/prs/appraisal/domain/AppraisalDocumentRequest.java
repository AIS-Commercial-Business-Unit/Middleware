package com.ais.middleware.prs.appraisal.domain;

/**
 * Input for GetAppraisalDocument.
 * documentKey is obtained from AppraisalListItem.documentKey — its format
 * encodes the source system and is the sole routing criterion (BR-APR-004).
 */
public record AppraisalDocumentRequest(String documentKey) {}
