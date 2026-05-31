package com.ais.middleware.prs.appraisal.application.gateway;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

/**
 * Demo fixture for the RiskID WCF GetInspectionPDFBizTalk service.
 *
 * Production replacement: swap this bean for a real HTTP/SOAP CXF call to the WCF endpoint.
 * The Camel route calls this via .bean(RiskIdWcfGateway.class, "getInspectionPdf*(...)") —
 * only this class changes when the real WCF endpoint is wired.
 *
 * ⚠️ DEMO GAP: Real WCF request schema (inspectionID, pdfType values) not yet confirmed.
 * See BR-APR-004 and 25-appraisal.md.
 */
@Component
public class RiskIdWcfGateway {

    private static final Logger log = LoggerFactory.getLogger(RiskIdWcfGateway.class);

    private static final String FAKE_PDF_CONTENT = "FAKE-RISKID-PDF-CONTENT-FOR-DEMO";

    /**
     * Retrieve insured inspection PDF (pdfType=Abiz equivalent) from RiskID WCF.
     *
     * @param documentKey the _RiskID_I documentKey from AppraisalListItem
     * @return base64-encoded PDF string
     */
    public String getInspectionPdfInsured(String documentKey) {
        log.info("RiskIdWcfGateway.getInspectionPdfInsured: documentKey={}", documentKey);
        String result = Base64.getEncoder().encodeToString(
            (FAKE_PDF_CONTENT + "-INSURED-" + documentKey).getBytes(StandardCharsets.UTF_8));
        log.info("RiskIdWcfGateway.getInspectionPdfInsured: documentKey={} pdfBytes={}", documentKey, result.length());
        return result;
    }

    /**
     * Retrieve agent inspection PDF from RiskID WCF.
     *
     * @param documentKey the _RiskID_A documentKey from AppraisalListItem
     * @return base64-encoded PDF string
     */
    public String getInspectionPdfAgent(String documentKey) {
        log.info("RiskIdWcfGateway.getInspectionPdfAgent: documentKey={}", documentKey);
        String result = Base64.getEncoder().encodeToString(
            (FAKE_PDF_CONTENT + "-AGENT-" + documentKey).getBytes(StandardCharsets.UTF_8));
        log.info("RiskIdWcfGateway.getInspectionPdfAgent: documentKey={} pdfBytes={}", documentKey, result.length());
        return result;
    }
}
