package com.ais.middleware.stubs.erm7x1;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.UUID;

@RestController
public class Erm7x1AccountController {
    private static final Logger log = LoggerFactory.getLogger(Erm7x1AccountController.class);

    @GetMapping("/account-service")
    public ResponseEntity<Map<String, Object>> getAccountService() {
        String accountServiceRequestNumber = "ERM-" + UUID.randomUUID().toString().substring(0, 8).toUpperCase();
        log.info("[ERM7X1] AccountServiceRequestNumber generated: {}", accountServiceRequestNumber);
        return ResponseEntity.ok(Map.of(
            "accountServiceRequestNumber", accountServiceRequestNumber,
            "status", "Active",
            "retrievedAt", OffsetDateTime.now().toString()
        ));
    }
}
