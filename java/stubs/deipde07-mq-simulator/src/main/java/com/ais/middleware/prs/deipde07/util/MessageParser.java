package com.ais.middleware.prs.deipde07.util;

import org.springframework.stereotype.Component;

@Component
public class MessageParser {

    public boolean isAppraisalListRequest(String body) {
        return body != null && body.startsWith("APPRAISAL_LIST|||");
    }

    public boolean isDocumentRequest(String body) {
        return body != null && body.startsWith("APPRAISAL_DOC|||");
    }

    public String parsePolicyNumber(String body) {
        if (!isAppraisalListRequest(body)) {
            return null;
        }
        String[] parts = body.split("\\|\\|\\|");
        return parts.length >= 2 ? parts[1].trim() : null;
    }

    public String parseDocumentKey(String body) {
        if (!isDocumentRequest(body)) {
            return null;
        }
        String[] parts = body.split("\\|\\|\\|");
        return parts.length >= 2 ? parts[1].trim() : null;
    }
}
