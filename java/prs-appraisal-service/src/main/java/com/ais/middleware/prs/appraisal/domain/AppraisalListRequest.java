package com.ais.middleware.prs.appraisal.domain;

/**
 * Input for the GetAppraisalList scatter-gather query.
 * PolicyNumber is the sole routing key — no backend lookup required.
 */
public record AppraisalListRequest(String policyNumber) {}
