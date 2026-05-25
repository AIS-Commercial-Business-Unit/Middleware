package com.ais.middleware.stubs.rsk3x3;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.UUID;

@RestController
public class Rsk3x3ComplianceController {
    private static final Logger log = LoggerFactory.getLogger(Rsk3x3ComplianceController.class);

    @PostMapping("/screen")
    public ResponseEntity<Map<String, Object>> screen(@RequestBody(required = false) String body) {
        log.info("[RSK3X3] Sanctions screening: Clear for request");
        return ResponseEntity.ok(Map.of(
            "status", "Clear",
            "referenceId", "RSK-" + UUID.randomUUID(),
            "screenedAt", OffsetDateTime.now().toString()
        ));
    }
}
