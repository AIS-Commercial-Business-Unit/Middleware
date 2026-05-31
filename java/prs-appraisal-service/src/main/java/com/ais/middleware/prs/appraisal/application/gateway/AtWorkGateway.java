package com.ais.middleware.prs.appraisal.application.gateway;

import com.ais.middleware.prs.appraisal.domain.AppraisalListItem;
import com.ais.middleware.prs.appraisal.domain.AppraisalListItem.Source;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Map;

/**
 * Demo fixture for @Work SQL stored procedure ESB_MWInterfaces_LC360_GetAppraisalList_MPC.
 *
 * Production replacement: swap this bean for a real DataSource/JdbcTemplate call.
 * The Camel route calls this via .bean(AtWorkGateway.class, "getAppraisalList(...)") —
 * only this class changes when the real SQL connection is wired.
 */
@Component
public class AtWorkGateway {

    private static final Logger log = LoggerFactory.getLogger(AtWorkGateway.class);

    private static final Map<String, List<AppraisalListItem>> FIXTURE = Map.of(

        "POL-001-TEST", List.of(
            new AppraisalListItem(
                "RK-001-INSURED", "POL-001-TEST",
                "456 MAPLE STREET", "ST PAUL", "MN", "55102",
                "2024-02-10", "APPRAISAL", "Insured Full Appraisal",
                "POL-001-TEST_RiskID_I_INS001", Source.AT_WORK),
            new AppraisalListItem(
                "RK-002-AGENT", "POL-001-TEST",
                "789 ELM AVE", "MINNEAPOLIS", "MN", "55404",
                "2024-01-20", "APPRAISAL", "Agent Appraisal Report",
                "POL-001-TEST_RiskID_A_AGT002", Source.AT_WORK)
        ),

        "POL-002-TEST", List.of(),

        "POL-003-TEST", List.of(
            new AppraisalListItem(
                "RK-100-INSURED", "POL-003-TEST",
                "321 PINE ROAD", "DULUTH", "MN", "55803",
                "2024-04-05", "REINSPECTION", "Reinspection Report",
                "POL-003-TEST_RiskID_I_INS100", Source.AT_WORK)
        )
    );

    /**
     * Return @Work appraisal records for the given policy number.
     * Returns 1 record for all unknown policies (demo default).
     */
    public List<AppraisalListItem> getAppraisalList(String policyNumber) {
        log.info("AtWorkGateway.getAppraisalList: policyNumber={}", policyNumber);

        if (FIXTURE.containsKey(policyNumber)) {
            List<AppraisalListItem> result = FIXTURE.get(policyNumber);
            log.info("AtWorkGateway.getAppraisalList: policyNumber={} resultCount={}", policyNumber, result.size());
            return result;
        }

        // Default: 1 record for unknown policies
        List<AppraisalListItem> defaultResult = List.of(
            new AppraisalListItem(
                "RK-DEFAULT", policyNumber,
                "100 DEFAULT BLVD", "MINNEAPOLIS", "MN", "55401",
                "2024-06-01", "APPRAISAL", "Default Appraisal",
                policyNumber + "_RiskID_I_DEFAULT", Source.AT_WORK)
        );
        log.info("AtWorkGateway.getAppraisalList: policyNumber={} resultCount=1 (default fixture)", policyNumber);
        return defaultResult;
    }
}
