package com.ais.middleware.prs.appraisal.domain;

/**
 * Classifies a DocumentKey to determine which backend system owns the document.
 * Routing is deterministic — key format is the sole criterion (BR-APR-004).
 */
public enum DocumentKeySource {

    AT_WORK_INSURED,
    AT_WORK_AGENT,
    DEIPDE07_MAINFRAME,
    UNKNOWN;

    /**
     * Classify a DocumentKey string without any lookup table.
     *
     * @param documentKey raw key from AppraisalListItem
     * @return DocumentKeySource enum value
     */
    public static DocumentKeySource classify(String documentKey) {
        if (documentKey == null) {
            return UNKNOWN;
        }
        if (documentKey.contains("_RiskID_I")) {
            return AT_WORK_INSURED;
        }
        if (documentKey.contains("_RiskID_A")) {
            return AT_WORK_AGENT;
        }
        if (documentKey.matches("^[0-9]{10,15}$")) {
            return DEIPDE07_MAINFRAME;
        }
        return UNKNOWN;
    }
}
