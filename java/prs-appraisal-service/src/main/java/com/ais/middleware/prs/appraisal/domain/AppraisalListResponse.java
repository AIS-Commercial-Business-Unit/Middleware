package com.ais.middleware.prs.appraisal.domain;

import java.util.List;

/**
 * Response from GetAppraisalList.
 * partialResult=true when the DEIPDE07 MQ branch timed out (BR-APR-002).
 */
public record AppraisalListResponse(
        String policyNumber,
        List<AppraisalListItem> items,
        boolean partialResult
) {}
