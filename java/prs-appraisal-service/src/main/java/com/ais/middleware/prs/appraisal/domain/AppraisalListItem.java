package com.ais.middleware.prs.appraisal.domain;

/**
 * A single appraisal record returned by GetAppraisalList.
 * Read-side projection only — never persisted.
 *
 * source: which backend system produced this record (not exposed to API consumers).
 * documentKey: encodes the source system; used as routing key in GetAppraisalDocument.
 */
public record AppraisalListItem(
        String appraisalUid,
        String policyQuoteNbr,
        String streetAdr,
        String cityAdr,
        String stateCde,
        String zipAdr,
        String appraisalDte,
        String documentType,
        String documentName,
        String documentKey,
        Source source
) {
    public enum Source {
        AT_WORK,
        DEIPDE07
    }
}
