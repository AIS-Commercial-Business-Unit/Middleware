package com.ais.middleware.platform.integration.api;

import com.ais.middleware.platform.integration.demo.DemoResetService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.Map;

/**
 * DemoResetController — one-click demo prep API.
 *
 * GET  /api/demo/health  — probe all Java + .NET service health endpoints
 * POST /api/demo/clear   — drop all UC4 MongoDB documents
 * POST /api/demo/seed    — insert pre-baked appraisal demo scenarios
 * POST /api/demo/reset   — orchestrate: health → clear → seed; returns full status report
 *
 * Consumed by the Platform UI /demo page so the presenter never has to remember
 * script paths or manual steps before a demo.
 */
@RestController
@RequestMapping("/api/demo")
public class DemoResetController {

    private static final Logger log = LoggerFactory.getLogger(DemoResetController.class);

    private final DemoResetService demoResetService;

    public DemoResetController(DemoResetService demoResetService) {
        this.demoResetService = demoResetService;
    }

    /**
     * GET /api/demo/health
     * Probes health endpoints for all Java + .NET services.
     * Returns a map of serviceName → { status, url, latencyMs }.
     */
    @GetMapping("/health")
    public ResponseEntity<Map<String, Object>> health() {
        log.info("[Demo] GET /api/demo/health — checking all service health endpoints");
        Map<String, Object> result = demoResetService.checkHealth();
        return ResponseEntity.ok(result);
    }

    /**
     * POST /api/demo/clear
     * Deletes all documents from UC4 MongoDB collections (both Java and .NET databases).
     */
    @PostMapping("/clear")
    public ResponseEntity<Map<String, Object>> clear() {
        log.info("[Demo] POST /api/demo/clear — clearing UC4 MongoDB collections");
        Map<String, Object> result = demoResetService.clearData();
        return ResponseEntity.ok(result);
    }

    /**
     * POST /api/demo/seed
     * Inserts the 5 standard demo appraisal scenarios into prs_appraisal_db.
     */
    @PostMapping("/seed")
    public ResponseEntity<Map<String, Object>> seed() {
        log.info("[Demo] POST /api/demo/seed — seeding demo appraisal scenarios");
        Map<String, Object> result = demoResetService.seedData();
        return ResponseEntity.ok(result);
    }

    /**
     * POST /api/demo/reset
     * Full reset: health check → clear → seed.
     * Returns a comprehensive status report suitable for display in the UI.
     */
    @PostMapping("/reset")
    public ResponseEntity<Map<String, Object>> reset() {
        log.info("[Demo] POST /api/demo/reset — executing full demo reset");
        Map<String, Object> result = demoResetService.fullReset();
        boolean success = Boolean.TRUE.equals(result.get("success"));
        return success ? ResponseEntity.ok(result)
                       : ResponseEntity.status(207).body(result); // 207 Multi-Status if partial
    }
}
