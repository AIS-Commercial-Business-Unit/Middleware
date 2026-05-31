package com.ais.middleware.prs.appraisal;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

/**
 * PRS Appraisal Service — UC4 query-side workflows.
 *
 * Hosts two Camel routes:
 *   1. GetAppraisalList  — scatter-gather fan-out to @Work SQL and DEIPDE07 mainframe via IBM MQ
 *   2. GetAppraisalDocument — content-based router to RiskID WCF or DEIPDE07 MQ chunked PDF
 *
 * Exposes REST endpoints:
 *   POST /api/appraisals/list     — returns merged, deduplicated appraisal list
 *   POST /api/appraisals/document — returns base64-encoded PDF
 *
 * No saga state. Both workflows are synchronous query operations with async-to-sync MQ bridges.
 */
@SpringBootApplication
public class PrsAppraisalApplication {
    public static void main(String[] args) {
        SpringApplication.run(PrsAppraisalApplication.class, args);
    }
}
