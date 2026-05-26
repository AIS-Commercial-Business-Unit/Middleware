package com.ais.middleware.stubs.forefront;

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
public class ForeFrontController {
    private static final Logger log = LoggerFactory.getLogger(ForeFrontController.class);

    @PostMapping("/issue")
    public ResponseEntity<Map<String, Object>> issuePolicy(@RequestBody(required = false) String body) {
        String policyNumber = "FF-" + UUID.randomUUID().toString().substring(0, 8).toUpperCase();
        log.info("[ForeFront] Policy issued: {}", policyNumber);
        return ResponseEntity.ok(Map.of(
            "status", "SUCCESS",
            "policyNumber", policyNumber,
            "targetPas", "ForeFront",
            "issuedAt", OffsetDateTime.now().toString()
        ));
    }
}
