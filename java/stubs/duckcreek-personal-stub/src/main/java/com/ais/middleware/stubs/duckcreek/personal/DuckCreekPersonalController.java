package com.ais.middleware.stubs.duckcreek.personal;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.UUID;

@RestController
@RequestMapping("/policy")
public class DuckCreekPersonalController {
    private static final Logger log = LoggerFactory.getLogger(DuckCreekPersonalController.class);

    @PostMapping("/issue")
    public ResponseEntity<Map<String, Object>> issuePolicy(@RequestBody(required = false) String body) {
        String policyNumber = "DC-PERS-" + UUID.randomUUID().toString().substring(0, 8).toUpperCase();
        log.info("[DuckCreek Personal] Policy issued: {}", policyNumber);
        return ResponseEntity.ok(Map.of(
            "status", "SUCCESS",
            "policyNumber", policyNumber,
            "targetPas", "DuckCreek-Personal",
            "issuedAt", OffsetDateTime.now().toString()
        ));
    }
}
